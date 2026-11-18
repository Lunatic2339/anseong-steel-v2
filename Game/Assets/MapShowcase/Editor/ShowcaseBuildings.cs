using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anseong.MapShowcase.Editor
{
    /// Meshy-generated buildings: FBX + PBR textures in Assets/MapShowcase/Buildings/<id>/ -> Standard material -> prefab -> replaces the blockout group.
    public static class ShowcaseBuildings
    {
        const string Root = "Assets/MapShowcase";

        class Def
        {
            public string id;            // folder / prefab name
            public string hideGroup;     // name of the blockout group under "01 Buildings and traversable ground" whose renderers are hidden
            public Vector3 basePos;      // world position of footprint centre on the ground
            public float height;         // metres, uniform scale is derived from this
            public float yaw;            // degrees so the designed front faces the players
            public float sink;
            public Vector2 fitXZ;        // optional: stretch footprint to this world size (after yaw)
            public bool shell;           // keep the blockout (interior + gameplay parts) and wrap the generated model around it as an outer shell
            public string[] keep;        // blockout children whose names contain one of these stay visible (gameplay parts)           // metres pushed into the ground to hide the generated plinth edge
        }

        static readonly Def[] Defs =
        {
            new Def{ id="B08_BlastFurnace", hideGroup="B08 Protected furnace / 90m landmark", basePos=new Vector3(99,0,714), height=92, yaw=180, sink=0.6f },
            new Def{ id="B04_SteelHall", hideGroup="B04 transport plant", basePos=new Vector3(85,0,145), height=47, yaw=270, sink=0.4f, fitXZ=new Vector2(60,90) },
            new Def{ id="B05_DamagedWorkshop", hideGroup="B05 material warehouse", basePos=new Vector3(-155,0,235), height=27, yaw=90, sink=0.3f, fitXZ=new Vector2(64,112) },
            new Def{ id="B09_GasCleaning", hideGroup="B09 cooling / pipe hall", basePos=new Vector3(-110,0,556), height=32, yaw=90, sink=0.5f, fitXZ=new Vector2(40,54) },
            new Def{ id="B01_Hangar", hideGroup="B01 Mech maintenance hangar", basePos=new Vector3(0,0,-27.5f), height=52f, yaw=270, sink=0.3f, fitXZ=new Vector2(67f,63f), shell=true },
            new Def{ id="B07_GantryCrane", hideGroup="B07 Crane 68m / underside 62m", basePos=new Vector3(90,0,555), height=69, yaw=0, sink=0.3f, fitXZ=new Vector2(108,66), keep=new[]{"coil","hoist","hook","trolley","latch","pin","cable"} },
        };

        const string B03Prefab = "Assets/Map35/ExternalReferenceCandidate/Meshy/Generated/Prefabs/B03_MeshyGenerated_WorldPlacement.prefab";

        public static void Apply()
        {
            // B03: reuse the Meshy model generated earlier (already placed in world space by its prefab)
            var b03 = AssetDatabase.LoadAssetAtPath<GameObject>(B03Prefab);
            var bparent = GameObject.Find("01 Buildings and traversable ground");
            if (b03 != null && bparent != null)
            {
                var oldB03 = bparent.transform.Cast<Transform>().FirstOrDefault(t => t.name == "B03 control room");
                if (oldB03 != null) foreach (var r in oldB03.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var i3 = (GameObject)PrefabUtility.InstantiatePrefab(b03);
                var bb = Bounds(i3); Debug.Log("SHOWCASE B03 existing Meshy prefab bounds c " + bb.center.ToString("F1") + " s " + bb.size.ToString("F1"));
            }
            placed.Clear(); oldBoxes.Clear(); oldRoots.Clear();
            foreach (var d in Defs)
            {
                var prefab = BuildPrefab(d); if (prefab == null) continue;
                var parent = GameObject.Find("01 Buildings and traversable ground");
                var old = parent != null ? parent.transform.Cast<Transform>().FirstOrDefault(t => t.name == d.hideGroup) : null;
                Bounds shellInner = new Bounds();
                if (d.shell && old != null) { var srs = old.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.bounds.size.y > 0.5f).ToArray(); shellInner = srs[0].bounds; foreach (var r in srs) if (r.bounds.size.y > 20) shellInner.Encapsulate(r.bounds); old = null; }
                if (old != null) { var ors = old.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.bounds.size.y > 0.5f).ToArray(); if (ors.Length > 0) { var ob = ors[0].bounds; foreach (var r in ors) ob.Encapsulate(r.bounds); ob.Expand(6f); oldBoxes.Add(ob); oldRoots.Add(old); } }
                if (old != null) foreach (var l in old.GetComponentsInChildren<Light>(true)) l.enabled = false;
                if (old != null) foreach (var r in old.GetComponentsInChildren<Renderer>(true)) { var nm = r.name.ToLowerInvariant() + "/" + (r.transform.parent ? r.transform.parent.name.ToLowerInvariant() : ""); if (d.keep != null && d.keep.Any(k => nm.Contains(k))) continue; r.enabled = false; }
                else if (!d.shell) Debug.LogWarning("SHOWCASE no blockout group " + d.hideGroup);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.position = d.basePos + Vector3.down * d.sink;
                inst.transform.rotation = Quaternion.Euler(0, YawOverride(d), 0);
                if (d.fitXZ != Vector2.zero)
                {
                    var fb = Bounds(inst); var ls = inst.transform.localScale; bool swap = Mathf.Abs(Mathf.DeltaAngle(inst.transform.eulerAngles.y % 180, 90)) < 45;
                    float sx = d.fitXZ.x / fb.size.x, sz = d.fitXZ.y / fb.size.z;
                    inst.transform.localScale = swap ? new Vector3(ls.x * sz, ls.y, ls.z * sx) : new Vector3(ls.x * sx, ls.y, ls.z * sz);
                    Debug.Log("SHOWCASE fit " + d.id + " sx " + sx.ToString("F2") + " sz " + sz.ToString("F2"));
                }
                if (d.shell) CullInside(inst, shellInner, d.id);
                var b = Bounds(inst);
                Debug.Log("SHOWCASE building " + d.id + " bounds c " + b.center.ToString("F1") + " s " + b.size.ToString("F1"));
                placed.Add(b);
                if (d.keep == null && !d.shell) { var sh = new GameObject("SC_Shadow_" + d.id); sh.transform.position = new Vector3(b.center.x, 0.05f, b.center.z); ShowcaseBuild.ContactShadow(sh.transform, Vector3.zero, b.size.x * 1.12f + 10, b.size.z * 1.12f + 10, 0.55f); }
            }
            ReuseForBackground(bparent);
            // lamps that used to light a blockout door now light bare ground: switch off local lights sitting inside a replaced footprint
            // signs, brackets and lamps that were mounted on a blockout wall now float in front of the new model: hide small leftovers inside the old volume
            int lightsOff = 0, bitsOff = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional || !l.enabled) continue;
                if (oldBoxes.Any(b => b.Contains(l.transform.position))) { l.enabled = false; lightsOff++; }
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform.root.name.StartsWith("SC_") || r.transform.root.name.StartsWith("B0")) continue;
                if (oldRoots.Any(o => r.transform.IsChildOf(o))) continue;
                var rb = r.bounds; if (rb.center.y < 1.2f || Mathf.Max(rb.size.x, rb.size.y, rb.size.z) > 10f) continue;
                if (oldBoxes.Any(b => b.Contains(rb.center))) { r.enabled = false; bitsOff++; }
            }
            Debug.Log("SHOWCASE leftovers hidden: lights " + lightsOff + " small parts " + bitsOff);
        }

        static readonly System.Collections.Generic.List<Bounds> placed = new System.Collections.Generic.List<Bounds>();
        static readonly System.Collections.Generic.List<Bounds> oldBoxes = new System.Collections.Generic.List<Bounds>();
        static readonly System.Collections.Generic.List<Transform> oldRoots = new System.Collections.Generic.List<Transform>();

        // Background district blockouts and B06 are single flat-colour boxes: stand in the generated prefabs, stretched to the same footprint.
        static void ReuseForBackground(GameObject bparent)
        {
            var targets = new System.Collections.Generic.List<Renderer[]>();
            var districts = GameObject.Find("10 Reused industrial districts");
            if (districts != null) foreach (Transform t in districts.transform) if (t.name.StartsWith("WEST district") || t.name.StartsWith("EAST district")) targets.Add(t.GetComponentsInChildren<Renderer>());
            var b06 = bparent != null ? bparent.transform.Cast<Transform>().FirstOrDefault(t => t.name == "B06 optional tank") : null;
            if (b06 != null) targets.Add(b06.GetComponentsInChildren<Renderer>());
            var root = new GameObject("SC_BackgroundBuildings"); int i = 0;
            foreach (var rs in targets)
            {
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float L = Mathf.Max(b.size.x, b.size.z);
                string id = b.size.y <= 22 ? "B05_DamagedWorkshop" : (b.size.y <= 40 && L < 60 ? "B09_GasCleaning" : "B04_SteelHall");
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + id + ".prefab"); if (pf == null) continue;
                foreach (var r in rs) r.enabled = false;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                var nb = Bounds(inst);
                bool rot = (nb.size.x > nb.size.z) != (b.size.x > b.size.z);
                float yaw = (rot ? 90 : 0) + (i % 2 == 0 ? 0 : 180); i++;
                float tx = rot ? b.size.z : b.size.x, tz = rot ? b.size.x : b.size.z;
                inst.transform.localScale = new Vector3(tx / nb.size.x, b.size.y / nb.size.y, tz / nb.size.z);
                inst.transform.rotation = Quaternion.Euler(0, yaw, 0);
                inst.transform.position = new Vector3(b.center.x, -0.3f, b.center.z);
                { var sh = new GameObject("SC_Shadow"); sh.transform.SetParent(root.transform); sh.transform.position = new Vector3(b.center.x, 0.05f, b.center.z); ShowcaseBuild.ContactShadow(sh.transform, Vector3.zero, b.size.x * 1.12f + 10, b.size.z * 1.12f + 10, 0.5f); }
                Debug.Log("SHOWCASE background " + rs[0].name + " -> " + id + " s " + b.size.ToString("F0"));
            }
        }

        // Removes generated triangles that fall inside the kept blockout volume (generated interior, door infill) so only the outer skin remains.
        static void CullInside(GameObject inst, Bounds inner, string id)
        {
            inner.Expand(new Vector3(0.5f, 0.4f, 0.5f));
            int n = 0;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh; var m = Object.Instantiate(src); m.name = id + "_Shell";
                var v = src.vertices; var t = src.triangles; var keep = new System.Collections.Generic.List<int>(t.Length); var l2w = mf.transform.localToWorldMatrix;
                int removed = 0;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var c = l2w.MultiplyPoint3x4((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f);
                    bool inside = inner.Contains(c);
                    // doorway: anything in front of the kept opening (24 m wide, 42 m tall) up to 4 m out
                    bool door = Mathf.Abs(c.x - inner.center.x) < 12.2f && c.y < 37.5f && c.z > inner.max.z - 0.5f && c.z < inner.max.z + 7f;
                    if (inside || door) { removed++; continue; }
                    keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]);
                }
                m.triangles = keep.ToArray(); m.RecalculateBounds();
                var mp = Root + "/Meshes/" + id + "_Shell" + (n == 0 ? "" : n.ToString()) + ".asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(m, mp);
                mf.sharedMesh = m; n++;
                Debug.Log("SHOWCASE shell " + id + " removed tris " + removed + " kept " + keep.Count / 3 + " inner " + inner.center.ToString("F1") + " " + inner.size.ToString("F1"));
            }
        }

        static float YawOverride(Def d) { var s = ShowcaseCapture.Arg("-yaw:" + d.id, null); return s == null ? d.yaw : float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static Texture2D Tex(string path, bool normal, bool srgb, int max)
        {
            if (!File.Exists(path)) return null;
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; imp.sRGBTexture = srgb;
            imp.maxTextureSize = max; imp.anisoLevel = 8; imp.mipmapEnabled = true; imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static GameObject BuildPrefab(Def d) { return BuildPrefab(d.id, Root + "/Buildings/" + d.id, d.height, 8192, false); }

        public static GameObject BuildPrefab(string id, string dir, float height, int albedoMax, bool alignLongZ)
        {
            var d = new Def { id = id, height = height };
            var fbx = dir + "/model.fbx";
            if (!File.Exists(fbx)) { Debug.LogWarning("SHOWCASE missing " + fbx); return null; }
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk; mi.isReadable = id.StartsWith("B01");
            mi.meshCompression = ModelImporterMeshCompression.Off; mi.SaveAndReimport();

            var matPath = dir + "/" + d.id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_MainTex", Tex(dir + "/base_color.png", false, true, albedoMax));
            var n = Tex(dir + "/normal.png", true, false, Mathf.Min(4096, albedoMax)); if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            var ms = Tex(dir + "/metallic_smoothness.png", false, false, Mathf.Min(4096, albedoMax));
            if (ms) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICGLOSSMAP"); m.SetFloat("_GlossMapScale", 0.85f); }
            else { m.SetFloat("_Metallic", 0.2f); m.SetFloat("_Glossiness", 0.3f); }
            m.color = Color.white; EditorUtility.SetDirty(m);

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (src == null || src.GetComponentsInChildren<Renderer>().Length == 0) { Debug.LogWarning("SHOWCASE unusable model " + fbx); return null; }
            var root = new GameObject(d.id);
            // unlinked copy with the meshes saved as their own assets: the Meshy FBX embeds ~100 MB of textures and is kept out of version control
            var model = Object.Instantiate(src); model.name = "model"; model.transform.SetParent(root.transform, false);
            int mi2 = 0; foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            { var copy = Object.Instantiate(mf.sharedMesh); copy.name = id + "_Mesh" + mi2; var cp = Root + "/Meshes/" + id + "_Mesh" + mi2 + ".asset"; AssetDatabase.DeleteAsset(cp); AssetDatabase.CreateAsset(copy, cp); mf.sharedMesh = copy; mi2++; }
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = r.sharedMaterials.Select(_ => m).ToArray(); r.shadowCastingMode = ShadowCastingMode.On; }
            // normalise: uniform scale to target height, footprint centre at root origin, lowest point at y=0
            var b = Bounds(root);
            if (alignLongZ && b.size.x > b.size.z) { model.transform.Rotate(0, 90, 0, Space.World); b = Bounds(root); }
            float s = d.height / b.size.y; model.transform.localScale *= s;
            b = Bounds(root);
            model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            // not lightmap-static: the scene has baked GI only for the old Z1 meshes, and a static renderer without a lightmap gets no ambient at all (black shaded sides)
            foreach (var r in root.GetComponentsInChildren<Renderer>()) { r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; var mr2 = r as MeshRenderer; if (mr2 != null) mr2.receiveGI = ReceiveGI.LightProbes; }
            var pp = Root + "/Prefabs/" + d.id + ".prefab"; AssetDatabase.DeleteAsset(pp);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, pp);
            Object.DestroyImmediate(root);
            int tris = src.GetComponentsInChildren<MeshFilter>().Sum(f => (int)(f.sharedMesh.GetIndexCount(0) / 3));
            Debug.Log("SHOWCASE prefab " + d.id + " scale " + s.ToString("F3") + " tris " + tris);
            return prefab;
        }
    }
}
