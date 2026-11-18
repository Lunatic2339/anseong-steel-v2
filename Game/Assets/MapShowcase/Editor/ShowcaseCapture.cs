using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Anseong.MapShowcase.Editor
{
    public static class ShowcaseCapture
    {
        public const string SourceScene = "Assets/Map35/Scenes/Steelworks35m_WideRuins.unity";
        public const string WorkScene = "Assets/MapShowcase/Scenes/Steelworks35m_Showcase.unity";
        public const string OutRoot = "../docs/validation/map-showcase";

        struct View { public string name; public Vector3 pos; public Vector3 look; }
        static readonly View[] Views =
        {
            new View{name="v1-p1-z1-north", pos=new Vector3(0,30,65), look=new Vector3(0,22,265)},
            new View{name="v2-p1-z2-north", pos=new Vector3(0,30,205), look=new Vector3(0,22,405)},
            new View{name="v3-p2-z2-down", pos=new Vector3(20,50,180), look=new Vector3(20,50-93.3f,380)},
            new View{name="v4-p1-diagonal", pos=new Vector3(0,30,360), look=new Vector3(90,28,555)},
            new View{name="v5-p1-crane-furnace", pos=new Vector3(90,30,504), look=new Vector3(92,34,715)},
            new View{name="v6-p2-crane-under", pos=new Vector3(125,50,480), look=new Vector3(78,12,535)},
        };

        public static string Arg(string key, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return def;
        }

        public static void EnsureWorkScene()
        {
            if (!File.Exists(WorkScene))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(WorkScene));
                AssetDatabase.Refresh();
                if (!AssetDatabase.CopyAsset(SourceScene, WorkScene)) throw new Exception("copy failed");
                AssetDatabase.Refresh();
            }
        }

        public static void CaptureSource() { EditorSceneManager.OpenScene(SourceScene); CaptureOpen(Arg("-label", "before")); }
        public static void CaptureWork() { EnsureWorkScene(); EditorSceneManager.OpenScene(WorkScene); CaptureOpen(Arg("-label", "after")); }

        public static void CaptureOpen(string label)
        {
            var dir = System.IO.Path.Combine(OutRoot, label); Directory.CreateDirectory(dir);
            var template = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.GetComponent<PostProcessLayer>() != null);
            var go = new GameObject("ShowcaseCaptureCam"); go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            if (template != null) cam.CopyFrom(template);
            cam.fieldOfView = 75; cam.nearClipPlane = 0.5f; cam.farClipPlane = 6000; cam.allowHDR = true; cam.enabled = false;
            cam.rect = new Rect(0, 0, 1, 1); cam.stereoTargetEye = StereoTargetEyeMask.None;
            if (template != null)
            {
                var src = template.GetComponent<PostProcessLayer>();
                var layer = go.AddComponent<PostProcessLayer>();
                EditorUtility.CopySerialized(src, layer);
                layer.volumeTrigger = go.transform;
            }
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var views = new System.Collections.Generic.List<View>(Views);
            if (Arg("-extra", "0") == "1") { views.Clear(); views.Add(new View { name = "x1-b01-outside", pos = new Vector3(18, 30, 100), look = new Vector3(0, 22, -28) }); views.Add(new View { name = "x2-b01-inside", pos = new Vector3(0, 30, -22), look = new Vector3(0, 24, 120) }); views.Add(new View { name = "x3-b01-start", pos = new Vector3(0, 30, 2), look = new Vector3(0, 24, 200) }); }
            if (Arg("-extra", "0") == "2") { views.Clear();
                views.Add(new View { name = "y1-mountain-northwest", pos = new Vector3(0, 50, 300), look = new Vector3(-600, 70, 1200) });
                views.Add(new View { name = "y2-mountain-west", pos = new Vector3(-60, 40, 250), look = new Vector3(-900, 60, 400) });
                views.Add(new View { name = "y3-sea-east", pos = new Vector3(310, 55, 480), look = new Vector3(1500, 0, 620) });
                views.Add(new View { name = "y4-coast-northeast", pos = new Vector3(230, 35, 300), look = new Vector3(620, 5, 900) }); }
            foreach (var v in views)
            {
                go.transform.position = v.pos; go.transform.rotation = Quaternion.LookRotation(v.look - v.pos, Vector3.up);
                cam.targetTexture = rt; cam.aspect = (float)W / H;
                for (int i = 0; i < 3; i++) cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes(System.IO.Path.Combine(dir, v.name + ".png"), tex.EncodeToPNG());
            }
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("SHOWCASE captured " + label + " template=" + (template ? template.name : "none"));
        }

        public static void Dump()
        {
            EditorSceneManager.OpenScene(Arg("-scene", SourceScene));
            var sb = new StringBuilder();
            sb.AppendLine("fog " + RenderSettings.fog + " " + RenderSettings.fogMode + " " + RenderSettings.fogColor + " " + RenderSettings.fogStartDistance + "-" + RenderSettings.fogEndDistance + " dens " + RenderSettings.fogDensity);
            sb.AppendLine("skybox " + (RenderSettings.skybox ? AssetDatabase.GetAssetPath(RenderSettings.skybox) + " shader " + RenderSettings.skybox.shader.name : "null") + " ambientMode " + RenderSettings.ambientMode + " intensity " + RenderSettings.ambientIntensity);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lights.Where(l => l.type == LightType.Directional))
                sb.AppendLine("dirlight " + P(l.transform) + " int " + l.intensity + " col " + l.color + " rot " + l.transform.eulerAngles + " shadows " + l.shadows + " mode " + l.lightmapBakeType + " active " + l.gameObject.activeInHierarchy);
            sb.AppendLine("lights total " + lights.Length);
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine("camera " + P(c.transform) + " pos " + c.transform.position + " fov " + c.fieldOfView + " hdr " + c.allowHDR + " ppl " + (c.GetComponent<PostProcessLayer>() != null) + " active " + c.gameObject.activeInHierarchy);
            foreach (var v in UnityEngine.Object.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine("ppvolume " + P(v.transform) + " global " + v.isGlobal + " profile " + AssetDatabase.GetAssetPath(v.sharedProfile));
                if (v.sharedProfile) foreach (var s in v.sharedProfile.settings) sb.AppendLine("   " + s.GetType().Name + " enabled " + s.enabled.value);
            }
            sb.AppendLine("QUALITY " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " shadowDist " + QualitySettings.shadowDistance + " colorSpace " + PlayerSettings.colorSpace);
            sb.AppendLine("== hierarchy (depth<=2) with renderer counts and bounds");
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) DumpNode(root.transform, 0, sb);
            sb.AppendLine("== big renderers (bounds size >= 25m any axis) inside X-260..300 Z-80..800");
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in all)
            {
                var b = r.bounds; if (Mathf.Max(b.size.x, b.size.y, b.size.z) < 25) continue;
                if (b.center.x < -260 || b.center.x > 300 || b.center.z < -80 || b.center.z > 800) continue;
                var mf = r.GetComponent<MeshFilter>();
                sb.AppendLine(P(r.transform) + " | c " + b.center.ToString("F0") + " s " + b.size.ToString("F0") + " | mats " + string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "[" + m.shader.name + "]" : "null")) + " | idx " + (mf && mf.sharedMesh ? mf.sharedMesh.GetIndexCount(0) / 3 : 0) + " lm " + r.lightmapIndex);
            }
            sb.AppendLine("== shaders in use");
            foreach (var g in all.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().GroupBy(m => m.shader.name))
                sb.AppendLine(g.Key + " x" + g.Count());
            File.WriteAllText(Arg("-dumpOut", "Temp/showcase-dump.txt"), sb.ToString());
        }

        static void DumpNode(Transform t, int depth, StringBuilder sb)
        {
            var rs = t.GetComponentsInChildren<Renderer>(false);
            string bs = "";
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); bs = " c " + b.center.ToString("F0") + " s " + b.size.ToString("F0"); }
            sb.AppendLine(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (off)") + " r" + rs.Length + bs);
            if (depth < 2) foreach (Transform c in t) DumpNode(c, depth + 1, sb);
        }
        static string P(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
    }
}
