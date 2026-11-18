using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string B02ArchFolder = "Assets/Map35/B02Architecture/Meshes";
        sealed class B02ArchMesh
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Vector4> panel = new List<Vector4>();
            public readonly List<Color> colors = new List<Color>();
            public Color seed;
            public B02ArchMesh(string key, float exposure = 1)
            {
                uint h = 2166136261; foreach (char c in key) h = (h ^ c) * 16777619;
                seed = new Color((h & 65535) / 65535f, ((h >> 16) & 65535) / 65535f, exposure, 1);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
                Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, float width, float height)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0)
                { var p = b; b = d; d = p; var t = ub; ub = ud; ud = t; }
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d }); uv.AddRange(new[] { ua, ub, uc, ud });
                foreach (var p in new[] { ua, ub, uc, ud })
                { panel.Add(new Vector4(p.x / Mathf.Max(width, .001f), p.y / Mathf.Max(height, .001f), width, height)); colors.Add(seed); }
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                float width = Vector3.Distance(a, b), height = Vector3.Distance(a, d);
                Quad(a, b, c, d, normal, Vector2.zero, new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height), width, height);
            }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal,
                Vector2 ua, Vector2 ub, Vector2 uc, float width, float height)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0)
                { var p = b; b = c; c = p; var t = ub; ub = uc; uc = t; }
                int i = vertices.Count; vertices.AddRange(new[] { a, b, c }); uv.AddRange(new[] { ua, ub, uc });
                foreach (var p in new[] { ua, ub, uc })
                { panel.Add(new Vector4(p.x / width, p.y / height, width, height)); colors.Add(seed); }
                triangles.AddRange(new[] { i, i + 1, i + 2 });
            }
            public void Box(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 size)
            {
                Vector3 x = right * size.x * .5f, y = up * size.y * .5f, z = forward * size.z * .5f;
                Face(center - x - y + z, center + x - y + z, center + x + y + z, center - x + y + z, forward);
                Face(center + x - y - z, center - x - y - z, center - x + y - z, center + x + y - z, -forward);
                Face(center - x + y + z, center + x + y + z, center + x + y - z, center - x + y - z, up);
                Face(center - x - y - z, center + x - y - z, center + x - y + z, center - x - y + z, -up);
                Face(center + x - y + z, center + x - y - z, center + x + y - z, center + x + y + z, right);
                Face(center - x - y - z, center - x - y + z, center - x + y + z, center - x + y - z, -right);
            }
        }

        // Called only by the root's capture/save workflow. No scene, source asset, lighting,
        // material or collider changes are performed outside the explicitly owned additions.
        static void BuildB02Architecture()
        {
            var b02 = Existing("B02");
            if (b02 == null) throw new InvalidOperationException("B02 not loaded.");
            if (b02.Find("B02 architectural depth upgrade") != null)
                throw new InvalidOperationException("B02 architecture already applied; start from its saved baseline.");
            var original = b02.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => !r.transform.GetComponentsInParent<Transform>().Any(t => t.name == "LOD1 combined structure"))
                .ToArray();
            Func<string, MeshRenderer> find = n => original.FirstOrDefault(r => r.name == n)
                ?? throw new InvalidOperationException("Missing B02 source: " + n);
            var steel = original.First(r => r.name == "remaining shallow steel eave gutter").sharedMaterial;
            var root = ArchitectureGroup("B02 architectural depth upgrade", b02);
            var report = new StringBuilder("source,operation,min_x,min_y,min_z,max_x,max_y,max_z,original_collider_count\n");
            Directory.CreateDirectory(B02ArchFolder); AssetDatabase.Refresh();
            var bays = new[] {
                new { id = "E03", prefix = "SideWall_1_3_", normal = Vector3.right },
                new { id = "S01", prefix = "EndWall_0_1_", normal = Vector3.back },
                new { id = "W01", prefix = "SideWall_0_1_", normal = Vector3.left }
            };
            foreach (var bay in bays)
            {
                var fixings = new B02ArchMesh(bay.id + " fixings");
                Bounds lower = default;
                for (int row = 0; row < 2; row++)
                {
                    var source = find(bay.prefix + row); var bounds = source.bounds;
                    // West legacy chunks contain overhanging ribs in the next wall bay. That
                    // neighbour remains solid; use the actual nominal 7.98m sheet span there.
                    if (bay.id == "W01") { bounds.center = new Vector3(bounds.center.x, bounds.center.y, source.transform.position.z); bounds.size = new Vector3(bounds.size.x, bounds.size.y, 7.98f); }
                    if (row == 0) lower = bounds;
                    Vector3 u = Vector3.Cross(Vector3.up, bay.normal);
                    bool side = Mathf.Abs(bay.normal.x) > .5f;
                    float width = side ? bounds.size.z : bounds.size.x;
                    float thickness = side ? bounds.size.x : bounds.size.z;
                    Vector3 origin = bounds.center + bay.normal * (thickness * .5f);
                    origin.y = bounds.min.y;
                    B02ArchCladding(bay.id + " row " + row, origin, u, bay.normal, width, bounds.size.y,
                        thickness, source.sharedMaterial, steel, root, fixings);
                    source.enabled = false; // Preserve its GameObject, mesh asset and exact original collider.
                    report.AppendLine(B02ArchReport(source, "renderer disabled; full solid replaced visually"));
                }
                var opening = find(bay.prefix + "2");
                if (opening.gameObject.activeInHierarchy || opening.GetComponent<Collider>() != null)
                    throw new InvalidOperationException("Expected an already empty uncollided glazing bay: " + opening.name);
                B02ArchWindow(bay.id, opening, lower, bay.normal, find(bay.prefix + "1").sharedMaterial, steel, root, fixings);
                report.AppendLine(B02ArchReport(opening, "existing empty upper bay receives recessed window assembly"));
                B02ArchSave(fixings, "B02 panel fixing " + bay.id, steel, root);
            }
            foreach (string name in new[] { "RoofPanel_2_0", "RoofPanel_2_1" })
                B02ArchRoofEdge(find(name), steel, root);
            // The true southwest building corner closes the perpendicular retained wall faces.
            var corner = new B02ArchMesh("southwest corner", .9f);
            corner.Box(new Vector3(-119.31f, 12.96f, 29.38f), Vector3.forward, Vector3.up, Vector3.right, new Vector3(.30f, 23.8f, .014f));
            corner.Box(new Vector3(-119.16f, 12.96f, 29.23f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(.30f, 23.8f, .014f));
            B02ArchSave(corner, "B02 corner flashing southwest", steel, root);
            string output = Path.GetFullPath("../docs/validation/b02-building-detail-2026-11-15");
            Directory.CreateDirectory(output); File.WriteAllText(output + "/architecture-source-changes.csv", report.ToString());
            File.WriteAllText(output + "/architecture-geometry.txt",
                "Six original solid renderers disabled; original MeshColliders and source assets retained.\n"
                + "Three windows occupy previously inactive glass bays with no source colliders; no false opening over a solid wall.\n"
                + "Closed corrugated sheets: nominal 1.15m width, 0.18m rib pitch, 24mm profile, 6mm metal thickness.\n"
                + "Physical fixing locations: panel UV U=.12/.88, V=.04/.50/.96; backing girts at matching heights.\n"
                + "New meshes: metre UV0, normalized panel coordinates/dimensions UV3, stable panel seed COLOR.rg, exposure COLOR.b.\n"
                + "New colliders=0; upper glazing/frame additions are visual environment geometry, not a new traversal controller.\n"
                + "Original roof retained; two broken roof boundaries receive closed folded rake caps and seated support brackets.\n"
                + "New renderers=" + root.GetComponentsInChildren<MeshRenderer>().Length + "\n");
            Debug.Log("B02_ARCHITECTURE_READY original_renderers_disabled=6 original_colliders_preserved=True new_colliders=0");
        }

        static string B02ArchReport(MeshRenderer r, string operation)
        {
            Bounds b = r.bounds;
            return r.name + "," + operation + "," + RepVec(b.min) + "," + RepVec(b.max) + "," + r.GetComponents<Collider>().Length;
        }

        static void B02ArchCladding(string id, Vector3 origin, Vector3 u, Vector3 normal, float width, float height,
            float wallDepth, Material skin, Material steel, Transform root, B02ArchMesh fixings)
        {
            int sheets = Mathf.CeilToInt(width / 1.15f); float step = width / sheets;
            var backing = new B02ArchMesh(id + " backings", .3f);
            for (int i = 0; i < sheets; i++)
            {
                float x0 = -width * .5f + i * step + .004f, sheetWidth = step - .008f;
                var data = new B02ArchMesh(id + " sheet " + i);
                B02ArchCorrugated(data, origin + u * x0, u, Vector3.up, normal, sheetWidth, height, .018f);
                B02ArchSave(data, "B02 facade panel " + id + " sheet " + i.ToString("00"), skin, root);
                foreach (float fu in new[] { .12f, .88f }) foreach (float fv in new[] { .04f, .50f, .96f })
                {
                    float localU = sheetWidth * fu;
                    float depth = .018f + B02ArchRib(localU) + .009f;
                    B02ArchBolt(fixings, origin + u * (x0 + localU) + Vector3.up * (height * fv) + normal * depth, u, normal);
                }
                if (i > 0) backing.Box(origin + u * (x0 - .004f) + Vector3.up * (height * .5f) - normal * .015f,
                    u, Vector3.up, normal, new Vector3(.09f, height, .024f));
            }
            foreach (float fv in new[] { .04f, .50f, .96f })
                backing.Box(origin + Vector3.up * (height * fv) - normal * (wallDepth * .55f), u, Vector3.up, normal, new Vector3(width, .14f, .13f));
            // A real inside liner and closed perimeter returns explain the old collider thickness.
            var liner = new B02ArchMesh(id + " inside", .25f);
            liner.Box(origin + Vector3.up * (height * .5f) - normal * (wallDepth + .007f), u, Vector3.up, normal, new Vector3(width, height, .014f));
            B02ArchSave(liner, "B02 inner liner " + id, skin, root);
            foreach (float x in new[] { -width * .5f, width * .5f })
                backing.Box(origin + u * x + Vector3.up * (height * .5f) - normal * (wallDepth * .5f), u, Vector3.up, normal, new Vector3(.014f, height, wallDepth + .04f));
            foreach (float y in new[] { .006f, height - .006f })
                backing.Box(origin + Vector3.up * y - normal * (wallDepth * .5f), u, Vector3.up, normal, new Vector3(width, .012f, wallDepth + .04f));
            B02ArchSave(backing, "B02 reveal " + id + " closed wall returns and girts", steel, root);
        }

        static float B02ArchRib(float metre)
        {
            float t = Mathf.Repeat(metre, .18f) / .18f;
            if (t < .32f || t > .82f) return 0;
            if (t < .42f) return .024f * (t - .32f) / .10f;
            if (t <= .72f) return .024f;
            return .024f * (.82f - t) / .10f;
        }

        static void B02ArchCorrugated(B02ArchMesh data, Vector3 origin, Vector3 u, Vector3 v, Vector3 normal,
            float width, float height, float baseDepth)
        {
            var points = new SortedSet<float> { 0, width };
            for (int cycle = 0; cycle * .18f < width; cycle++)
            foreach (float f in new[] { 0f, .32f, .42f, .72f, .82f, 1f })
            { float x = (cycle + f) * .18f; if (x > 0 && x < width) points.Add(x); }
            var xs = points.ToArray();
            Func<float, float, float, Vector3> p = (x, y, back) => origin + u * x + v * y + normal * (baseDepth + B02ArchRib(x) - back);
            for (int j = 0; j < xs.Length - 1; j++)
            {
                float a = xs[j], b = xs[j + 1];
                var ua = new Vector2(a, 0); var ub = new Vector2(b, 0); var uc = new Vector2(b, height); var ud = new Vector2(a, height);
                data.Quad(p(a, 0, 0), p(b, 0, 0), p(b, height, 0), p(a, height, 0), normal, ua, ub, uc, ud, width, height);
                data.Quad(p(a, 0, .006f), p(b, 0, .006f), p(b, height, .006f), p(a, height, .006f), -normal, ua, ub, uc, ud, width, height);
                data.Face(p(a, height, 0), p(b, height, 0), p(b, height, .006f), p(a, height, .006f), v);
                data.Face(p(a, 0, .006f), p(b, 0, .006f), p(b, 0, 0), p(a, 0, 0), -v);
            }
            data.Face(p(0, 0, 0), p(0, height, 0), p(0, height, .006f), p(0, 0, .006f), -u);
            data.Face(p(width, 0, .006f), p(width, height, .006f), p(width, height, 0), p(width, 0, 0), u);
        }

        static void B02ArchWindow(string id, MeshRenderer oldOpening, Bounds lower, Vector3 normal,
            Material skin, Material steel, Transform root, B02ArchMesh fixings)
        {
            Vector3 u = Vector3.Cross(Vector3.up, normal); bool side = Mathf.Abs(normal.x) > .5f;
            float width = side ? lower.size.z : lower.size.x;
            float depth = side ? lower.size.x : lower.size.z;
            Vector3 origin = lower.center + normal * (depth * .5f); origin.y = 17.01f;
            const float sill = 18.6f, head = 21.4f, upper = 24.96f;
            B02ArchCladding(id + " window knee", origin, u, normal, width, sill - origin.y, depth, skin, steel, root, fixings);
            Vector3 highOrigin = origin; highOrigin.y = head;
            B02ArchCladding(id + " window head", highOrigin, u, normal, width, upper - head, depth, skin, steel, root, fixings);
            var reveal = new B02ArchMesh(id + " window reveals", .65f);
            float bottom = sill + .08f, top = head - .08f, usableWidth = width - .34f;
            foreach (float x in new[] { -width * .5f + .08f, width * .5f - .08f })
                reveal.Box(new Vector3(origin.x, (sill + head) * .5f, origin.z) + u * x - normal * (depth * .5f),
                    u, Vector3.up, normal, new Vector3(.16f, head - sill, depth + .10f));
            foreach (float y in new[] { sill + .04f, head - .04f })
                reveal.Box(new Vector3(origin.x, y, origin.z) - normal * (depth * .5f), u, Vector3.up, normal, new Vector3(width, .08f, depth + .18f));
            // The sill has an outboard drip, and the hood projects from actual wall returns.
            reveal.Box(new Vector3(origin.x, sill - .04f, origin.z) + normal * .10f, u, Vector3.up, normal, new Vector3(width + .08f, .08f, .18f));
            reveal.Box(new Vector3(origin.x, head + .06f, origin.z) + normal * .11f, u, Vector3.up, normal, new Vector3(width + .12f, .045f, .32f));
            B02ArchSave(reveal, "B02 reveal " + id + " recessed window jamb sill and hood", steel, root);
            var frame = new B02ArchMesh(id + " window frame", .55f);
            float recess = Mathf.Min(.38f, depth * .65f); int bays = Mathf.Max(3, Mathf.CeilToInt(usableWidth / 1.55f));
            float pitch = usableWidth / bays;
            for (int i = 0; i <= bays; i++)
                frame.Box(new Vector3(origin.x, (bottom + top) * .5f, origin.z) + u * (-usableWidth * .5f + i * pitch) - normal * recess,
                    u, Vector3.up, normal, new Vector3(.075f, top - bottom, .14f));
            foreach (float y in new[] { bottom, top, Mathf.Lerp(bottom, top, .52f) })
                frame.Box(new Vector3(origin.x, y, origin.z) - normal * recess, u, Vector3.up, normal, new Vector3(usableWidth, .075f, .14f));
            B02ArchSave(frame, "B02 window frame " + id, steel, root);
            var glass = B02Material("glass");
            for (int i = 0; i < bays; i++) for (int row = 0; row < 2; row++)
            {
                // One missing upper light retains a readable damaged building, with no solid
                // backing behind the hole. Remaining panes are 6mm glass, recessed >0.3m.
                if (i == bays / 2 && row == 1) continue;
                float y0 = row == 0 ? bottom : Mathf.Lerp(bottom, top, .52f);
                float y1 = row == 0 ? Mathf.Lerp(bottom, top, .52f) : top;
                var pane = new B02ArchMesh(id + " glazing " + i + " " + row, .4f);
                pane.Box(new Vector3(origin.x, (y0 + y1) * .5f, origin.z) + u * (-usableWidth * .5f + (i + .5f) * pitch) - normal * (recess + .035f),
                    u, Vector3.up, normal, new Vector3(pitch - .09f, y1 - y0 - .09f, .006f));
                B02ArchSave(pane, "B02 glazing " + id + " pane " + i + " " + row, glass, root);
            }
        }

        static void B02ArchRoofEdge(MeshRenderer source, Material steel, Transform root)
        {
            var filter = source.GetComponent<MeshFilter>(); var bounds = filter.sharedMesh.bounds; var t = source.transform;
            Vector3 a = t.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
            Vector3 b = t.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));
            Vector3 along = (b - a).normalized, top = t.up.normalized, outward = Vector3.right;
            float length = Vector3.Distance(a, b);
            var edge = new B02ArchMesh(source.name + " folded roof edge", .9f);
            edge.Box((a + b) * .5f + outward * .075f + top * .008f, along, top, outward, new Vector3(length, .016f, .25f));
            edge.Box((a + b) * .5f + outward * .19f - top * .20f, along, top, outward, new Vector3(length, .42f, .014f));
            edge.Box((a + b) * .5f + outward * .14f - top * .407f, along, top, outward, new Vector3(length, .014f, .10f));
            B02ArchSave(edge, "B02 roof edge " + source.name + " folded rake cap", source.sharedMaterial, root);
            var clips = new B02ArchMesh(source.name + " roof support", .45f);
            foreach (float f in new[] { .12f, .47f, .82f })
            {
                Vector3 p = Vector3.Lerp(a, b, f) - top * .39f;
                clips.Box(p - outward * .10f, along, top, outward, new Vector3(.16f, .018f, .52f));
                clips.Box(p - outward * .35f - top * .10f, along, top, outward, new Vector3(.16f, .22f, .018f));
            }
            B02ArchSave(clips, "B02 roof support " + source.name + " edge cleats", steel, root);
        }

        static void B02ArchBolt(B02ArchMesh data, Vector3 center, Vector3 u, Vector3 normal)
        {
            Vector3 v = Vector3.up; const float radius = .022f, half = .009f; const int sides = 8;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 ra = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius, rb = (u * Mathf.Cos(b) + v * Mathf.Sin(b)) * radius;
                data.Face(center + ra - normal * half, center + rb - normal * half, center + rb + normal * half, center + ra + normal * half, (ra + rb).normalized);
                var middleUV = new Vector2(radius, radius);
                var aUV = middleUV + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                var bUV = middleUV + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                data.Triangle(center + normal * half, center + ra + normal * half, center + rb + normal * half,
                    normal, middleUV, aUV, bUV, radius * 2, radius * 2);
                data.Triangle(center - normal * half, center + rb - normal * half, center + ra - normal * half,
                    -normal, middleUV, bUV, aUV, radius * 2, radius * 2);
            }
        }

        static void B02ArchSave(B02ArchMesh data, string name, Material material, Transform parent)
        {
            if (data.vertices.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = data.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(data.vertices); mesh.SetTriangles(data.triangles, 0); mesh.SetUVs(0, data.uv);
            mesh.SetUVs(2, data.panel); mesh.SetColors(data.colors); mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            string path = B02ArchFolder + "/" + name.Replace(' ', '_') + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Obj.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            if (name.StartsWith("B02 glazing")) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true; }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.ContributeGI);
        }
    }
}
