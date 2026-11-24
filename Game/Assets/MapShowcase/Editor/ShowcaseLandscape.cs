using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anseong.MapShowcase.Editor
{
    /// Far scenery: forested ridges and foothills, tree clumps between the site and the ridges, ships on the open sea.
    public static class ShowcaseLandscape
    {
        const string Root = "Assets/MapShowcase";

        static Texture2D Tex(string name, bool alphaSmooth, int max)
        {
            var p = Root + "/Textures/" + name + ".png"; var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 8; imp.maxTextureSize = max; imp.mipmapEnabled = true;
            imp.alphaSource = alphaSmooth ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        static Material ForestMat(string name, Color tint, Texture2D macro, Texture2D detail)
        {
            var path = Root + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_MainTex", macro); m.SetTextureScale("_MainTex", Vector2.one / 1200f); m.color = tint; m.SetFloat("_Metallic", 0);
            m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); m.SetFloat("_GlossMapScale", 1f);
            m.SetTexture("_DetailAlbedoMap", detail); m.SetTextureScale("_DetailAlbedoMap", Vector2.one / 110f); m.EnableKeyword("_DETAIL_MULX2");
            m.SetFloat("_SpecularHighlights", 0); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(m); return m;
        }

        const float RidgeHeight = 0.5f;   // ridges were steep alpine peaks; halved for the gentle Korean coastal hills
        static readonly List<Mesh> allMeshes = new List<Mesh>();
        static readonly List<Mesh> lowMeshes = new List<Mesh>();   // world-space meshes trees may stand on

        public static void Apply()
        {
            var macro = Tex("SC_ForestMacro", true, 4096); var detail = Tex("SC_ForestDetail", false, 512);
            var far = ForestMat("SC_Forest_FarRidge", new Color(.92f, .97f, 1f), macro, detail);
            var near = ForestMat("SC_Forest_NearRidge", Color.white, macro, detail);
            var low = ForestMat("SC_Forest_Foothill", new Color(1.08f, 1.08f, .98f), macro, detail);
            var scrub = ForestMat("SC_Forest_Scrubland", new Color(1.25f, 1.18f, .95f), macro, detail);
            var root = new GameObject("SC_Landscape"); lowMeshes.Clear(); allMeshes.Clear();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList())
            {
                if (!r.enabled) continue; var n = r.name;
                Material mat = n.Contains("far storm silhouette") || n.Contains("middle overlapping massif") ? far
                    : n.StartsWith("Geological ridge") ? near
                    : n.Contains("foothill") ? low
                    : n.Contains("hinterland") ? scrub : null;
                if (mat == null) continue;
                var src = r.GetComponent<MeshFilter>().sharedMesh; var l2w = r.transform.localToWorldMatrix;
                // weld by position so normals are smooth (the source is flat-shaded), project UV from above in world metres
                var sv = src.vertices; var st = src.triangles; var map = new Dictionary<Vector3Int, int>(); var verts = new List<Vector3>(); var remap = new int[sv.Length];
                for (int i = 0; i < sv.Length; i++)
                {
                    var w = l2w.MultiplyPoint3x4(sv[i]); if (n.StartsWith("Geological ridge")) w.y *= RidgeHeight; var key = new Vector3Int(Mathf.RoundToInt(w.x * 20), Mathf.RoundToInt(w.y * 20), Mathf.RoundToInt(w.z * 20));
                    int idx; if (!map.TryGetValue(key, out idx)) { idx = verts.Count; verts.Add(w); map[key] = idx; }
                    remap[i] = idx;
                }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "SC_Land_" + n };
                mesh.SetVertices(verts); mesh.triangles = st.Select(i => remap[i]).ToArray(); mesh.uv = verts.Select(v => new Vector2(v.x, v.z)).ToArray();
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var safe = new string(n.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Replace(' ', '_');
                var mp = Root + "/Meshes/SC_Land_" + safe + ".asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
                var go = new GameObject("SC_Land_" + safe); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.lightProbeUsage = LightProbeUsage.Off; mr.shadowCastingMode = ShadowCastingMode.Off;
                r.enabled = false;
                if (mat == low || mat == near) lowMeshes.Add(mesh);
                if (mat != scrub) allMeshes.Add(mesh);
                Debug.Log("SHOWCASE land " + n + " verts " + sv.Length + " -> " + verts.Count);
            }
            Trees(root.transform);
            Distant(root.transform);
            Ships(root.transform);
        }

        static void Trees(Transform root)
        {
            var clumps = LeafyClumps();
            var rng = new System.Random(4217); var holder = new GameObject("SC_Trees"); holder.transform.SetParent(root, false);
            // candidate triangles: gentle slopes, low altitude, within sight of the play area
            var cand = new List<Vector3[]>(); var areas = new List<float>(); float total = 0; var centre = new Vector3(0, 0, 400);
            foreach (var m in lowMeshes)
            {
                var v = m.vertices; var t = m.triangles; float crest = 1e9f;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]]; var cr = Vector3.Cross(b - a, c - a); float area = cr.magnitude * .5f; if (area < 1) continue;
                    var nrm = cr.normalized; if (Mathf.Abs(nrm.y) < 0.75f) continue;
                    var mid = (a + b + c) / 3f; if (mid.y > 70 || mid.y < 1.5f || mid.y > crest) continue;
                    var flat = mid - centre; flat.y = 0; if (flat.magnitude > 1500) continue;
                    // denser close to the site so the tree line reads from the play area
                    float wgt = area * Mathf.Lerp(3f, 0.6f, Mathf.InverseLerp(500, 1500, flat.magnitude));
                    cand.Add(new[] { a, b, c }); total += wgt; areas.Add(total);
                }
            }
            int count = 0, target = 900;
            for (int k = 0; k < target * 3 && count < target && cand.Count > 0; k++)
            {
                float pick = (float)rng.NextDouble() * total; int lo = areas.BinarySearch(pick); if (lo < 0) lo = ~lo; lo = Mathf.Min(lo, cand.Count - 1);
                var tri = cand[lo]; float u = (float)rng.NextDouble(), w = (float)rng.NextDouble(); if (u + w > 1) { u = 1 - u; w = 1 - w; }
                var p = tri[0] + (tri[1] - tri[0]) * u + (tri[2] - tri[0]) * w;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(clumps[count % clumps.Length], holder.transform);
                float s = 0.85f + (float)rng.NextDouble() * 0.5f;
                go.transform.position = p + Vector3.down * 0.8f; go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                go.transform.localScale = new Vector3(s * (0.9f + (float)rng.NextDouble() * .3f), s, s * (0.9f + (float)rng.NextDouble() * .3f));
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                count++;
            }
            Debug.Log("SHOWCASE trees placed " + count + " candidates " + cand.Count);
        }

        // Leafy tree clumps: crossed cut-out cards from one generated tree sheet (the Meshy clump came out as bare trunks).
        static GameObject[] LeafyClumps()
        {
            var tp = Root + "/Textures/SC_TreeAtlas.png"; var imp = (TextureImporter)AssetImporter.GetAtPath(tp);
            imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true; imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.4f; imp.maxTextureSize = 2048; imp.anisoLevel = 4; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.SaveAndReimport();
            var mp = Root + "/Materials/SC_TreeCards.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, mp); }
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); mat.color = new Color(.82f, .86f, .80f);
            mat.SetFloat("_Mode", 1); mat.SetFloat("_Cutoff", 0.4f); mat.EnableKeyword("_ALPHATEST_ON"); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = 2450;
            mat.SetFloat("_Glossiness", 0.05f); mat.SetFloat("_Metallic", 0); mat.SetFloat("_SpecularHighlights", 0); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); mat.SetFloat("_GlossyReflections", 0); mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            mat.enableInstancing = true; EditorUtility.SetDirty(mat);
            // atlas rectangles (u0, v0, u1, v1) and tree heights in metres: pine, oak, tall pine, broadleaf
            var rects = new[] { new Vector4(.003f, .077f, .251f, .757f), new Vector4(.240f, .077f, .5065f, .690f), new Vector4(.5065f, .077f, .713f, .908f), new Vector4(.7135f, .077f, .997f, .752f) };
            var heights = new[] { 17f, 13f, 21f, 15f }; var aspect = new[] { .55f, .65f, .37f, .63f };
            var result = new GameObject[3];
            for (int c = 0; c < 3; c++)
            {
                var rng = new System.Random(77 + c * 13); var v = new List<Vector3>(); var uv = new List<Vector2>(); var nrm = new List<Vector3>(); var tri = new List<int>();
                int trees = 9 + c * 2;
                for (int i = 0; i < trees; i++)
                {
                    int k = rng.Next(10) < 6 ? (rng.Next(2) * 2) : (1 + rng.Next(2) * 2);   // pines a little more common
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2, rad = Mathf.Sqrt((float)rng.NextDouble()) * 13f; var basePos = new Vector3(Mathf.Cos(ang) * rad, 0, Mathf.Sin(ang) * rad);
                    float h = heights[k] * (0.8f + (float)rng.NextDouble() * .45f), w = h * aspect[k]; float yaw = (float)rng.NextDouble() * Mathf.PI; var r = rects[k];
                    for (int q = 0; q < 2; q++)
                    {
                        float a = yaw + q * Mathf.PI * .5f; var side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (w * .5f);
                        for (int f = 0; f < 2; f++)   // both windings: the Standard shader culls back faces
                        {
                            int o = v.Count; var s = f == 0 ? side : -side; float u0 = f == 0 ? r.x : r.z, u1 = f == 0 ? r.z : r.x;
                            v.Add(basePos - s); v.Add(basePos + s); v.Add(basePos + s + Vector3.up * h); v.Add(basePos - s + Vector3.up * h);
                            uv.Add(new Vector2(u0, r.y)); uv.Add(new Vector2(u1, r.y)); uv.Add(new Vector2(u1, r.w)); uv.Add(new Vector2(u0, r.w));
                            for (int n = 0; n < 4; n++) nrm.Add(Vector3.up);   // upward normals: even overcast shading, no card flicker
                            tri.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
                        }
                    }
                }
                var mesh = new Mesh { name = "SC_TreeClump_" + c }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(nrm); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
                var meshPath = Root + "/Meshes/SC_TreeClump_" + c + ".asset"; AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh, meshPath);
                var go = new GameObject("Prop_LeafyClump_" + c); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                result[c] = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Prop_LeafyClump_" + c + ".prefab"); Object.DestroyImmediate(go);
            }
            return result;
        }

        static float HeightAt(float x, float z)
        {
            float best = float.NegativeInfinity; var p = new Vector2(x, z);
            foreach (var m in allMeshes)
            {
                var b = m.bounds; if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z) continue;
                var v = m.vertices; var t = m.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], bb = v[t[i + 1]], c = v[t[i + 2]];
                    var v0 = new Vector2(bb.x - a.x, bb.z - a.z); var v1 = new Vector2(c.x - a.x, c.z - a.z); var v2 = p - new Vector2(a.x, a.z);
                    float den = v0.x * v1.y - v1.x * v0.y; if (Mathf.Abs(den) < 1e-6f) continue;
                    float u = (v2.x * v1.y - v1.x * v2.y) / den, w = (v0.x * v2.y - v2.x * v0.y) / den; if (u < 0 || w < 0 || u + w > 1) continue;
                    best = Mathf.Max(best, a.y + (bb.y - a.y) * u + (c.y - a.y) * w);
                }
            }
            return float.IsNegativeInfinity(best) ? 0f : best;
        }

        // A second line of works beyond the site: sheds, tank farms and stacks on the low ground in front of the hills, faded by the fog.
        static void Distant(Transform root)
        {
            var holder = new GameObject("SC_DistantWorks"); holder.transform.SetParent(root, false);
            var ids = new[] { "B04_SteelHall", "B05_DamagedWorkshop", "B09_GasCleaning", "B04_SteelHall", "B05_DamagedWorkshop" };
            var stack = ShowcaseBuildings.BuildPrefab("Prop_Chimney", Root + "/Props/Prop_Chimney", 64f, 2048, false) ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LegacySteelworks/Environment/Assets/Smokestack/Smokestack.prefab");
            // the three stacked-cylinder chimneys west of the yard become the generated chimney
            if (stack != null && stack.name == "Prop_Chimney")
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name.StartsWith("West damaged chimney"))
                    {
                        var rs0 = t.GetComponentsInChildren<Renderer>(); if (rs0.Length == 0) continue; var b0 = rs0[0].bounds; foreach (var r in rs0) { b0.Encapsulate(r.bounds); r.enabled = false; }
                        var c = (GameObject)PrefabUtility.InstantiatePrefab(stack, holder.transform); float cz = t.name.EndsWith("0") ? 160f : t.name.EndsWith("1") ? 370f : 580f; b0.center = new Vector3(-220f, 0, cz);   /* blockout positions; the hidden shells report stale bounds */ c.transform.position = new Vector3(b0.center.x, -0.4f, b0.center.z); c.transform.rotation = Quaternion.Euler(0, (b0.center.z * 37f) % 360f, 0);
                        var sh = new GameObject("shadow"); sh.transform.SetParent(c.transform, false); ShowcaseBuild.ContactShadow(sh.transform, new Vector3(0, 0.43f, 0), 16f, 16f, 0.6f);
                    }
            var rng = new System.Random(911); int n = 0, st = 0;
            // (x, z) anchors: north-west and north behind the furnace, west behind the rolling mill, far north-east shore
            var anchors = new[] { new Vector2(-380, 980), new Vector2(-180, 1010), new Vector2(40, 1000), new Vector2(260, 990), new Vector2(-560, 900), new Vector2(-640, 620), new Vector2(-650, 330), new Vector2(-640, 60),
                                  new Vector2(-300, 1180), new Vector2(-60, 1220), new Vector2(190, 1200), new Vector2(-760, 780), new Vector2(-800, 450), new Vector2(-520, 1120), new Vector2(380, 1150), new Vector2(-780, 150) };
            foreach (var a in anchors)
            {
                // pushed 1.35x out from the site centre so even the nearest row sits in the haze instead of reading as crisp as the play area
                var far = new Vector2(0, 400) + (a - new Vector2(0, 400)) * 1.35f;
                float x = far.x + (float)(rng.NextDouble() - .5) * 60, z = far.y + (float)(rng.NextDouble() - .5) * 60; float y = HeightAt(x, z);
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + ids[n % ids.Length] + ".prefab"); if (pf == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder.transform); float s = 0.9f + (float)rng.NextDouble() * 0.7f;
                go.transform.position = new Vector3(x, y - 2.5f, z); go.transform.rotation = Quaternion.Euler(0, rng.Next(4) * 90 + (float)(rng.NextDouble() - .5) * 16, 0); go.transform.localScale = new Vector3(s, s * (0.8f + (float)rng.NextDouble() * .4f), s);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                n++;
                if (stack != null && n % 2 == 0)
                {
                    float sx = x + 70 + (float)rng.NextDouble() * 40, sz = z + (float)(rng.NextDouble() - .5) * 80;
                    var sg = (GameObject)PrefabUtility.InstantiatePrefab(stack, holder.transform); sg.transform.position = new Vector3(sx, HeightAt(sx, sz) - 1.5f, sz);
                    var rs = sg.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    float k = (85f + (float)rng.NextDouble() * 50f) / Mathf.Max(1f, b.size.y); sg.transform.localScale *= k; st++;
                    foreach (var r in rs) r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            Debug.Log("SHOWCASE distant works " + n + " stacks " + st);
        }

        static void Ships(Transform root)
        {
            var pf = ShowcaseBuildings.BuildPrefab("Prop_CargoShip", Root + "/Props/Prop_CargoShip", 42f, 2048, true); if (pf == null) return;
            var holder = new GameObject("SC_Ships"); holder.transform.SetParent(root, false);
            var spots = new[] { new Vector4(820, 140, 8, 1f), new Vector4(1250, 660, 196, 1.1f), new Vector4(1900, 1150, 25, 1.2f), new Vector4(1000, 1050, -18, 0.9f), new Vector4(2600, 300, 170, 1.2f) };
            foreach (var s in spots)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder.transform);
                go.transform.position = new Vector3(s.x, -9f * s.w, s.y); go.transform.rotation = Quaternion.Euler(0, s.z, 0); go.transform.localScale = Vector3.one * s.w;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var tb = Object.Instantiate(pf); var rs = tb.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); Object.DestroyImmediate(tb);
            Debug.Log("SHOWCASE ships placed " + spots.Length + " size " + b.size.ToString("F0"));
        }
    }
}
