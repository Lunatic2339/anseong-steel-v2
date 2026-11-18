using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace Anseong.MapShowcase.Editor
{
    /// Rebuilds the showcase work scene from a fresh copy of the production scene, then applies each pass in order.
    public static class ShowcaseBuild
    {
        const string Root = "Assets/MapShowcase";

        public static void BuildAll()
        {
            var stage = int.Parse(ShowcaseCapture.Arg("-stage", "99"));
            foreach (var d in new[] { "Scenes", "Materials", "Meshes", "Prefabs", "Lighting" }) Directory.CreateDirectory(Root + "/" + d);
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(ShowcaseCapture.WorkScene);
            if (!AssetDatabase.CopyAsset(ShowcaseCapture.SourceScene, ShowcaseCapture.WorkScene)) throw new System.Exception("scene copy failed");
            var scene = EditorSceneManager.OpenScene(ShowcaseCapture.WorkScene);
            if (stage >= 1) Atmosphere();
            if (stage >= 2) Ground();
            if (stage >= 3) ShowcaseBuildings.Apply();
            if (stage >= 4) ShowcaseProps.Apply();
            if (stage >= 5) ShowcaseLandscape.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHOWCASE build stage " + stage + " saved");
            ShowcaseCapture.CaptureOpen(ShowcaseCapture.Arg("-label", "after"));
        }

        static float F(string key, float def) { return float.Parse(ShowcaseCapture.Arg(key, def.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture); }

        // ---------- 1. sky / fog / exposure / post
        static void Atmosphere()
        {
            // fog colour sampled from the actual sky just above the horizon, so distant hills fade into the sky
            var go = new GameObject("tmp sky sampler"); var cam = go.AddComponent<Camera>();
            cam.cullingMask = 0; cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 20; cam.allowHDR = false;
            var rt = new RenderTexture(64, 64, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); var tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
            Color sum = Color.black; int n = 0;
            foreach (var yaw in new[] { -60f, -30f, 0f, 30f, 60f })
            {
                go.transform.rotation = Quaternion.Euler(-6, yaw, 0); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); tex.Apply(); RenderTexture.active = null;
                foreach (var c in tex.GetPixels()) { sum += c; n++; }
            }
            cam.targetTexture = null; Object.DestroyImmediate(go);
            var sky = sum / n; sky.a = 1;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = F("-fogDensity", 0.0011f);
            RenderSettings.fogColor = new Color(sky.r * .86f, sky.g * .88f, sky.b * .92f, 1);
            Debug.Log("SHOWCASE fog colour from sky " + sky);

            // own copy of the grading profile so the production scene keeps its look
            var vol = Object.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(v => v.isGlobal);
            var path = Root + "/Lighting/SC_PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(vol.sharedProfile), path);
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
            vol.sharedProfile = profile;
            ColorGrading cg; if (!profile.TryGetSettings(out cg)) cg = profile.AddSettings<ColorGrading>();
            cg.enabled.Override(true);
            cg.gradingMode.Override(GradingMode.HighDefinitionRange);
            cg.tonemapper.Override(Tonemapper.ACES);
            cg.postExposure.Override(F("-exposure", 1.25f));
            cg.contrast.Override(F("-contrast", 10f));
            cg.saturation.Override(F("-saturation", -6f));
            cg.temperature.Override(4f);
            Bloom bloom; if (profile.TryGetSettings(out bloom)) { bloom.intensity.Override(0.6f); bloom.threshold.Override(1.1f); }
            EditorUtility.SetDirty(profile);
        }

        // ---------- soft contact shadow under objects (overcast light gives no cast shadows, so things look pasted on without it)
        static Material shadowMat;
        public static void ContactShadow(Transform parent, Vector3 localCentre, float sizeX, float sizeZ, float alpha)
        {
            if (shadowMat == null)
            {
                var tp = Root + "/Textures/SC_ContactShadow.png"; var imp = (TextureImporter)AssetImporter.GetAtPath(tp);
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; imp.SaveAndReimport();
                var mp = Root + "/Materials/SC_ContactShadow.mat"; shadowMat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (shadowMat == null) { shadowMat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(shadowMat, mp); }
                shadowMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); shadowMat.color = new Color(0, 0, 0, 1);
                shadowMat.SetFloat("_Mode", 2); shadowMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); shadowMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); shadowMat.SetInt("_ZWrite", 0);
                shadowMat.DisableKeyword("_ALPHATEST_ON"); shadowMat.EnableKeyword("_ALPHABLEND_ON"); shadowMat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); shadowMat.renderQueue = 3000;
                shadowMat.SetFloat("_Glossiness", 0); shadowMat.SetFloat("_SpecularHighlights", 0); shadowMat.SetFloat("_GlossyReflections", 0);
                shadowMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); shadowMat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                EditorUtility.SetDirty(shadowMat);
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>()); q.name = "SC_ContactShadow";
            q.transform.SetParent(parent, false); q.transform.localPosition = localCentre; q.transform.localRotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = new Vector3(sizeX, sizeZ, 1);
            var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = shadowMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_Color", new Color(0, 0, 0, alpha)); mr.SetPropertyBlock(mpb);
        }

        // ---------- 2. ground: tiling scanned PBR (detail) + large reference-derived pattern (macro), Standard shader only
        static Texture2D Tex(string name, bool normal, bool alphaSmooth, int max)
        {
            var p = Root + "/Textures/" + name + ".png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal; imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 12; imp.maxTextureSize = max; imp.mipmapEnabled = true;
            imp.alphaSource = alphaSmooth ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        static Material GroundMat(string name, Texture2D macro, Texture2D detail, Texture2D detailN, Color tint, float macroMetres, float detailMetres, Vector2 offset)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Standard");
            m.SetTexture("_MainTex", macro); m.SetTextureScale("_MainTex", Vector2.one / macroMetres); m.SetTextureOffset("_MainTex", offset);
            m.color = tint; m.SetFloat("_Metallic", 0);
            m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); m.SetFloat("_GlossMapScale", 1f);
            m.SetTexture("_DetailAlbedoMap", detail); m.SetTexture("_DetailNormalMap", detailN); m.SetFloat("_DetailNormalMapScale", 0.9f);
            // detail tiling is relative to raw UV (metres)
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one / detailMetres);
            m.EnableKeyword("_DETAIL_MULX2");
            m.SetFloat("_UVSec", 0);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Ground()
        {
            var macroC = Tex("SC_GroundMacro_Concrete", false, true, 4096); var macroA = Tex("SC_GroundMacro_Road", false, true, 4096); var macroS = Tex("SC_GroundMacro_Slabs", false, true, 4096);
            var detC = Tex("SC_GroundDetail_Concrete", false, false, 1024); var detA = Tex("SC_GroundDetail_Asphalt", false, false, 1024);
            var nC = Tex("SC_GroundDetail_Concrete_Normal", true, false, 2048); var nA = Tex("SC_GroundDetail_Asphalt_Normal", true, false, 2048);
            var mats = new Dictionary<string, Material>
            {
                { "Yard", GroundMat("SC_Ground_ConcreteYard", macroS, detC, nC, new Color(1f, 1f, 1f), 160, 3.0f, Vector2.zero) },
                { "Site", GroundMat("SC_Ground_SiteConcrete", macroC, detC, nC, new Color(.86f, .86f, .85f), 128, 3.0f, new Vector2(.37f, .61f)) },
                { "Asphalt", GroundMat("SC_Ground_Road", macroA, detA, nA, new Color(.95f, .95f, .97f), 160, 2.2f, new Vector2(.11f, .23f)) },
            };
            var groundShaders = new HashSet<string> { "Anseong/PurposeGround", "Anseong/PhotographicGround", "Anseong/GeologicalGround" };
            var buckets = mats.Keys.ToDictionary(k => k, k => new List<CombineInstance>());
            int hidden = 0;
            foreach (var r in ShowcaseGround.Candidates().ToList())
            {
                var m = r.sharedMaterial; if (!groundShaders.Contains(m.shader.name)) continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                string g = m.HasProperty("_GravelTex") && m.GetTexture("_GravelTex") ? m.GetTexture("_GravelTex").name : "";
                string key = g.StartsWith("Asphalt") || g.StartsWith("Road") ? "Asphalt" : g.StartsWith("Concrete") || g == "" ? "Yard" : "Site";
                // wide asphalt areas are yards, not roads: only strips up to ~34 m wide keep the dark road surface
                if (key == "Asphalt" && Mathf.Min(r.bounds.size.x, r.bounds.size.z) > 40f) key = "Yard";
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    buckets[key].Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = r.transform.localToWorldMatrix });
                r.enabled = false; hidden++;
            }
            var root = new GameObject("SC_Ground");
            foreach (var kv in buckets)
            {
                if (kv.Value.Count == 0) continue;
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "SC_Ground_" + kv.Key };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true, false);
                var v = mesh.vertices; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x, v[i].z);   // world metres
                mesh.uv = uv; mesh.uv2 = null; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                var mp = Root + "/Meshes/SC_Ground_" + kv.Key + ".asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
                var child = new GameObject("SC_Ground_" + kv.Key); child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = child.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[kv.Key]; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
                mr.lightProbeUsage = LightProbeUsage.Off; mr.receiveGI = ReceiveGI.LightProbes;
                Debug.Log("SHOWCASE ground " + kv.Key + " pieces " + kv.Value.Count + " verts " + v.Length);
            }
            var pp = Root + "/Prefabs/SC_Ground.prefab"; AssetDatabase.DeleteAsset(pp);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, pp, InteractionMode.AutomatedAction);
            Debug.Log("SHOWCASE ground hidden originals " + hidden);
        }
    }
}
