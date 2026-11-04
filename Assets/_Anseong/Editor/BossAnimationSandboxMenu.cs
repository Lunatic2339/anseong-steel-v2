using System;
using System.IO;
using System.Linq;
using Anseong.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Anseong.EditorTools
{
    public static class BossAnimationSandboxMenu
    {
        public const string Root = "Assets/_Anseong/Art/Boss/AnimationSandbox";
        public const string ScenePath = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Yujin_BossAnimations.unity";
        [MenuItem("Anseong/Sandbox/Build Final Boss Animation Test")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string map in new[] { "BaseColor", "Normal", "MetallicSmoothness", "Emission" })
            {
                string path = Root + "/Textures/SteelV13_" + map + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Missing final texture " + path);
                importer.textureType = map == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = map == "BaseColor" || map == "Emission";
                importer.maxTextureSize = 8192;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is missing.");
            var steel = MaterialAt("BossV13Final", shader);
            steel.SetTexture("_BaseMap", Texture("BaseColor")); steel.SetColor("_BaseColor", Color.white);
            steel.SetTexture("_BumpMap", Texture("Normal")); steel.SetFloat("_BumpScale", 1);
            steel.SetTexture("_MetallicGlossMap", Texture("MetallicSmoothness")); steel.SetFloat("_Metallic", 1); steel.SetFloat("_Smoothness", 1);
            steel.SetTexture("_EmissionMap", Texture("Emission")); steel.SetColor("_EmissionColor", Color.white * 1.6f);
            steel.EnableKeyword("_NORMALMAP"); steel.EnableKeyword("_METALLICSPECGLOSSMAP"); steel.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(steel);
            var swordMaterial = MaterialAt("GreatswordSteel", shader); swordMaterial.SetColor("_BaseColor", new Color(.22f, .24f, .27f)); swordMaterial.SetFloat("_Metallic", .95f); swordMaterial.SetFloat("_Smoothness", .4f); EditorUtility.SetDirty(swordMaterial);
            var groundMaterial = MaterialAt("PreviewGround", shader); groundMaterial.SetColor("_BaseColor", new Color(.065f, .075f, .085f)); groundMaterial.SetFloat("_Smoothness", .2f); EditorUtility.SetDirty(groundMaterial);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/BossV13FinalAnimation.prefab");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var animator = model.GetComponent<Animator>();
            var idle = animator.runtimeAnimatorController.animationClips.First(c => c.name == "Idle");
            idle.SampleAnimation(model, 0);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.sharedMaterials = skin.sharedMaterials.Select(_ => steel).ToArray();
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => swordMaterial).ToArray();
            // Save the material-equipped prefab through Unity; the earlier comparison prefab remains independent.
            PrefabUtility.ApplyPrefabInstance(model, InteractionMode.AutomatedAction);
            var bounds = SurfaceBounds(model);
            float scale = 65 / bounds.size.y;
            model.transform.localScale *= scale;
            model.transform.position -= Vector3.up * bounds.min.y * scale;
            var center = new GameObject("InspectionCenter").transform; center.position = new Vector3(0, 32.5f, 0);
            var camera = new GameObject("InspectionCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.fieldOfView = 38; camera.nearClipPlane = .1f; camera.farClipPlane = 1000; camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .055f, .07f);
            camera.transform.position = center.position + new Vector3(20, 10, 120); camera.transform.LookAt(center);
            camera.gameObject.AddComponent<AudioListener>();
            var key = new GameObject("InspectionKey").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 2;
            key.transform.rotation = Quaternion.Euler(35, 150, 0); key.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.35f, .38f, .42f);
            var reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(Root + "/SteelReflection.asset");
            if (reflection) { RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = reflection; RenderSettings.reflectionIntensity = 1; }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "InspectionGround"; ground.transform.localScale = Vector3.one * 24; ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial; UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/SandboxPanelSettings.asset");
            if (!panelSettings) { panelSettings = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panelSettings, Root + "/SandboxPanelSettings.asset"); }
            string generatedTheme = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
            string themePath = Root + "/RuntimeTheme.tss";
            if (!AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath) && AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(generatedTheme))
            {
                string error = AssetDatabase.MoveAsset(generatedTheme, themePath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (Directory.Exists("Assets/UI Toolkit") && !Directory.GetFiles("Assets/UI Toolkit", "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta"))) AssetDatabase.DeleteAsset("Assets/UI Toolkit");
            }
            panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize; panelSettings.referenceResolution = new Vector2Int(1600, 900); EditorUtility.SetDirty(panelSettings);
            var ui = new GameObject("AnimationControls").AddComponent<UIDocument>(); ui.panelSettings = panelSettings;
            var controller = ui.gameObject.AddComponent<BossAnimationSandbox>();
            var catalogue = JsonUtility.FromJson<BossAnimationSandbox.Catalogue>(File.ReadAllText(Root + "/MotionCatalogue.json"));
            foreach (var motion in catalogue.motions)
            {
                var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == motion.state);
                Bounds envelope = default; bool first = true;
                for (int sample = 0; sample <= 10; sample++)
                {
                    clip.SampleAnimation(model, clip.length * sample / 10f);
                    var poseBounds = SurfaceBounds(model);
                    foreach (var weapon in model.GetComponentsInChildren<MeshRenderer>()) poseBounds.Encapsulate(weapon.bounds);
                    if (first) { envelope = poseBounds; first = false; } else envelope.Encapsulate(poseBounds);
                }
                motion.viewCenter = envelope.center;
                float tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                motion.viewDistance = (Mathf.Max(envelope.extents.y / tangent, envelope.extents.x / (tangent * 1.4f)) + envelope.extents.z) * 1.12f / 65;
            }
            idle.SampleAnimation(model, 0);
            var wrist = model.GetComponentsInChildren<Transform>().First(t => t.name == "HF_mixamorig:RightHand");
            var finger = model.GetComponentsInChildren<Transform>().First(t => t.name == "HF_mixamorig:RightHandMiddle3");
            controller.Configure(animator, ui, camera, center, wrist, finger, catalogue.motions, 65);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("FINAL BOSS ANIMATION SANDBOX READY: " + catalogue.motions.Length + " motions; final HQ51 maps and revised hands.");
        }
        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/SteelV13_" + name + ".png");
        static Material MaterialAt(string name, Shader shader)
        {
            string path = Root + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        static Bounds SurfaceBounds(GameObject model)
        {
            Bounds bounds = default; bool first = true;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.enabled) continue;
                var mesh = r.sharedMesh; var vertices = mesh.vertices; var weights = mesh.boneWeights;
                var matrices = r.bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i]).ToArray();
                foreach (int index in mesh.triangles.Distinct())
                {
                    var w = weights[index]; var v = vertices[index]; var point = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
                    if (w.weight1 > 0) point += matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
                    if (w.weight2 > 0) point += matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
                    if (w.weight3 > 0) point += matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }
}
