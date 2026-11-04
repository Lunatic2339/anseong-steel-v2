using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Anseong.Tests
{
    public class BossHandRigComparisonTests
    {
        const string ScenePath = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Yujin_HandRigComparison.unity";

        [UnityTest]
        public IEnumerator ComparisonPlaysAllSwordStatesWithMatchingWeaponPose()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            Assert.That(animators.Length, Is.EqualTo(2));
            var previous = animators.Single(a => a.runtimeAnimatorController.name == "Previous");
            var matched = animators.Single(a => a.runtimeAnimatorController.name == "Matched");
            var oldSword = previous.GetComponentsInChildren<MeshRenderer>().Single().transform;
            var newSword = matched.GetComponentsInChildren<MeshRenderer>().Single().transform;
            var expected = new[] { "SwordIdle", "Combat_SwordSlash", "Combat_OverheadSmash" };
            var seen = new HashSet<string>();
            float maxDistance = 0, maxAngle = 0;
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                var oldState = previous.GetCurrentAnimatorStateInfo(0);
                var newState = matched.GetCurrentAnimatorStateInfo(0);
                Assert.That(newState.shortNameHash, Is.EqualTo(oldState.shortNameHash), "The two comparison controllers must stay synchronized.");
                var oldLocal = previous.transform.InverseTransformPoint(oldSword.position);
                var newLocal = matched.transform.InverseTransformPoint(newSword.position);
                maxDistance = Mathf.Max(maxDistance, Vector3.Distance(oldLocal, newLocal));
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(oldSword.rotation, newSword.rotation));
                foreach (var state in expected)
                {
                    if (!oldState.IsName(state) || oldState.normalizedTime % 1 < .25f || !seen.Add(state)) continue;
                    foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                        Capture(camera, state);
                }
            }
            CollectionAssert.AreEquivalent(expected, seen);
            Assert.That(maxDistance, Is.LessThan(.001f), "Weapon placement drifted relative to the comparison root.");
            Assert.That(maxAngle, Is.LessThan(.1f), "Weapon angle drifted.");
            foreach (var renderer in previous.GetComponentsInChildren<Renderer>().Concat(matched.GetComponentsInChildren<Renderer>()))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                    Assert.That(material.shader.isSupported, Is.True);
                }
            Directory.CreateDirectory("docs/test_vid/BossHandRigComparison");
            File.WriteAllText("docs/test_vid/BossHandRigComparison/Unity26_PlayVerification.txt",
                $"Unity {Application.unityVersion}\nObserved: {string.Join(", ", seen)}\nMaximum root-local sword distance: {maxDistance}\nMaximum sword angle: {maxAngle}\nURP Lit materials supported.\n");
            yield return new ExitPlayMode();
        }

        static void Capture(Camera camera, string state)
        {
            Directory.CreateDirectory("docs/test_vid/BossHandRigComparison");
            var rect = camera.rect;
            var active = RenderTexture.active;
            var target = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
            try
            {
                camera.rect = new Rect(0, 0, 1, 1);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes($"docs/test_vid/BossHandRigComparison/Unity26_{camera.name}_{state}.png", image.EncodeToPNG());
            }
            finally
            {
                camera.rect = rect;
                RenderTexture.active = active;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }
    }
}
