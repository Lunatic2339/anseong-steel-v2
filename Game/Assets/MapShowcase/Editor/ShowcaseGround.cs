using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.MapShowcase.Editor
{
    public static class ShowcaseGround
    {
        static readonly string[] GroundShaders = { "Anseong/PurposeGround", "Anseong/PhotographicGround", "Anseong/GeologicalGround" };

        public static IEnumerable<MeshRenderer> Candidates()
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                var b = r.bounds;
                if (b.size.y > 0.6f || b.center.y > 1.5f || b.center.y < -1f) continue;
                if (b.size.x * b.size.z < 12f) continue;
                if (b.center.x < -480 || b.center.x > 420 || b.center.z < -200 || b.center.z > 900) continue;
                var m = r.sharedMaterial; if (m == null) continue;
                yield return r;
            }
        }

        public static void ListCandidates()
        {
            EditorSceneManager.OpenScene(ShowcaseCapture.SourceScene);
            var sb = new StringBuilder();
            foreach (var r in Candidates().OrderBy(r => r.sharedMaterial.shader.name))
            {
                var m = r.sharedMaterial; var b = r.bounds;
                string tex = "";
                foreach (var p in new[] { "_GravelTex", "_MainTex", "_RockTex" }) if (m.HasProperty(p) && m.GetTexture(p)) tex += p + "=" + m.GetTexture(p).name + " ";
                var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                var t = r.transform; var path = t.name; if (t.parent) path = t.parent.name + "/" + path; if (t.parent && t.parent.parent) path = t.parent.parent.name.Substring(0, 2) + "/" + path;
                sb.AppendLine(m.shader.name.Replace("Anseong/", "") + " | " + path + " | c " + b.center.x.ToString("F0") + "," + b.center.y.ToString("F2") + "," + b.center.z.ToString("F0") + " s " + b.size.x.ToString("F0") + "x" + b.size.z.ToString("F0") + " h" + b.size.y.ToString("F2") + " | col " + col.r.ToString("F2") + "," + col.g.ToString("F2") + "," + col.b.ToString("F2") + " | " + tex + "| rq " + m.renderQueue);
            }
            File.WriteAllText(ShowcaseCapture.Arg("-dumpOut", "Temp/ground-candidates.txt"), sb.ToString());
        }
    }
}
namespace Anseong.MapShowcase.Editor
{
    public static class ShowcaseQuery
    {
        public static void Box()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShowcaseCapture.WorkScene);
            var a = ShowcaseCapture.Arg("-box", "80,640,120,680").Split(','); float x0 = float.Parse(a[0]), z0 = float.Parse(a[1]), x1 = float.Parse(a[2]), z1 = float.Parse(a[3]);
            var sb = new System.Text.StringBuilder();
            foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None))
            { if (!r.enabled) continue; var c = r.bounds.center; if (c.x < x0 || c.x > x1 || c.z < z0 || c.z > z1) continue; var t = r.transform; sb.AppendLine("R " + (t.parent ? (t.parent.parent ? t.parent.parent.name + "/" : "") + t.parent.name + "/" : "") + t.name + " c " + c.ToString("F1") + " s " + r.bounds.size.ToString("F1") + " mat " + (r.sharedMaterial ? r.sharedMaterial.name + "[" + r.sharedMaterial.shader.name + "]" : "")); }
            foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None))
            { var c = l.transform.position; if (c.x < x0 - 40 || c.x > x1 + 40 || c.z < z0 - 40 || c.z > z1 + 40) continue; sb.AppendLine("L " + l.name + " " + l.type + " enabled " + l.enabled + " pos " + c.ToString("F1") + " range " + l.range + " int " + l.intensity + " parent " + (l.transform.parent ? l.transform.parent.name : "")); }
            System.IO.File.WriteAllText(ShowcaseCapture.Arg("-dumpOut", "Temp/box.txt"), sb.ToString());
        }
    }
}
namespace Anseong.MapShowcase.Editor
{
    public static class ShowcasePreview
    {
        // renders one prefab alone from +Z, +X, -Z, -X and above; file names say where the camera stands
        public static void Prefab()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(.75f, .75f, .75f); UnityEngine.RenderSettings.fog = false;
            var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(ShowcaseCapture.Arg("-prefab", ""));
            var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pf);
            var rs = go.GetComponentsInChildren<UnityEngine.Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var lg = new UnityEngine.GameObject("l"); var l = lg.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = .6f; lg.transform.rotation = UnityEngine.Quaternion.Euler(50, 30, 0);
            var cg = new UnityEngine.GameObject("c"); var cam = cg.AddComponent<UnityEngine.Camera>(); cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(.2f, .25f, .35f); cam.fieldOfView = 40; cam.farClipPlane = 5000;
            var rt = new UnityEngine.RenderTexture(800, 600, 24); var tex = new UnityEngine.Texture2D(800, 600, UnityEngine.TextureFormat.RGB24, false);
            float d = b.size.magnitude * 1.35f; var dirs = new[] { new UnityEngine.Vector3(0, .25f, 1), new UnityEngine.Vector3(1, .25f, 0), new UnityEngine.Vector3(0, .25f, -1), new UnityEngine.Vector3(-1, .25f, 0), new UnityEngine.Vector3(0.01f, 1, 0.01f) };
            var names = new[] { "from+Z", "from+X", "from-Z", "from-X", "top" };
            var outDir = ShowcaseCapture.Arg("-out", "Temp/preview"); System.IO.Directory.CreateDirectory(outDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                cg.transform.position = b.center + dirs[i].normalized * d; cg.transform.LookAt(b.center, i == 4 ? UnityEngine.Vector3.forward : UnityEngine.Vector3.up);
                cam.targetTexture = rt; cam.Render(); UnityEngine.RenderTexture.active = rt; tex.ReadPixels(new UnityEngine.Rect(0, 0, 800, 600), 0, 0); tex.Apply(); UnityEngine.RenderTexture.active = null;
                System.IO.File.WriteAllBytes(outDir + "/" + names[i] + ".png", tex.EncodeToPNG());
            }
            UnityEngine.Debug.Log("SHOWCASE preview bounds " + b.size.ToString("F1"));
        }
    }
}
namespace Anseong.MapShowcase.Editor
{
    public static class ShowcaseTerrainQuery
    {
        public static void List()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShowcaseCapture.WorkScene);
            var sb = new System.Text.StringBuilder();
            foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None))
            {
                if (!r.enabled) continue; var b = r.bounds; if (b.size.y < 6 || UnityEngine.Mathf.Max(b.size.x, b.size.z) < 150) continue;
                var mf = r.GetComponent<UnityEngine.MeshFilter>(); var m = mf ? mf.sharedMesh : null; var mat = r.sharedMaterial;
                string tex = ""; if (mat) foreach (var n in mat.GetTexturePropertyNames()) if (mat.GetTexture(n)) tex += n + "=" + mat.GetTexture(n).name + " ";
                sb.AppendLine((r.transform.parent ? r.transform.parent.name + "/" : "") + r.name + " | c " + b.center.ToString("F0") + " s " + b.size.ToString("F0") + " | verts " + (m ? m.vertexCount : 0) + " tris " + (m ? m.triangles.Length / 3 : 0) + " | " + (mat ? mat.name + "[" + mat.shader.name + "] col " + (mat.HasProperty("_Color") ? mat.color.ToString() : "") : "") + " | " + tex);
            }
            System.IO.File.WriteAllText(ShowcaseCapture.Arg("-dumpOut", "Temp/terrain.txt"), sb.ToString());
        }
    }
}
namespace Anseong.MapShowcase.Editor
{
    public static class ShowcaseFlatQuery
    {
        // lists what is still drawn with untextured (blockout) materials in the work scene, grouped by material
        public static void List()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShowcaseCapture.WorkScene);
            var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UnityEngine.Renderer>>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None))
            {
                if (!r.enabled) continue; var m = r.sharedMaterial; if (m == null) continue;
                bool tex = false; foreach (var n in m.GetTexturePropertyNames()) if (m.GetTexture(n)) tex = true;
                if (tex) continue;
                var key = m.name + " [" + m.shader.name + "] col " + (m.HasProperty("_Color") ? m.color.ToString("F2") : "") + (m.IsKeywordEnabled("_EMISSION") ? " EMISSIVE" : "") + " | " + r.GetType().Name;
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new System.Collections.Generic.List<UnityEngine.Renderer>();
                l.Add(r);
            }
            var sb = new System.Text.StringBuilder();
            foreach (var g in System.Linq.Enumerable.OrderByDescending(groups, g => g.Value.Count))
            {
                var b = g.Value[0].bounds; foreach (var r in g.Value) b.Encapsulate(r.bounds);
                sb.AppendLine(g.Value.Count + " x " + g.Key + " | union c " + b.center.ToString("F0") + " s " + b.size.ToString("F0"));
                foreach (var r in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(g.Value, r => r.bounds.size.magnitude), 4))
                { var t = r.transform; string p = t.name; for (int i = 0; i < 3 && t.parent; i++) { t = t.parent; p = t.name + "/" + p; } sb.AppendLine("    " + p + " c " + r.bounds.center.ToString("F0") + " s " + r.bounds.size.ToString("F1")); }
            }
            System.IO.File.WriteAllText(ShowcaseCapture.Arg("-dumpOut", "Temp/flat.txt"), sb.ToString());
        }
    }
}
namespace Anseong.MapShowcase.Editor
{
    public static class ShowcaseOldQuery
    {
        // what is still drawn with the old blockout shaders/materials, grouped by its two top hierarchy levels
        public static void List()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShowcaseCapture.WorkScene);
            var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UnityEngine.Renderer>>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None))
            {
                if (!r.enabled || r is UnityEngine.ParticleSystemRenderer) continue; var t = r.transform; var chain = new System.Collections.Generic.List<string>(); while (t) { chain.Insert(0, t.name); t = t.parent; }
                if (chain[0].StartsWith("SC_")) continue;
                var key = chain[0] + " / " + (chain.Count > 2 ? chain[1] : "");
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new System.Collections.Generic.List<UnityEngine.Renderer>(); l.Add(r);
            }
            var sb = new System.Text.StringBuilder();
            foreach (var g in System.Linq.Enumerable.OrderByDescending(groups, g => { var b = g.Value[0].bounds; foreach (var r in g.Value) b.Encapsulate(r.bounds); return b.size.y; }))
            {
                var b = g.Value[0].bounds; foreach (var r in g.Value) b.Encapsulate(r.bounds);
                var big = System.Linq.Enumerable.First(System.Linq.Enumerable.OrderByDescending(g.Value, r => r.bounds.size.magnitude));
                sb.AppendLine(g.Value.Count + " | " + g.Key + " | c " + b.center.ToString("F0") + " s " + b.size.ToString("F0") + " | biggest " + big.name + " s " + big.bounds.size.ToString("F0") + " " + (big.sharedMaterial ? big.sharedMaterial.shader.name : ""));
            }
            System.IO.File.WriteAllText(ShowcaseCapture.Arg("-dumpOut", "Temp/old.txt"), sb.ToString());
        }
    }
}
