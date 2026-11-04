using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>Art-only comparison. Does not rebuild Robot_Boss or modify Main.</summary>
    public sealed class BossHandRigComparisonMenu : EditorWindow
    {
        const string Art = "Assets/_Anseong/Art/Boss/HandRigComparison";
        const string Scene = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Yujin_HandRigComparison.unity";
        static readonly string[] States = { "SwordIdle", "Combat_SwordSlash", "Combat_OverheadSmash" };
        int selected;
        float normalizedTime;

        [MenuItem("Anseong/Sandbox/Build Yujin Hand Rig Comparison")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/PreviousApproved.prefab");
            var matched = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/MatchedHandRig.prefab");
            if (!previous || !matched) throw new InvalidOperationException("HandRigComparison art assets are missing.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("A Lit shader is required for the preview.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/ComparisonSteel.mat");
            if (!material)
            {
                material = new Material(shader) { name = "ComparisonSteel" };
                material.color = new Color(.3f, .34f, .38f);
                material.SetFloat("_Metallic", .7f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .35f);
                AssetDatabase.CreateAsset(material, Art + "/ComparisonSteel.mat");
            }
            float referenceScale = 0;
            for (int side = 0; side < 2; side++)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(side == 0 ? previous : matched);
                var animator = model.GetComponent<Animator>();
                var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == States[0]);
                clip.SampleAnimation(model, 0);
                var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                var bounds = SkinBounds(model);
                // Comparison-only art scale; gameplay prefabs and their calibration stay independent.
                if (side == 0) referenceScale = 65f / bounds.size.y;
                float scale = referenceScale;
                model.transform.localScale *= scale;
                float x = side == 0 ? -55f : 55f;
                model.transform.position = new Vector3(x, -bounds.min.y * scale, 0);
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var camera = new GameObject(side == 0 ? "PreviousCamera" : "MatchedCamera").AddComponent<Camera>();
                camera.transform.position = new Vector3(x + 12f, 38f, 180f);
                camera.transform.LookAt(new Vector3(x, 32f, 0));
                camera.orthographic = true;
                camera.orthographicSize = 46f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 400f;
                camera.rect = new Rect(side * .5f, 0, .5f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .09f, .11f);
            }
            var light = new GameObject("ComparisonKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35, 180, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            Directory.CreateDirectory(Path.GetDirectoryName(Scene));
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, Scene);
            AssetDatabase.SaveAssets();
            Open();
            Debug.Log("Hand rig comparison saved. Left: previous reference. Right: matched hand rig. Play cycles three sword clips.");
        }

        static Bounds SkinBounds(GameObject model)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var mesh = renderer.sharedMesh;
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var matrices = renderer.bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i]).ToArray();
                foreach (int index in mesh.triangles.Distinct())
                {
                    var w = weights[index]; var v = vertices[index];
                    Vector3 point = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
                    if (w.weight1 > 0) point += matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
                    if (w.weight2 > 0) point += matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
                    if (w.weight3 > 0) point += matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            if (first) throw new InvalidOperationException("No skinned surface in the comparison prefab.");
            return bounds;
        }

        [MenuItem("Anseong/Sandbox/Hand Rig Comparison Controls")]
        public static void Open() => GetWindow<BossHandRigComparisonMenu>("Hand Rig Comparison");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Left: previous / Right: matched", EditorStyles.boldLabel);
            selected = EditorGUILayout.Popup("Motion", selected, States);
            normalizedTime = EditorGUILayout.Slider("Pose time", normalizedTime, 0, 1);
            if (GUILayout.Button(EditorApplication.isPlaying ? "Play selected motion" : "Show selected pose"))
            {
                foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    string path = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                    if (!path.StartsWith(Art + "/", StringComparison.Ordinal)) continue;
                    if (EditorApplication.isPlaying) animator.Play(States[selected], 0, normalizedTime);
                    else
                    {
                        var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == States[selected]);
                        clip.SampleAnimation(animator.gameObject, normalizedTime * clip.length);
                    }
                }
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox("Art preview only: three sampled sword motions, no gameplay or VR verification. Main and Robot_Boss are not changed.", MessageType.Info);
        }
    }
}
