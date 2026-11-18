using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Geometry-only representative trial. All materials are existing assets, without edits.
        // The loading route uses B02's already open east bay; this does not create a window.
        static void RepApplyDepth()
        {
            var warehouse = Existing("B02");
            if (warehouse == null) throw new InvalidOperationException("REP depth requires B02.");
            var foundation = warehouse.GetComponentsInChildren<MeshRenderer>()
                .FirstOrDefault(r => r.name == "Foundation");
            if (foundation == null || Mathf.Abs(foundation.bounds.max.x + 70) > .03f
                || Mathf.Abs(foundation.bounds.max.y - 1) > .015f)
                throw new InvalidOperationException("B02 foundation changed; refusing a disconnected loading dock.");
            var concrete = foundation.sharedMaterial;
            var steel = warehouse.GetComponentsInChildren<MeshRenderer>()
                .First(r => r.name == "remaining shallow steel eave gutter").sharedMaterial;
            var rubber = AssetDatabase.LoadAssetAtPath<Material>(Base + "Materials/Maintenance_cable_rubber.mat");
            if (concrete == null || steel == null || rubber == null)
                throw new InvalidOperationException("REP depth existing material is missing.");

            var root = ArchitectureGroup("REP depth connections", structures);
            const float south = 47.2f, north = 51.0f, dockTop = 1f, toeTop = 0f;
            const float west = -70.02f, joint = -62f, east = -54f, bottom = -.12f;
            const float dockEnd = -62.35f, rampStart = -61.65f;
            float rampStartTop = dockTop - (rampStart - joint) / 8f;
            // The 20 mm overlap closes the old slab contact, without entering the personnel strip.
            // A 25 mm long-edge chamfer changes the silhouette but keeps the two access ends flush.
            RepDepthPrism("REP concrete loading dock", west, dockEnd,
                RepDepthSlabSection(south, north, dockTop, bottom, .025f),
                RepDepthSlabSection(south, north, dockTop, bottom, .025f), concrete, root, true);
            RepDepthPrism("REP concrete loading ramp", rampStart, east,
                RepDepthSlabSection(south, north, rampStartTop, bottom, .025f),
                RepDepthSlabSection(south, north, toeTop, bottom, .025f), concrete, root, true);

            // Thin steel angles have an actual closed L section, not opaque strip decals.
            // They stop before each access end; north clearance to the retained wall is > 0.5 m.
            foreach (int side in new[] { -1, 1 })
            {
                float z = side < 0 ? south : north;
                string sideName = side < 0 ? "south" : "north";
                RepDepthPrism("REP dock " + sideName + " steel edge angle", -69.75f, dockEnd,
                    RepDepthAngleSection(z, dockTop, side), RepDepthAngleSection(z, dockTop, side), steel, root, false);
                RepDepthPrism("REP ramp " + sideName + " steel edge angle", rampStart, -54.35f,
                    RepDepthAngleSection(z, rampStartTop, side),
                    RepDepthAngleSection(z, Mathf.Lerp(dockTop, toeTop, 7.65f / 8f), side), steel, root, false);
            }

            // The existing north-south stormwater channel is centred at x=-62. Its actual bed is
            // x[-62.23,-61.77]. No solid concrete crosses the 0.70 m clear gap. The original bed,
            // shoulders and grates stay in place; a real bridge deck bears 0.10 m onto each end.
            const float plateStart = -62.45f, plateEnd = -61.55f, plateThickness = .025f;
            float plateBottomStart = dockTop, plateBottomEnd = dockTop - (plateEnd - joint) / 8f;
            RepDepthPrism("REP steel drainage bridge deck", plateStart, plateEnd,
                RepDepthRectangle(south, north, plateBottomStart + plateThickness, plateBottomStart),
                RepDepthRectangle(south, north, plateBottomEnd + plateThickness, plateBottomEnd), steel, root, true);
            foreach (float z in new[] { 47.4f, 50.8f })
                RepDepthPrism("REP drainage bridge I girder " + z.ToString("F1", Inv), plateStart, plateEnd,
                    RepDepthISection(z, plateBottomStart, .13f, .10f, .012f, .010f),
                    RepDepthISection(z, plateBottomEnd, .13f, .10f, .012f, .010f), steel, root, false);
            foreach (int side in new[] { -1, 1 })
            {
                float z = side < 0 ? south : north;
                float topA = Mathf.Lerp(plateBottomStart, plateBottomEnd, (dockEnd - plateStart) / (plateEnd - plateStart)) + plateThickness;
                float topB = Mathf.Lerp(plateBottomStart, plateBottomEnd, (rampStart - plateStart) / (plateEnd - plateStart)) + plateThickness;
                RepDepthPrism("REP bridge " + (side < 0 ? "south" : "north") + " steel edge angle", dockEnd, rampStart,
                    RepDepthAngleSection(z, topA, side), RepDepthAngleSection(z, topB, side), steel, root, false);
            }

            // Reuse the existing rubber material for two protected loading-side contact blocks.
            foreach (float x in new[] { -68.1f, -65.3f })
            {
                RepDepthPrism("REP dock bumper steel backing " + x.ToString("F1", Inv), x - .18f, x + .18f,
                    RepDepthRectangle(south - .028f, south + .006f, .84f, .22f),
                    RepDepthRectangle(south - .028f, south + .006f, .84f, .22f), steel, root, false);
                RepDepthPrism("REP dock rubber bumper " + x.ToString("F1", Inv), x - .135f, x + .135f,
                    RepDepthRectangle(south - .145f, south - .025f, .79f, .27f),
                    RepDepthRectangle(south - .145f, south - .025f, .79f, .27f), rubber, root, false);
            }
            Physics.SyncTransforms();
            Debug.Log("REP_DEPTH_READY dock x[-70.02,-62.35], ramp x[-61.65,-54], z[47.2,51], top 1 to 0; "
                + "drain clear gap x[-62.35,-61.65], bridge x[-62.45,-61.55], 25mm deck, two 130mm I girders; "
                + "lowest girder underside y=0.81375 (>0.826m above existing grate y=-0.013); "
                + "existing Foundation, drainage and personnel walkway preserved; three new static MeshColliders; reused materials only.");
        }

        // Sections are counterclockwise in (world z, world y). Every prism is closed and manifold.
        static Vector2[] RepDepthSlabSection(float z0, float z1, float top, float bottom, float bevel)
        {
            return new[] { new Vector2(z0, bottom), new Vector2(z1, bottom),
                new Vector2(z1, top - bevel), new Vector2(z1 - bevel, top),
                new Vector2(z0 + bevel, top), new Vector2(z0, top - bevel) };
        }

        static Vector2[] RepDepthRectangle(float z0, float z1, float top, float bottom)
        {
            return new[] { new Vector2(z0, bottom), new Vector2(z1, bottom),
                new Vector2(z1, top), new Vector2(z0, top) };
        }

        static Vector2[] RepDepthAngleSection(float edge, float top, int side)
        {
            var points = new[] { new Vector2(-.09f, .001f), new Vector2(.008f, .001f),
                new Vector2(.008f, .15f), new Vector2(-.004f, .15f),
                new Vector2(-.004f, .013f), new Vector2(-.09f, .013f) };
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(edge + points[i].x * side, top + points[i].y);
            if (side < 0) Array.Reverse(points);
            return points;
        }

        static Vector2[] RepDepthISection(float center, float top, float depth, float width, float flange, float web)
        {
            float left = center - width * .5f, right = center + width * .5f;
            float webLeft = center - web * .5f, webRight = center + web * .5f, bottom = top - depth;
            return new[] { new Vector2(left, bottom), new Vector2(right, bottom),
                new Vector2(right, bottom + flange), new Vector2(webRight, bottom + flange),
                new Vector2(webRight, top - flange), new Vector2(right, top - flange),
                new Vector2(right, top), new Vector2(left, top),
                new Vector2(left, top - flange), new Vector2(webLeft, top - flange),
                new Vector2(webLeft, bottom + flange), new Vector2(left, bottom + flange) };
        }

        static GameObject RepDepthPrism(string name, float x0, float x1, Vector2[] a, Vector2[] b,
            Material material, Transform parent, bool collision)
        {
            if (x1 <= x0 || a.Length != b.Length) throw new ArgumentException("Invalid REP prism: " + name);
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            Action<Vector3, Vector3, Vector3> add = (p, q, r) =>
            {
                int first = vertices.Count; vertices.AddRange(new[] { p, q, r });
                var normal = Vector3.Cross(q - p, r - p).normalized;
                foreach (var v in new[] { p, q, r })
                    uv.Add(Mathf.Abs(normal.y) >= Mathf.Max(Mathf.Abs(normal.x), Mathf.Abs(normal.z))
                        ? new Vector2(v.x, v.z) : Mathf.Abs(normal.x) > Mathf.Abs(normal.z)
                        ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y));
                triangles.AddRange(new[] { first, first + 1, first + 2 });
            };
            for (int i = 0; i < a.Length; i++)
            {
                int j = (i + 1) % a.Length;
                var p = new Vector3(x0, a[i].y, a[i].x); var q = new Vector3(x1, b[i].y, b[i].x);
                var r = new Vector3(x1, b[j].y, b[j].x); var s = new Vector3(x0, a[j].y, a[j].x);
                add(p, q, r); add(p, r, s);
            }
            var capA = RepDepthTriangulate(a); var capB = RepDepthTriangulate(b);
            for (int i = 0; i < capA.Count; i += 3)
                add(new Vector3(x0, a[capA[i]].y, a[capA[i]].x),
                    new Vector3(x0, a[capA[i + 1]].y, a[capA[i + 1]].x),
                    new Vector3(x0, a[capA[i + 2]].y, a[capA[i + 2]].x));
            for (int i = 0; i < capB.Count; i += 3)
                add(new Vector3(x1, b[capB[i + 2]].y, b[capB[i + 2]].x),
                    new Vector3(x1, b[capB[i + 1]].y, b[capB[i + 1]].x),
                    new Vector3(x1, b[capB[i]].y, b[capB[i]].x));

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            RepDepthValidateClosed(vertices, triangles, name);
            Unwrapping.GenerateSecondaryUVSet(mesh);
            const string folder = "Assets/Map35/RepSection/Meshes";
            if (!AssetDatabase.IsValidFolder(folder)) { Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
            string path = folder + "/" + name.Replace(' ', '_') + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Obj.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = saved;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic
                | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        static List<int> RepDepthTriangulate(Vector2[] polygon)
        {
            var ring = Enumerable.Range(0, polygon.Length).ToList(); var result = new List<int>();
            Func<Vector2, Vector2, float> cross = (a, b) => a.x * b.y - a.y * b.x;
            while (ring.Count > 3)
            {
                bool clipped = false;
                for (int n = 0; n < ring.Count; n++)
                {
                    int i = ring[(n + ring.Count - 1) % ring.Count], j = ring[n], k = ring[(n + 1) % ring.Count];
                    var a = polygon[i]; var b = polygon[j]; var c = polygon[k];
                    if (cross(b - a, c - a) <= 1e-8f) continue;
                    bool occupied = ring.Any(v => v != i && v != j && v != k
                        && cross(b - a, polygon[v] - a) >= -1e-8f
                        && cross(c - b, polygon[v] - b) >= -1e-8f
                        && cross(a - c, polygon[v] - c) >= -1e-8f);
                    if (occupied) continue;
                    result.AddRange(new[] { i, j, k }); ring.RemoveAt(n); clipped = true; break;
                }
                if (!clipped) throw new InvalidOperationException("REP section cannot be triangulated.");
            }
            result.AddRange(ring); return result;
        }

        static void RepDepthValidateClosed(List<Vector3> vertices, List<int> triangles, string name)
        {
            var welded = new Dictionary<Vector3, int>(); var ids = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                if (!welded.TryGetValue(vertices[i], out int id)) { id = welded.Count; welded.Add(vertices[i], id); }
                ids[i] = id;
            }
            var counts = new Dictionary<long, int>(); var orientation = new Dictionary<long, int>();
            for (int i = 0; i < triangles.Count; i += 3)
            for (int j = 0; j < 3; j++)
            {
                int a = ids[triangles[i + j]], b = ids[triangles[i + (j + 1) % 3]];
                long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
                orientation[key] = (orientation.TryGetValue(key, out int direction) ? direction : 0) + (a < b ? 1 : -1);
            }
            if (counts.Any(p => p.Value != 2 || orientation[p.Key] != 0))
                throw new InvalidOperationException("REP depth mesh is not a closed oriented manifold: " + name);
        }
    }
}
