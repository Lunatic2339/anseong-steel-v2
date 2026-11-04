using System.Collections;
using System.IO;
using System.Linq;
using Anseong.Boss;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace Anseong.Tests
{
    public class BossAnimationSandboxTests
    {
        const string Output = "docs/test_vid/BossAnimationSandbox";
        [UnityTest]
        public IEnumerator AllMotionsPlayWithFinalTexturesAndRevisedHands()
        {
            EditorSceneManager.OpenScene("Assets/_Anseong/Scenes/Sandbox/Sandbox_Yujin_BossAnimations.unity");
            yield return new EnterPlayMode();
            yield return null;
            var preview = Object.FindFirstObjectByType<BossAnimationSandbox>();
            Assert.That(preview, Is.Not.Null); Assert.That(preview.MotionCount, Is.EqualTo(22));
            var actor = preview.Animator; var ui = preview.GetComponent<UIDocument>();
            Assert.That(ui.rootVisualElement.Query<Button>().ToList().Count(b => b.name.StartsWith("Motion_")), Is.EqualTo(22));
            Assert.That(ui.rootVisualElement.Q("MotionPanel").worldBound.height, Is.GreaterThan(200));
            Assert.That(actor.GetComponentsInChildren<Transform>().Any(t => t.name == "HF_mixamorig:RightHandThumb1"), Is.True);
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (var material in skin.sharedMaterials)
                {
                    foreach (var slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" }) Assert.That(material.GetTexture(slot), Is.Not.Null, slot);
                    Assert.That(material.shader.isSupported, Is.True);
                }
            var settings = ui.panelSettings;
            var captureSettings = Object.Instantiate(settings);
            var uiTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            captureSettings.targetTexture = uiTexture;
            ui.panelSettings = captureSettings;
            yield return null; yield return null;
            var uiImage = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var uiActive = RenderTexture.active;
            RenderTexture.active = uiTexture;
            uiImage.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); uiImage.Apply();
            Directory.CreateDirectory(Output); File.WriteAllBytes(Output + "/Controls.png", uiImage.EncodeToPNG());
            RenderTexture.active = uiActive; ui.panelSettings = settings;
            uiTexture.Release(); Object.Destroy(uiTexture); Object.Destroy(uiImage); Object.Destroy(captureSettings);
            yield return null;
            var camera = Camera.main;
            Directory.CreateDirectory(Output);
            var lines = new System.Collections.Generic.List<string>();
            for (int i = 0; i < preview.MotionCount; i++)
            {
                preview.SelectMotion(i); yield return null;
                Assert.That(actor.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash(preview.CurrentMotion)), preview.CurrentMotion);
                foreach (float time in new[] { .15f, .5f, .85f })
                {
                    preview.Seek(time); yield return null;
                    foreach (var bone in actor.GetComponentsInChildren<Transform>())
                        Assert.That(float.IsNaN(bone.position.x) || float.IsInfinity(bone.position.x), Is.False, bone.name);
                    if (new[] { "Idle", "SwordIdle", "Punch", "DrawSword", "SheatheSword", "Combat_SwordSlash", "Combat_Grab" }.Contains(preview.CurrentMotion))
                        Capture(camera, preview.CurrentMotion + "_" + Mathf.RoundToInt(time * 100));
                }
                lines.Add(preview.CurrentMotion + ": selected, evaluated at 15/50/85 percent; finite transforms.");
            }
            preview.SelectMotion(0); yield return null; preview.SetPaused(true);
            float before = actor.GetCurrentAnimatorStateInfo(0).normalizedTime; yield return null; yield return null;
            Assert.That(actor.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(before).Within(.0001f));
            preview.SetPaused(false); yield return null; yield return null;
            Assert.That(actor.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(before));
            preview.SetCameraView(2); yield return null; Capture(camera, "HandCloseup_Idle");
            File.WriteAllLines(Output + "/verification.txt", lines.Concat(new[] { "Unity " + Application.unityVersion, "22 UI motion buttons; four final maps; revised HF hand bones; pause/resume checked." }));
            yield return new ExitPlayMode();
        }
        static void Capture(Camera camera, string name)
        {
            var rt = new RenderTexture(1400, 1000, 24, RenderTextureFormat.ARGB32); var image = new Texture2D(1400, 1000, TextureFormat.RGB24, false); var active = RenderTexture.active;
            try { RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt }); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
            finally { RenderTexture.active = active; rt.Release(); Object.Destroy(rt); Object.Destroy(image); }
        }
    }
}
