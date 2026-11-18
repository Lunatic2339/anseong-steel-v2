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
        // Called after the palette and maintenance yard, before physical UVs and prefab/LOD saving.
        // All dimensions below are metres. Source-building details use the source transform once.
        static void CompleteIndustrialStructures()
        {
            var report = new StringBuilder("Industrial completion / authored static environment, not runtime machinery\n");
            CompleteValveAccess();
            CompleteBrokenPipeBridge();
            CompleteHandlingCrane();
            CompletePumpBuilding();
            CompleteProductionHalls();
            CompleteFurnaceAndCleaning();
            foreach (string id in new[] { "B03", "B04", "B05", "B06", "B07", "B08", "B09" })
            {
                var root = Existing(id);
                report.AppendLine(id + " renderers=" + root.GetComponentsInChildren<MeshRenderer>(true).Length
                    + " bounds=" + LegacyEnvironmentAudit.BoundsOf(root.gameObject).size.ToString("F2"));
            }
            report.AppendLine("P02=(140,48,555), suspended coil=(90,52,555), settled coil XZ=(57,555) unchanged. Following player-facing pass sets settled y=2.62 to contact the R1 slab.");
            report.AppendLine("Z2C supported pipe rack x[-168,-130], z[322,337], deck y21; grounded landing ladder.");
            report.AppendLine("B07 personnel tower x[147.4,153], z[575,585], below y46; drone spine remains x165.");
            report.AppendLine("Native meshes are grouped by material; no per-bolt renderers. Physical UV and building LOD run afterwards.");
            report.AppendLine("HMD performance and animated lift/coil destruction are not validated by this static pass.");
            File.WriteAllText(WideOutput + "/industrial-completion.txt", report.ToString());
        }

        // Small native mesh assembler. World transforms are baked once; batch groups stay world-identity.
        // Bevels, hollow tube ends and annular steel coils are real geometry, not normal-map stand-ins.
        sealed class IndustrialGeometry
        {
            sealed class Part
            {
                public readonly List<Vector3> vertices = new List<Vector3>();
                public readonly List<int> indices = new List<int>();
            }
            readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();
            readonly Transform group;
            readonly string assetName;
            public readonly Matrix4x4 frame;
            public IndustrialGeometry(Transform parent, string name, Matrix4x4? coordinateFrame = null)
            {
                group = ArchitectureGroup(name, parent); assetName = name.Replace(' ', '_');
                frame = coordinateFrame ?? Matrix4x4.identity;
            }
            Part Get(Material material)
            {
                if (!parts.TryGetValue(material, out var p)) { p = new Part(); parts.Add(material, p); }
                return p;
            }
            void Face(Material mat, Vector3 normal, params Vector3[] points)
            {
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0) Array.Reverse(points);
                var p = Get(mat); int first = p.vertices.Count;
                foreach (var v in points) p.vertices.Add(frame.MultiplyPoint3x4(v));
                for (int i = 1; i < points.Length - 1; i++) p.indices.AddRange(new[] { first, first + i, first + i + 1 });
            }
            public void Box(Vector3 center, Vector3 size, Material mat, float bevel = 0, Quaternion? orientation = null)
            {
                Quaternion q = orientation ?? Quaternion.identity; Vector3 h = size * .5f;
                float b = Mathf.Clamp(bevel, 0, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .8f);
                Func<Vector3, Vector3> point = v => center + q * v;
                Action<Vector3, Vector3[]> face = (n, v) => Face(mat, q * n, v.Select(point).ToArray());
                for (int a = 0; a < 3; a++) foreach (int sign in new[] { -1, 1 })
                {
                    int u = (a + 1) % 3, v = (a + 2) % 3; var n = Vector3.zero; n[a] = sign;
                    var corners = new Vector3[4]; int k = 0;
                    foreach (var uv in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                    { var p = Vector3.zero; p[a] = sign * h[a]; p[u] = uv.x * (h[u] - b); p[v] = uv.y * (h[v] - b); corners[k++] = p; }
                    face(n, corners);
                }
                if (b <= 0) return;
                for (int axis = 0; axis < 3; axis++) foreach (int sa in new[] { -1, 1 }) foreach (int sb in new[] { -1, 1 })
                {
                    int a = (axis + 1) % 3, c = (axis + 2) % 3; var n = Vector3.zero; n[a] = sa; n[c] = sb;
                    var v = new Vector3[4];
                    for (int j = 0; j < 4; j++)
                    { var p = Vector3.zero; p[axis] = (j < 2 ? -1 : 1) * (h[axis] - b); bool first = j == 0 || j == 3; p[a] = sa * (h[a] - (first ? 0 : b)); p[c] = sb * (h[c] - (first ? b : 0)); v[j] = p; }
                    face(n, v);
                }
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                    face(new Vector3(x, y, z), new[] { new Vector3(x * h.x, y * (h.y - b), z * (h.z - b)), new Vector3(x * (h.x - b), y * h.y, z * (h.z - b)), new Vector3(x * (h.x - b), y * (h.y - b), z * h.z) });
            }
            public void Bar(Vector3 a, Vector3 b, float width, float depth, Material mat)
            {
                var d = b - a; if (d.sqrMagnitude < .0001f) return;
                Box((a + b) * .5f, new Vector3(width, depth, d.magnitude), mat, Mathf.Min(width, depth) * .08f,
                    Quaternion.LookRotation(d, Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > .98f ? Vector3.forward : Vector3.up));
            }
            public void IBeam(Vector3 a, Vector3 b, float width, float depth, Material mat)
            {
                var d = b - a; var q = Quaternion.LookRotation(d, Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > .98f ? Vector3.forward : Vector3.up);
                var c = (a + b) * .5f; float t = depth * .12f;
                Box(c, new Vector3(width * .16f, depth - t * 2, d.magnitude), mat, 0, q);
                foreach (float s in new[] { -1f, 1f }) Box(c + q * Vector3.up * s * (depth - t) * .5f, new Vector3(width, t, d.magnitude), mat, .025f, q);
            }
            public void Tube(IList<Vector3> path, float radius, Material mat, int sides = 18, bool hollowEnds = false)
            {
                if (path.Count < 2) return; var p = Get(mat); int first = p.vertices.Count;
                var rings = new Vector3[path.Count][]; var tangents = new Vector3[path.Count]; Vector3 transportedU = Vector3.zero;
                for (int i = 0; i < path.Count; i++)
                {
                    var d = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(0, i - 1)]).normalized; tangents[i] = d;
                    // Transport the section along bends instead of swapping reference axes mid-elbow.
                    var u = i == 0 ? Vector3.Cross(d, Mathf.Abs(Vector3.Dot(d, Vector3.up)) > .98f ? Vector3.forward : Vector3.up).normalized
                        : Quaternion.FromToRotation(tangents[i - 1], d) * transportedU;
                    u = (u - d * Vector3.Dot(u, d)).normalized; transportedU = u;
                    var v = Vector3.Cross(d, u); rings[i] = new Vector3[sides];
                    for (int j = 0; j < sides; j++) { float t = j * Mathf.PI * 2 / sides; var pt = path[i] + radius * (Mathf.Cos(t) * u + Mathf.Sin(t) * v); rings[i][j] = pt; p.vertices.Add(frame.MultiplyPoint3x4(pt)); }
                }
                for (int i = 0; i < path.Count - 1; i++) for (int j = 0; j < sides; j++)
                { int a = first + i * sides + j, b = first + i * sides + (j + 1) % sides, c = b + sides, d = a + sides; p.indices.AddRange(new[] { a, b, c, a, c, d }); }
                foreach (int end in new[] { 0, path.Count - 1 })
                {
                    float sign = end == 0 ? -1 : 1; Vector3 n = tangents[end] * sign;
                    if (!hollowEnds) Face(mat, n, rings[end]);
                    else
                    {
                        for (int j = 0; j < sides; j++)
                        {
                            var a = rings[end][j]; var b = rings[end][(j + 1) % sides];
                            var ai = Vector3.Lerp(path[end], a, .84f); var bi = Vector3.Lerp(path[end], b, .84f);
                            Face(mat, n, a, b, bi, ai);
                            Face(structuralSteel, path[end] - a, ai, bi, bi - n * radius * 1.2f, ai - n * radius * 1.2f);
                        }
                        Face(agedRoof, n, rings[end].Select(v => Vector3.Lerp(path[end], v, .84f) - n * radius * 1.2f).ToArray());
                    }
                }
            }
            public void Rod(Vector3 a, Vector3 b, float radius, Material mat, int sides = 16) => Tube(new[] { a, b }, radius, mat, sides);
            public void Pipe(IList<Vector3> corners, float radius, Material mat)
            {
                var path = new List<Vector3> { corners[0] };
                for (int i = 1; i < corners.Count - 1; i++)
                {
                    var incoming = corners[i] - corners[i - 1]; var outgoing = corners[i + 1] - corners[i];
                    float trim = Mathf.Min(radius * 2.2f, Mathf.Min(incoming.magnitude, outgoing.magnitude) * .35f);
                    var a = corners[i] - incoming.normalized * trim; var b = corners[i] + outgoing.normalized * trim;
                    for (int j = 0; j <= 5; j++) { float t = j / 5f; path.Add(Vector3.Lerp(Vector3.Lerp(a, corners[i], t), Vector3.Lerp(corners[i], b, t), t)); }
                }
                path.Add(corners[corners.Count - 1]); Tube(path, radius, mat, 20);
            }
            public void Ring(Vector3 center, Vector3 axis, float outer, float inner, float depth, Material mat, int sides = 32, float ellipse = 1)
            {
                var q = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                Func<float, float, float, Vector3> at = (r, t, h) => center + q * new Vector3(Mathf.Cos(t) * r, h, Mathf.Sin(t) * r * ellipse);
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2 / sides, b = (j + 1) * Mathf.PI * 2 / sides;
                    foreach (float s in new[] { -1f, 1f }) Face(mat, axis * s, at(inner, a, s * depth / 2), at(outer, a, s * depth / 2), at(outer, b, s * depth / 2), at(inner, b, s * depth / 2));
                    Face(mat, q * new Vector3(Mathf.Cos((a + b) / 2), 0, Mathf.Sin((a + b) / 2)), at(outer, a, -depth / 2), at(outer, b, -depth / 2), at(outer, b, depth / 2), at(outer, a, depth / 2));
                    Face(mat, q * new Vector3(-Mathf.Cos((a + b) / 2), 0, -Mathf.Sin((a + b) / 2)), at(inner, a, -depth / 2), at(inner, a, depth / 2), at(inner, b, depth / 2), at(inner, b, -depth / 2));
                }
            }
            public void Frustum(Vector3 bottom, Vector3 top, float lowerRadius, float upperRadius, Material mat, int sides = 32)
            {
                var d = (top - bottom).normalized; var q = Quaternion.FromToRotation(Vector3.up, d);
                var lower = new List<Vector3>(); var upper = new List<Vector3>();
                for (int i = 0; i < sides; i++)
                { float a = i * Mathf.PI * 2 / sides; var v = q * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); lower.Add(bottom + v * lowerRadius); upper.Add(top + v * upperRadius); }
                for (int i = 0; i < sides; i++)
                { int j = (i + 1) % sides; Face(mat, lower[i] - bottom, lower[i], lower[j], upper[j], upper[i]); }
                Face(mat, -d, lower.ToArray()); Face(mat, d, upper.ToArray());
            }
            public void Finish(bool collision = false)
            {
                int index = 0;
                foreach (var pair in parts)
                {
                    var mesh = new Mesh { indexFormat = pair.Value.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(pair.Value.vertices); mesh.SetTriangles(pair.Value.indices, 0);
                    mesh = SaveDetailMesh(mesh, assetName + "_" + index);
                    var go = new GameObject(assetName + " material " + index++); go.transform.SetParent(group, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
                    if (collision) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            }
        }

        static void IndustrialRail(IndustrialGeometry g, Vector3 a, Vector3 b, Material frameMat, Material railMat)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2));
            for (int i = 0; i <= count; i++) { var p = Vector3.Lerp(a, b, i / (float)count); g.Rod(p, p + Vector3.up * 1.1f, .035f, frameMat, 8); }
            foreach (float h in new[] { .55f, 1.1f }) g.Rod(a + Vector3.up * h, b + Vector3.up * h, .035f, railMat, 10);
            g.Bar(a + Vector3.up * .09f, b + Vector3.up * .09f, .045f, .17f, frameMat);
        }
        static void IndustrialFlange(IndustrialGeometry g, Vector3 p, Vector3 axis, float radius, Material m)
        {
            g.Ring(p, axis, radius * 1.28f, radius * .95f, .13f, m, 20);
            var q = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; var v = q * new Vector3(Mathf.Cos(a) * radius * 1.1f, 0, Mathf.Sin(a) * radius * 1.1f); g.Rod(p + v - axis.normalized * .095f, p + v + axis.normalized * .095f, .035f, structuralSteel, 6); }
        }
        static void IndustrialLadder(IndustrialGeometry g, Vector3 bottom, float height, Material mat)
        {
            foreach (float s in new[] { -.4f, .4f }) g.Bar(bottom + Vector3.right * s, bottom + Vector3.up * height + Vector3.right * s, .065f, .065f, mat);
            for (float y = .28f; y < height; y += .3f) g.Rod(bottom + new Vector3(-.4f, y, 0), bottom + new Vector3(.4f, y, 0), .022f, mat, 8);
        }
        static void IndustrialCollider(string name, Transform parent, Vector3 center, Vector3 size)
        { var go = new GameObject(name); go.transform.SetPositionAndRotation(center, Quaternion.identity); go.transform.SetParent(parent, true); go.AddComponent<BoxCollider>().size = size; }

        static void IndustrialRemoveGeometryKeepText(Transform group)
        {
            if (group == null) return;
            foreach (var text in group.GetComponentsInChildren<TextMesh>(true)) text.transform.SetParent(group.parent, true);
            Obj.DestroyImmediate(group.gameObject);
        }

        static void CompleteValveAccess()
        {
            IndustrialRemoveGeometryKeepText(nearDetails.Find("Z2C maintenance valve platform"));
            var parent = Existing("B06"); var g = new IndustrialGeometry(parent, "Industrial B06 connected valve rack");
            var metal = ThemeMaterial(5, "metal", 0); var pipe = ThemeMaterial(5, "galvanized", 0); var paint = ThemeMaterial(5, "paint", 0);
            // Four real columns carry the service deck; the rack is entirely west of the player arena.
            foreach (float x in new[] { -147.4f, -136.5f }) foreach (float z in new[] { 330f, 334f })
            {
                g.Box(new Vector3(x, .25f, z), new Vector3(1.6f, .5f, 1.6f), agedConcrete, .05f);
                g.IBeam(new Vector3(x, .5f, z), new Vector3(x, 21, z), .42f, .5f, metal);
            }
            foreach (float y in new[] { 7f, 14f, 20.7f })
            {
                g.IBeam(new Vector3(-147.4f, y, 334), new Vector3(-136.5f, y, 334), .3f, .42f, metal);
                foreach (float x in new[] { -147.4f, -136.5f }) g.IBeam(new Vector3(x, y, 330), new Vector3(x, y, 334), .25f, .35f, metal);
                g.Bar(new Vector3(-147.4f, y - 6.5f, 334), new Vector3(-136.5f, y, 334), .16f, .2f, metal);
            }
            g.Box(new Vector3(-142, 20.88f, 332), new Vector3(12, .24f, 4.2f), metal, .025f);
            for (float x = -147.8f; x < -136.1f; x += .35f) g.Bar(new Vector3(x, 21.015f, 330), new Vector3(x, 21.015f, 334), .045f, .045f, pipe);
            IndustrialRail(g, new Vector3(-147.9f, 21, 334.05f), new Vector3(-136.1f, 21, 334.05f), metal, agedYellow);
            IndustrialRail(g, new Vector3(-147.9f, 21, 330), new Vector3(-147.9f, 21, 334), metal, agedYellow);
            IndustrialRail(g, new Vector3(-135.95f, 21, 330), new Vector3(-135.95f, 21, 334), metal, agedYellow);
            IndustrialRail(g, new Vector3(-147.9f, 21, 329.95f), new Vector3(-138.1f, 21, 329.95f), metal, agedYellow);
            IndustrialLadder(g, new Vector3(-137, .15f, 329.6f), 21.9f, pipe);
            for (float y = 3; y < 20.5f; y += 1.4f) g.Ring(new Vector3(-137, y, 329.15f), Vector3.up, .68f, .64f, .05f, metal, 16);
            foreach (float y in new[] { 7f, 14f })
            {
                g.Box(new Vector3(-135.9f, y, 330.3f), new Vector3(1.4f, .18f, 1.4f), metal, .02f);
                g.Bar(new Vector3(-136.5f, y - 1.1f, 330), new Vector3(-135.2f, y, 330.3f), .16f, .2f, metal);
                IndustrialRail(g, new Vector3(-135.2f, y + .1f, 329.6f), new Vector3(-135.2f, y + .1f, 331), metal, agedYellow);
            }
            // Circular process lines connect the silo wall to a grounded pumping manifold.
            for (int i = 0; i < 3; i++)
            {
                float y = 10 + i * 3, z = 323 + i * 1.1f;
                g.Pipe(new[] { new Vector3(-155, y, 333.1f), new Vector3(-155, y, z), new Vector3(-132.3f, y, z), new Vector3(-132.3f, 2.1f, z) }, .43f, pipe);
                IndustrialFlange(g, new Vector3(-154.3f, y, z), Vector3.right, .43f, metal);
                IndustrialFlange(g, new Vector3(-135, y, z), Vector3.right, .43f, metal);
                g.Box(new Vector3(-142 + i * 1.4f, y + .65f, z), new Vector3(.8f, .65f, .65f), paint, .08f);
                float x = -143 + i * 3;
                g.Pipe(new[] { new Vector3(x, 22.1f, 329.3f), new Vector3(x, 22.1f, 327.5f), new Vector3(x, y + .7f, 327.5f), new Vector3(-142 + i * 1.4f, y + .7f, z) }, .045f, metal);
                g.Box(new Vector3(x, 23, 329.16f), new Vector3(2.2f, 2.5f, .32f), paint, .06f);
                g.Box(new Vector3(x, 22.65f, 328.94f), new Vector3(1.55f, .8f, .12f), ThemeMaterial(5, "glass", 0), .025f);
                g.Rod(new Vector3(x, 21, 329.2f), new Vector3(x, 21.8f, 329.2f), .09f, metal);
            }
            foreach (float x in new[] { -151f, -140f, -132.3f })
            {
                foreach (float z in new[] { 322f, 326.2f }) { g.Box(new Vector3(x, .25f, z), new Vector3(1.25f, .5f, 1.25f), agedConcrete); g.IBeam(new Vector3(x, .5f, z), new Vector3(x, 16.7f, z), .3f, .35f, metal); }
                foreach (float y in new[] { 9.5f, 12.5f, 15.5f }) g.IBeam(new Vector3(x, y, 322), new Vector3(x, y, 326.2f), .25f, .3f, metal);
            }
            g.Box(new Vector3(-132.3f, .45f, 324.1f), new Vector3(4, .9f, 5.2f), agedConcrete, .08f);
            g.Box(new Vector3(-132.3f, 1.4f, 324.1f), new Vector3(2.4f, 1, 4.2f), metal, .1f);
            g.Finish(true);
        }

        static void CompleteBrokenPipeBridge()
        {
            var broken = ruins.Find("Z3A broken pipe bridge"); if (broken == null) return;
            foreach (var t in broken.Cast<Transform>().Where(t => t.name == "ruptured pipe").ToArray()) Obj.DestroyImmediate(t.gameObject);
            var g = new IndustrialGeometry(broken, "Industrial Z3 fractured circular pipeline");
            for (int i = 0; i < 3; i++)
            {
                var top = new Vector3(237 + i * 2, 22, 459); var low = new Vector3(260 + i * 2, 2.4f + i * .2f, 480);
                g.Tube(new[] { top, top + new Vector3(6, -2, 3), low }, .4f, agedSteel, 20, true);
                var d = (low - top).normalized; IndustrialFlange(g, Vector3.Lerp(top, low, .3f), d, .4f, structuralSteel);
                g.Box(low + new Vector3(0, -.9f, .4f), new Vector3(2.1f, .8f, 2), damagedConcrete, .12f);
            }
            g.Box(new Vector3(237, .65f, 459), new Vector3(3.2f, 1.3f, 3.2f), agedConcrete, .07f);
            g.IBeam(new Vector3(237, 21.7f, 458), new Vector3(243, 21.7f, 458), .35f, .5f, structuralSteel);
            g.Finish();
        }

        static void CompleteHandlingCrane()
        {
            var root = Existing("B07");
            IndustrialRemoveGeometryKeepText(root.Find("Z3B close inspection service deck"));
            foreach (var t in root.Cast<Transform>().Where(t => t.name == "trolley" || t.name == "hoist cable" || t.name == "travelling bridge").ToArray()) Obj.DestroyImmediate(t.gameObject);
            var oldJoints = root.Find("Architecture B07 crane joints");
            if (oldJoints != null) foreach (var t in oldJoints.Cast<Transform>().Where(t => t.name.StartsWith("service guardrail") || t.name == "service walkway toe board").ToArray()) Obj.DestroyImmediate(t.gameObject);
            var g = new IndustrialGeometry(root, "Industrial B07 rail trolley and hoist");
            var metal = ThemeMaterial(6, "metal", 0); var zinc = ThemeMaterial(6, "galvanized", 0); var paint = ThemeMaterial(6, "paint", 0);
            // Twin bridge girders and real wheel/rail contact within the existing y68 envelope.
            foreach (float x in new[] { 40f, 140f })
            {
                g.IBeam(new Vector3(x, 67.6f, 526), new Vector3(x, 67.6f, 584), .25f, .32f, zinc);
                g.Box(new Vector3(x, 66.5f, 555), new Vector3(3.3f, 1.2f, 10), metal, .12f);
                foreach (float z in new[] { 551.5f, 558.5f }) g.Rod(new Vector3(x - 1.6f, 67, z), new Vector3(x + 1.6f, 67, z), .63f, zinc, 24);
            }
            foreach (float z in new[] { 551.5f, 558.5f })
            {
                g.IBeam(new Vector3(40, 65, z), new Vector3(140, 65, z), 1.6f, 5.5f, paint);
                g.IBeam(new Vector3(42, 67.78f, z), new Vector3(138, 67.78f, z), .18f, .2f, zinc);
                for (float x = 44; x < 138; x += 7) g.Box(new Vector3(x, 65, z), new Vector3(.14f, 5.2f, 1.58f), metal);
            }
            g.Box(new Vector3(90, 66.5f, 555), new Vector3(11.6f, .6f, 8.8f), metal, .1f);
            foreach (float x in new[] { 85.5f, 94.5f }) foreach (float z in new[] { 551.5f, 558.5f })
                g.Rod(new Vector3(x - .55f, 67.25f, z), new Vector3(x + .55f, 67.25f, z), .48f, zinc, 24);
            // Drums are mounted below the trolley, leaving its top below the established 68 m height.
            foreach (float x in new[] { 87f, 93f })
            {
                g.Rod(new Vector3(x, 63.4f, 552), new Vector3(x, 63.4f, 558), .8f, zinc, 32);
                foreach (float z in new[] { 551.9f, 558.1f }) g.Ring(new Vector3(x, 63.4f, z), Vector3.forward, 1, .3f, .14f, metal);
                for (float z = 552.2f; z <= 557.8f; z += .35f) g.Ring(new Vector3(x, 63.4f, z), Vector3.forward, .818f, .792f, .045f, structuralSteel, 24);
                foreach (float z in new[] { 553f, 557f })
                {
                    var reeving = new List<Vector3> { new Vector3(x, 62.8f, z - .53f) };
                    for (int j = 0; j <= 10; j++) { float a = j * Mathf.PI / 10; reeving.Add(new Vector3(x, 56.9f - Mathf.Sin(a) * .53f, z - Mathf.Cos(a) * .53f)); }
                    reeving.Add(new Vector3(x, 62.8f, z + .53f)); g.Tube(reeving, .045f, metal, 10);
                }
                g.Box(new Vector3(x, 65, 555), new Vector3(.8f, 2.4f, 7.2f), paint, .08f);
                g.Box(new Vector3(x, 63.4f, 559.1f), new Vector3(1.6f, 1.8f, 1.7f), paint, .18f);
                g.Rod(new Vector3(x, 63.4f, 560), new Vector3(x, 63.4f, 562.4f), .6f, metal, 24);
            }
            g.Box(new Vector3(90, 56.4f, 555), new Vector3(8.2f, .8f, 5.5f), paint, .14f);
            foreach (float x in new[] { 87f, 93f }) foreach (float z in new[] { 553f, 557f })
                g.Ring(new Vector3(x, 56.9f, z), Vector3.right, .5f, .12f, .24f, zinc, 24);
            // Lifting arms terminate at the coil upper rim; the planned side target stays visible.
            foreach (float x in new[] { 85.5f, 94.5f })
            {
                g.IBeam(new Vector3(x, 55.9f, 555), new Vector3(x, 54.15f, 555), .4f, .55f, metal);
                g.Box(new Vector3(x, 54.15f, 555), new Vector3(1.2f, .3f, 2.4f), zinc, .06f);
            }
            g.Finish();
            CompleteCoilGeometry(viewer.coil, "Industrial B07 suspended rolled steel");
            CompleteCoilGeometry(viewer.lateCoil.transform, "Industrial B07 settled rolled steel");
            CompleteCraneAccess(root, metal, paint, zinc);
        }

        static void CompleteCoilGeometry(Transform coil, string id)
        {
            // Keep the viewer's existing transform and 12 x 4 x 10 m collision envelope.
            var oldRenderer = coil.GetComponent<MeshRenderer>(); if (oldRenderer != null) Obj.DestroyImmediate(oldRenderer);
            var oldFilter = coil.GetComponent<MeshFilter>(); if (oldFilter != null) Obj.DestroyImmediate(oldFilter);
            var g = new IndustrialGeometry(coil, id, Matrix4x4.TRS(coil.position, Quaternion.identity, Vector3.one));
            var mat = ThemeMaterial(6, "galvanized", 0);
            g.Ring(Vector3.zero, Vector3.up, 6, 2, 4, mat, 80, 5f / 6);
            // Thin wound-sheet edges on both flat faces; no soft torus profile.
            foreach (float y in new[] { -2.006f, 2.006f })
                for (float r = 2.12f; r < 5.95f; r += .25f) g.Ring(new Vector3(0, y, 0), Vector3.up, r + .012f, r, .016f, structuralSteel, 80, 5f / 6);
            foreach (float z in new[] { -3.5f, 3.5f })
            {
                float x = 6 * Mathf.Sqrt(1 - z * z / 25);
                g.Box(new Vector3(0, 2.025f, z), new Vector3(x * 2, .035f, .22f), structuralSteel);
                foreach (float s in new[] { -1f, 1f }) g.Box(new Vector3(x * s, 0, z), new Vector3(.035f, 4, .22f), structuralSteel);
            }
            g.Finish();
        }

        static void CompleteCraneAccess(Transform root, Material metal, Material paint, Material zinc)
        {
            var g = new IndustrialGeometry(root, "Industrial B07 supported service access");
            // The deck remains at y46; the approach, supporting brackets and lift linkage are now explicit.
            g.Box(new Vector3(144.2f, 45.82f, 555), new Vector3(6, .36f, 13), metal, .035f);
            foreach (float z in new[] { 549f, 555f, 561f })
            {
                g.IBeam(new Vector3(140.2f, 45.5f, z), new Vector3(147.2f, 45.5f, z), .3f, .55f, metal);
                g.Bar(new Vector3(140, 41.8f, z), new Vector3(147, 45.5f, z), .25f, .3f, metal);
            }
            foreach (float x in new[] { 141.4f, 146.9f }) g.IBeam(new Vector3(x, 45.45f, 525), new Vector3(x, 45.45f, 585), .32f, .6f, metal);
            // A continuous truss takes the cantilever reactions back to the existing two east columns.
            g.IBeam(new Vector3(140, 41.8f, 525), new Vector3(140, 41.8f, 585), .3f, .45f, metal);
            g.IBeam(new Vector3(140, 45.45f, 525), new Vector3(140, 45.45f, 585), .3f, .45f, metal);
            for (int bay = 0; bay < 10; bay++)
            {
                float z = 525 + bay * 6;
                g.Bar(new Vector3(140, 41.8f, z), new Vector3(140, 45.45f, z), .18f, .22f, metal);
                g.Bar(new Vector3(140, bay % 2 == 0 ? 41.8f : 45.45f, z), new Vector3(140, bay % 2 == 0 ? 45.45f : 41.8f, z + 6), .18f, .22f, metal);
            }
            // A narrow catwalk links the deck to the columns and the ground-access tower.
            g.Box(new Vector3(146.2f, 45.87f, 555), new Vector3(1.45f, .22f, 60), metal, .02f);
            foreach (float z in new[] { 525f, 585f })
            {
                g.Bar(new Vector3(140, 39.5f, z), new Vector3(146.9f, 45.4f, z), .35f, .45f, metal);
                g.IBeam(new Vector3(140, 45.4f, z), new Vector3(146.9f, 45.4f, z), .35f, .55f, metal);
            }
            foreach (float z in new[] { 537f, 573f })
            { g.Bar(new Vector3(140, 42, z < 555 ? 525 : 585), new Vector3(146.9f, 45.4f, z), .28f, .35f, metal); }
            IndustrialRail(g, new Vector3(147.1f, 46, 525), new Vector3(147.1f, 46, 575), metal, paint);
            IndustrialRail(g, new Vector3(145.45f, 46, 525), new Vector3(145.45f, 46, 548.3f), metal, paint);
            IndustrialRail(g, new Vector3(145.45f, 46, 561.7f), new Vector3(145.45f, 46, 585), metal, paint);
            IndustrialRail(g, new Vector3(141.3f, 46, 548.6f), new Vector3(147.1f, 46, 548.6f), metal, paint);
            IndustrialRail(g, new Vector3(141.3f, 46, 561.4f), new Vector3(147.1f, 46, 561.4f), metal, paint);
            // Existing text stays in place on these backed housings. Controls occupy actual pedestals.
            foreach (float z in new[] { 549f, 554f })
            {
                g.Box(new Vector3(143, 47, z + .15f), new Vector3(.6f, 2, .5f), metal, .04f);
                g.Box(new Vector3(143, 48, z + .16f), new Vector3(z == 549 ? 2 : 2.3f, z == 549 ? 2.4f : 2.6f, .32f), paint, .08f);
                g.Box(new Vector3(143, 47.7f, z - .05f), new Vector3(1.25f, .6f, .14f), ThemeMaterial(6, "glass", 0), .03f);
            }
            // An open ring frames the P02 latch point without covering its central approach ray.
            g.Ring(new Vector3(140, 48, 555), Vector3.right, .85f, .48f, .24f, zinc, 32);
            g.Rod(new Vector3(140, 46, 555), new Vector3(140, 47.05f, 555), .13f, metal);
            // Offset the control conduit behind the latch before rising: K3b's coil ray is at z555.
            g.Pipe(new[] { new Vector3(140, 48.9f, 555), new Vector3(140, 49.2f, 557.5f), new Vector3(140, 60, 557.5f), new Vector3(137.5f, 63, 557.5f), new Vector3(99, 63, 557.5f) }, .085f, zinc);
            // Switchback stairs, 23 flights x 2 m, with 11 risers per flight (~182 mm).
            const float stepRise = 2f / 11; const float run = .27f;
            foreach (float x in new[] { 148f, 152.5f }) foreach (float z in new[] { 575.5f, 584.5f })
            { g.Box(new Vector3(x, .3f, z), new Vector3(1.5f, .6f, 1.5f), agedConcrete, .04f); g.IBeam(new Vector3(x, .6f, z), new Vector3(x, 46, z), .3f, .4f, metal); }
            for (int flight = 0; flight < 23; flight++)
            {
                float x = flight % 2 == 0 ? 149 : 151.2f; float startZ = flight % 2 == 0 ? 577 : 580; float sign = flight % 2 == 0 ? 1 : -1; float y = flight * 2;
                for (int step = 0; step < 11; step++) g.Box(new Vector3(x, y + (step + 1) * stepRise - .04f, startZ + sign * (step + .5f) * run), new Vector3(1.6f, .08f, .29f), metal);
                foreach (float side in new[] { -.86f, .86f })
                {
                    var a = new Vector3(x + side, y, startZ); var b = new Vector3(x + side, y + 2, startZ + sign * 2.97f);
                    g.Bar(a, b, .12f, .22f, metal); g.Rod(a + Vector3.up * 1.05f, b + Vector3.up * 1.05f, .035f, paint, 8);
                    g.Rod(a, a + Vector3.up * 1.1f, .035f, metal, 8); g.Rod(b, b + Vector3.up * 1.1f, .035f, metal, 8);
                }
                float landingZ = flight % 2 == 0 ? 580.7f : 576.3f;
                g.Box(new Vector3(150.1f, y + 1.91f, landingZ), new Vector3(4.5f, .18f, 1.5f), metal);
                IndustrialRail(g, new Vector3(148, y + 2, landingZ + sign * .7f), new Vector3(152.3f, y + 2, landingZ + sign * .7f), metal, paint);
            }
            g.Box(new Vector3(147.6f, 45.9f, 580.7f), new Vector3(2, .2f, 1.5f), metal);
            foreach (float z in new[] { 579.98f, 581.42f }) IndustrialRail(g, new Vector3(146.6f, 46, z), new Vector3(148, 46, z), metal, paint);
            g.Finish();
            IndustrialCollider("B07 service deck physical surface", root, new Vector3(144.2f, 45.82f, 555), new Vector3(6, .36f, 13));
            IndustrialCollider("B07 stair tower reservation", root, new Vector3(150.2f, 23, 580), new Vector3(5.4f, 46, 10));
        }

        static void IndustrialWindow(IndustrialGeometry g, Vector3 center, Vector3 outward, float width, float height, int theme)
        {
            var q = Quaternion.LookRotation(outward, Vector3.up); var metal = ThemeMaterial(theme, "metal", 0);
            g.Box(center + outward * .13f, new Vector3(width + .28f, height + .28f, .22f), metal, .035f, q);
            g.Box(center + outward * .26f, new Vector3(width, height, .05f), ThemeMaterial(theme, "glass", 0), .015f, q);
            foreach (float x in new[] { -width * .5f, width * .5f })
                g.Box(center + q * new Vector3(x, 0, .36f), new Vector3(.1f, height + .2f, .26f), metal, .018f, q);
            foreach (float y in new[] { -height * .5f, height * .5f })
                g.Box(center + q * new Vector3(0, y, .36f), new Vector3(width + .2f, .12f, .26f), metal, .018f, q);
            for (float x = -width * .5f + 1.15f; x < width * .5f - .3f; x += 1.15f)
                g.Box(center + q * new Vector3(x, 0, .32f), new Vector3(.055f, height, .1f), metal, 0, q);
            g.Box(center + q * new Vector3(0, -height * .5f - .1f, .4f), new Vector3(width + .45f, .09f, .6f), ThemeMaterial(theme, "galvanized", 0), .02f, q);
        }
        static void IndustrialDoor(IndustrialGeometry g, Vector3 bottom, Vector3 outward, float width, float height, int theme, bool loading = false)
        {
            var q = Quaternion.LookRotation(outward); var center = bottom + Vector3.up * height * .5f;
            var metal = ThemeMaterial(theme, "metal", 0); var paint = ThemeMaterial(theme, "paint", 1);
            g.Box(center + outward * .12f, new Vector3(width + .35f, height + .3f, .22f), agedRoof, .025f, q);
            g.Box(center + outward * .26f, new Vector3(width, height, .2f), paint, .04f, q);
            foreach (float x in new[] { -.5f, .5f }) g.Box(center + q * new Vector3(x * (width + .13f), 0, .35f), new Vector3(.16f, height + .2f, .3f), metal, .02f, q);
            g.Box(center + q * new Vector3(0, height / 2 + .06f, .35f), new Vector3(width + .45f, .18f, .4f), metal, .02f, q);
            if (loading)
            {
                for (float y = .5f; y < height; y += .6f) g.Box(bottom + q * new Vector3(0, y, .38f), new Vector3(width - .12f, .055f, .045f), metal, 0, q);
                foreach (float x in new[] { -.5f, .5f }) g.Box(bottom + q * new Vector3(x * (width + .6f), .75f, .5f), new Vector3(.25f, 1.5f, .35f), ThemeMaterial(theme, "warning", 0), .04f, q);
            }
            else
            {
                g.Box(center + q * new Vector3(width * .3f, -.12f, .46f), new Vector3(.055f, .32f, .1f), metal, .015f, q);
                g.Box(center + q * new Vector3(0, height * .22f, .38f), new Vector3(width * .48f, height * .22f, .05f), ThemeMaterial(theme, "glass", 0), 0, q);
            }
            g.Box(bottom + q * new Vector3(0, .035f, .43f), new Vector3(width + .65f, .07f, .85f), ThemeMaterial(theme, "concrete", 0), .025f, q);
        }
        static void IndustrialFacade(IndustrialGeometry g, Vector3 a, Vector3 b, Vector3 outward, float height, int theme, float windowY = -1, float pitch = 7)
        {
            var d = (b - a).normalized; float length = Vector3.Distance(a, b); int bays = Mathf.Max(1, Mathf.RoundToInt(length / pitch));
            var metal = ThemeMaterial(theme, "metal", 0); var zinc = ThemeMaterial(theme, "galvanized", 0);
            // Sheet modules and sill/fascia give large walls a construction scale on every exposed face.
            g.Bar(a + Vector3.up * .55f + outward * .07f, b + Vector3.up * .55f + outward * .07f, .3f, 1.05f, a.y < .1f ? ThemeMaterial(theme, "concrete", 0) : metal);
            g.Bar(a + Vector3.up * (height - .18f) + outward * .18f, b + Vector3.up * (height - .18f) + outward * .18f, .38f, .34f, metal);
            for (int i = 0; i <= bays; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)bays) + outward * .18f;
                g.IBeam(p + Vector3.up * .8f, p + Vector3.up * (height - .3f), .24f, .32f, metal);
                if (i == 0 || i == bays)
                    g.Pipe(new[] { p + Vector3.up * (height - .05f) + outward * .12f, p + Vector3.up * (height - .8f) + outward * .38f, p + Vector3.up * .3f + outward * .38f }, .095f, zinc);
            }
            for (float u = 1.5f; u < length; u += 1.5f)
                g.Bar(a + d * u + outward * .035f + Vector3.up * 1.1f, a + d * u + outward * .035f + Vector3.up * (height - .55f), .025f, .045f, metal);
            if (windowY > 0)
                for (int i = 0; i < bays; i++) IndustrialWindow(g, Vector3.Lerp(a, b, (i + .5f) / bays) + Vector3.up * windowY,
                    outward, Mathf.Min(5, length / bays - 1), Mathf.Min(2.8f, height * .15f), theme);
        }
        static void IndustrialRoof(IndustrialGeometry g, float x0, float x1, float z0, float z1, float eave, float ridge, int theme)
        {
            float mid = (x0 + x1) * .5f; var m = ThemeMaterial(theme, "roof", 0); var edge = ThemeMaterial(theme, "galvanized", 0);
            foreach (float z in new[] { z0, z1 })
            {
                g.Bar(new Vector3(x0, eave, z), new Vector3(mid, ridge, z), .18f, .22f, m);
                g.Bar(new Vector3(mid, ridge, z), new Vector3(x1, eave, z), .18f, .22f, m);
            }
            foreach (float x in new[] { x0, x1 }) g.Box(new Vector3(x, eave + .04f, (z0 + z1) / 2), new Vector3(.4f, .23f, z1 - z0), edge, .04f);
            g.Box(new Vector3(mid, ridge + .025f, (z0 + z1) / 2), new Vector3(.42f, .14f, z1 - z0), edge, .04f);
            for (float z = z0 + 3; z < z1; z += 4)
            {
                g.Bar(new Vector3(x0, eave + .08f, z), new Vector3(mid, ridge + .08f, z), .055f, .08f, m);
                g.Bar(new Vector3(mid, ridge + .08f, z), new Vector3(x1, eave + .08f, z), .055f, .08f, m);
            }
        }
        static void IndustrialVent(IndustrialGeometry g, Vector3 center, Vector3 outward, float width, float height, int theme)
        {
            var q = Quaternion.LookRotation(outward); var metal = ThemeMaterial(theme, "metal", 0);
            g.Box(center, new Vector3(width + .25f, height + .25f, .4f), metal, .05f, q);
            g.Box(center + outward * .24f, new Vector3(width, height, .07f), agedRoof, 0, q);
            for (float y = -height * .5f + .17f; y < height * .5f; y += .24f)
                g.Box(center + q * new Vector3(0, y, .4f), new Vector3(width, .055f, .4f), ThemeMaterial(theme, "galvanized", 0), .014f, q * Quaternion.Euler(22, 0, 0));
        }

        static void CompletePumpBuilding()
        {
            var root = Existing("B03"); var renderer = root.GetComponent<MeshRenderer>();
            // Source slot 3 is incorrectly named SafetyAndUtility: it contains both entire wall masses.
            if (renderer != null && renderer.sharedMaterials.Length == 4)
            { var mats = renderer.sharedMaterials; mats[3] = ThemeMaterial(2, "paint", 0); renderer.sharedMaterials = mats; }
            var g = new IndustrialGeometry(root, "Industrial B03 pump house complete envelope", root.localToWorldMatrix);
            var metal = ThemeMaterial(2, "metal", 0); var zinc = ThemeMaterial(2, "galvanized", 0);
            IndustrialFacade(g, new Vector3(-18, 0, -13.03f), new Vector3(18, 0, -13.03f), Vector3.back, 12, 2);
            IndustrialFacade(g, new Vector3(18.02f, 0, -13), new Vector3(18.02f, 0, 13), Vector3.right, 12, 2, 9.8f);
            IndustrialFacade(g, new Vector3(0, 0, 13.02f), new Vector3(18, 0, 13.02f), Vector3.forward, 12, 2, 9.6f);
            IndustrialFacade(g, new Vector3(-18, 0, 15.02f), new Vector3(0, 0, 15.02f), Vector3.forward, 16, 2, 11.5f);
            IndustrialFacade(g, new Vector3(-18.02f, 0, -5), new Vector3(-18.02f, 0, 15), Vector3.left, 16, 2, 11.5f);
            IndustrialFacade(g, new Vector3(-18.02f, 0, -13), new Vector3(-18.02f, 0, -5), Vector3.left, 12, 2, 9.5f);
            IndustrialRoof(g, -18.5f, .5f, -5.5f, 15.5f, 16, 18, 2);
            IndustrialVent(g, new Vector3(8, 5.6f, -13.38f), Vector3.back, 12.6f, 5.2f, 2);
            IndustrialDoor(g, new Vector3(-9, .05f, -13.23f), Vector3.back, 3, 4, 2, true);
            IndustrialDoor(g, new Vector3(-18.22f, .05f, -8.8f), Vector3.left, 1.6f, 3.1f, 2);
            IndustrialDoor(g, new Vector3(-8, 1.1f, 15.23f), Vector3.forward, 1.6f, 3.1f, 2);
            foreach (float z in new[] { -5f, 5f })
            {
                g.Box(new Vector3(8, 12.8f, z), new Vector3(4.2f, 1.2f, 3.2f), metal, .12f);
                IndustrialVent(g, new Vector3(8, 12.8f, z - 1.65f), Vector3.back, 3.7f, .85f, 2);
            }
            foreach (float z in new[] { -9f, -1f, 7f })
            {
                g.Rod(new Vector3(21, 4.7f, z), new Vector3(26.5f, 4.7f, z), .65f, metal, 24);
                IndustrialFlange(g, new Vector3(25.8f, 4.7f, z), Vector3.right, .65f, zinc);
                for (float x = 22; x < 25; x += .4f) g.Ring(new Vector3(x, 4.7f, z), Vector3.right, .71f, .65f, .06f, zinc, 20);
                g.Box(new Vector3(24, 2.3f, z - 2.55f), new Vector3(5.7f, 2.2f, .3f), ThemeMaterial(2, "paint", 1), .08f);
            }
            // A supported rear maintenance shelf makes the previously blank northwest side usable.
            g.Box(new Vector3(-9, .55f, 16.2f), new Vector3(16, 1.1f, 2.1f), ThemeMaterial(2, "concrete", 0), .06f);
            IndustrialRail(g, new Vector3(-16.8f, 1.1f, 17.1f), new Vector3(-10, 1.1f, 17.1f), metal, ThemeMaterial(2, "warning", 0));
            for (int i = 0; i < 6; i++) g.Box(new Vector3(-8, .09f + i * .183f, 19 - i * .3f), new Vector3(1.7f, .18f, .32f), metal, .02f);
            g.Finish();
            // Finish both older process mains and the new site branch at actual pump nozzles.
            // These are world-space endpoints authored by the landscape passes, not guessed offsets.
            var connections = new IndustrialGeometry(root, "Industrial B03 terminal pump connections");
            for (int i = 0; i < 3; i++)
            {
                var nozzle = root.TransformPoint(new Vector3(26.5f, 4.7f, -9 + i * 8));
                float x = 106.9f + i * .3f, y = 4 + i * .7f;
                connections.Ring(nozzle + Vector3.right * .055f, Vector3.right, .51f, .245f, .16f, zinc, 28);
                connections.Pipe(new[] { nozzle, new Vector3(x, nozzle.y, nozzle.z), new Vector3(x, y, nozzle.z), new Vector3(x, y, 45), new Vector3(108, y, 45) }, .25f, zinc);
                IndustrialFlange(connections, new Vector3(107.95f, y, 45), Vector3.right, .33f, metal);
                connections.Box(new Vector3(x, y - .4f, 45), new Vector3(.6f, .15f, .8f), metal, .03f);
            }
            var upperInlet = root.TransformPoint(new Vector3(23, 9.35f, 7));
            connections.Pipe(new[] { new Vector3(107, 7, 45), new Vector3(106, 7, 45), new Vector3(106, 7, 51), new Vector3(upperInlet.x, 7, 51), upperInlet }, .34f, zinc);
            IndustrialFlange(connections, new Vector3(107.03f, 7, 45), Vector3.right, .34f, metal);
            connections.Box(new Vector3(107.35f, 2.85f, 45), new Vector3(.18f, 5.7f, .18f), metal, .02f);
            connections.Box(new Vector3(107.35f, .15f, 45), new Vector3(.9f, .3f, .9f), ThemeMaterial(2, "concrete", 0), .03f);
            connections.Finish();
        }

        static void CompleteProductionHalls()
        {
            foreach (string id in new[] { "B04", "B05" })
            {
                var approximateFront = Existing(id).Find("Architecture " + id + " facade depth");
                if (approximateFront != null) Obj.DestroyImmediate(approximateFront.gameObject);
            }
            var root = Existing("B04"); var g = new IndustrialGeometry(root, "Industrial B04 steelmaking skin roof and ducts", root.localToWorldMatrix);
            var metal = ThemeMaterial(3, "metal", 0); var zinc = ThemeMaterial(3, "galvanized", 0);
            IndustrialFacade(g, new Vector3(-30, 0, -27.52f), new Vector3(30, 0, -27.52f), Vector3.back, 32, 3, -1, 7);
            for (int i = 0; i < 8; i++) IndustrialWindow(g, new Vector3(-25 + i * 7, 27, -27.86f), Vector3.back, 5, 2.8f, 3);
            IndustrialFacade(g, new Vector3(30.02f, 0, -27.5f), new Vector3(30.02f, 0, 27.5f), Vector3.right, 32, 3, 27, 8);
            IndustrialFacade(g, new Vector3(-30, 0, 27.52f), new Vector3(30, 0, 27.52f), Vector3.forward, 32, 3, 27, 8);
            IndustrialFacade(g, new Vector3(-30.02f, 14, -27.5f), new Vector3(-30.02f, 14, 27.5f), Vector3.left, 18, 3, 13, 8);
            IndustrialFacade(g, new Vector3(-48.02f, 0, -40), new Vector3(-48.02f, 0, 20), Vector3.left, 14, 3, 10.8f, 8);
            IndustrialFacade(g, new Vector3(-48, 0, -40.02f), new Vector3(-30, 0, -40.02f), Vector3.back, 14, 3);
            IndustrialFacade(g, new Vector3(-48, 0, 20.02f), new Vector3(-30, 0, 20.02f), Vector3.forward, 14, 3, 10.8f);
            IndustrialRoof(g, -30.5f, 30.5f, -28.5f, 28.5f, 32, 37, 3);
            IndustrialRoof(g, -48.5f, -29.5f, -41, 21, 14, 17, 3);
            foreach (float x in new[] { -18f, 2f, 20f }) IndustrialDoor(g, new Vector3(x, .1f, -27.8f), Vector3.back, 8, 10, 3, true);
            IndustrialDoor(g, new Vector3(-39, .05f, 20.22f), Vector3.forward, 1.3f, 2.5f, 3);
            IndustrialDoor(g, new Vector3(29.99f, .05f, 20), Vector3.right, 1.3f, 2.5f, 3);
            // Break down the tall process tower on the rear and sides, not just its front face.
            foreach (float x in new[] { 8f, 28f })
                IndustrialFacade(g, new Vector3(x, 32, -3.5f), new Vector3(x, 32, 19.5f), x < 10 ? Vector3.left : Vector3.right, 14, 3, 9, 7);
            IndustrialFacade(g, new Vector3(8, 32, 19.55f), new Vector3(28, 32, 19.55f), Vector3.forward, 14, 3, 9, 7);
            foreach (float z in new[] { -17f, 0f, 17f })
            {
                g.Box(new Vector3(0, 37.65f, z), new Vector3(5.4f, 1.3f, 6.5f), metal, .12f);
                IndustrialVent(g, new Vector3(-2.75f, 37.65f, z), Vector3.left, 5.8f, .85f, 3);
                IndustrialVent(g, new Vector3(2.75f, 37.65f, z), Vector3.right, 5.8f, .85f, 3);
            }
            foreach (float x in new[] { -16f, -4f, 8f })
            {
                IndustrialFlange(g, new Vector3(x, 40.5f, 0), Vector3.up, 1.4f, zinc);
                IndustrialFlange(g, new Vector3(x, 41, 12), Vector3.forward, 1.4f, zinc);
                foreach (float z in new[] { 7f, 20f }) { g.IBeam(new Vector3(x, 35, z), new Vector3(x, 39.7f, z), .3f, .4f, metal); g.Box(new Vector3(x, 39.7f, z), new Vector3(2.3f, .25f, 1), metal, .05f); }
            }
            IndustrialRail(g, new Vector3(-23.8f, 20.2f, 31.3f), new Vector3(23.8f, 20.2f, 31.3f), metal, ThemeMaterial(3, "warning", 0));
            IndustrialLadder(g, new Vector3(23, .2f, 31.6f), 21.2f, zinc);
            g.Finish();

            root = Existing("B05"); g = new IndustrialGeometry(root, "Industrial B05 rolling mill all faces and roof", root.localToWorldMatrix);
            metal = ThemeMaterial(4, "metal", 0); zinc = ThemeMaterial(4, "galvanized", 0);
            foreach (float x in new[] { -30.15f, 30.15f })
            {
                var outward = x < 0 ? Vector3.left : Vector3.right;
                IndustrialFacade(g, new Vector3(x, 0, -55), new Vector3(x, 0, 55), outward, 15, 4, -1, 11);
                for (int i = 0; i < 10; i++) IndustrialWindow(g, new Vector3(x, 12, -49 + i * 11), outward, 7.7f, 1.6f, 4);
            }
            foreach (float z in new[] { -55.24f, 55.05f }) IndustrialFacade(g, new Vector3(-30, 0, z), new Vector3(30, 0, z), z < 0 ? Vector3.back : Vector3.forward, 15, 4, 12.3f, 10);
            foreach (float x in new[] { -20f, 0f, 20f })
            {
                IndustrialDoor(g, new Vector3(x, .06f, -55.25f), Vector3.back, 8, 8, 4, true);
                IndustrialDoor(g, new Vector3(x, .06f, 55.2f), Vector3.forward, 7, 7, 4, true);
                IndustrialRoof(g, x - 10.4f, x + 10.4f, -56, 56, 15, 19, 4);
                foreach (float side in new[] { -1f, 1f })
                    for (float z = -42; z <= 42; z += 14) IndustrialVent(g, new Vector3(x + side * 2.1f, 19.55f, z), Vector3.right * side, 10.5f, 1.15f, 4);
            }
            IndustrialFacade(g, new Vector3(42.02f, 0, -52.5f), new Vector3(42.02f, 0, 2.5f), Vector3.right, 8, 4, 5.8f, 8);
            IndustrialRoof(g, 29.75f, 42.25f, -53, 3, 8, 10, 4);
            IndustrialDoor(g, new Vector3(42.2f, .06f, -15), Vector3.right, 1.25f, 2.4f, 4);
            IndustrialDoor(g, new Vector3(-30.32f, .06f, 45), Vector3.left, 1.25f, 2.4f, 4);
            foreach (float z in new[] { -40f, -15f, 9f, 35f })
            {
                g.Pipe(new[] { new Vector3(-29, 4, z), new Vector3(-31.2f, 4, z), new Vector3(-31.2f, 1.3f, z + 4) }, .2f, zinc);
                g.Box(new Vector3(-31.1f, .3f, z + 4), new Vector3(1.7f, .6f, 1.8f), ThemeMaterial(4, "concrete", 0), .04f);
            }
            g.Finish();
        }

        static void CompleteFurnaceAndCleaning()
        {
            var root = Existing("B08"); var g = new IndustrialGeometry(root, "Industrial B08 furnace vessel and connected service", root.localToWorldMatrix);
            var metal = ThemeMaterial(7, "metal", 0); var zinc = ThemeMaterial(7, "galvanized", 0); var warn = ThemeMaterial(7, "warning", 0);
            IndustrialFacade(g, new Vector3(-27.5f, 0, -45.02f), new Vector3(27.5f, 0, -45.02f), Vector3.back, 14, 7, 10.8f, 8);
            foreach (float x in new[] { -27.52f, 27.52f }) IndustrialFacade(g, new Vector3(x, 0, -45), new Vector3(x, 0, -19), x < 0 ? Vector3.left : Vector3.right, 14, 7, 10.7f, 7);
            IndustrialRoof(g, -28.5f, 28.5f, -46, -18, 14, 18, 7);
            foreach (float x in new[] { -17f, 4f, 20f }) IndustrialDoor(g, new Vector3(x, .05f, -45.22f), Vector3.back, x == 4 ? 8 : 5, 8.5f, 7, true);
            IndustrialDoor(g, new Vector3(27.7f, .05f, -24), Vector3.right, 1.05f, 1.9f, 7);
            foreach (float y in new[] { 6f, 16f, 26f, 36f, 46f })
            {
                float r = 13 - Mathf.Max(0, y - 30) * .2f;
                g.Ring(new Vector3(0, y, 0), Vector3.up, r + .11f, r + .015f, .22f, metal, 64);
            }
            foreach (float y in new[] { 13.2f, 30.2f, 47.2f })
            {
                foreach (float z in new[] { -14.8f, 14.8f }) IndustrialRail(g, new Vector3(-14.8f, y, z), new Vector3(14.8f, y, z), metal, warn);
                IndustrialRail(g, new Vector3(-14.8f, y, -14.8f), new Vector3(-14.8f, y, 14.8f), metal, warn);
                IndustrialRail(g, new Vector3(14.8f, y, -14.8f), new Vector3(14.8f, y, -4), metal, warn);
                IndustrialRail(g, new Vector3(14.8f, y, 4), new Vector3(14.8f, y, 14.8f), metal, warn);
                foreach (float z in new[] { -13.5f, 13.5f })
                {
                    g.IBeam(new Vector3(-13.5f, y - .5f, z), new Vector3(13.5f, y - .5f, z), .32f, .5f, metal);
                    g.Box(new Vector3(-13.5f, y - .2f, z), new Vector3(.9f, .7f, .9f), metal, .08f);
                    g.Box(new Vector3(13.5f, y - .2f, z), new Vector3(.9f, .7f, .9f), metal, .08f);
                }
            }
            // A landing ladder with cage and actual level access, safely behind the main arena.
            IndustrialLadder(g, new Vector3(16.1f, .2f, 0), 48.1f, zinc);
            for (float y = 2.8f; y < 47; y += 1.4f) g.Ring(new Vector3(16.1f, y, -.45f), Vector3.up, .68f, .63f, .06f, metal, 16);
            foreach (float y in new[] { 6.6f, 13.2f, 21.5f, 30.2f, 38.5f, 47.2f })
            {
                g.Box(new Vector3(15.7f, y, 1), new Vector3(2.8f, .18f, 2.6f), metal, .03f);
                g.Bar(new Vector3(13.5f, y - 1.7f, 1), new Vector3(17, y, 1), .17f, .22f, metal);
                IndustrialRail(g, new Vector3(17, y + .1f, 0), new Vector3(17, y + .1f, 2.3f), metal, warn);
                IndustrialRail(g, new Vector3(14.3f, y + .1f, 2.3f), new Vector3(17, y + .1f, 2.3f), metal, warn);
            }
            foreach (float z in new[] { -19f, 0f, 19f })
            {
                for (float y = 7; y < 42; y += 8) g.Ring(new Vector3(-29, y, z), Vector3.up, 5.48f, 5.39f, .18f, metal, 40);
                IndustrialFlange(g, new Vector3(-19, 35, z), Vector3.right, 1.6f, zinc);
                if (Mathf.Abs(z) > 1)
                    g.Pipe(new[] { new Vector3(-10, 35, z), new Vector3(-7, 35, z), new Vector3(-7, 35, Mathf.Sign(z) * 7.5f) }, 1.6f, metal);
                g.IBeam(new Vector3(-19, .5f, z), new Vector3(-19, 33.5f, z), .4f, .5f, metal);
                g.Box(new Vector3(-19, .3f, z), new Vector3(2, .6f, 2), ThemeMaterial(7, "concrete", 0), .06f);
                g.Pipe(new[] { new Vector3(-29, 4.5f, z), new Vector3(-36.3f, 4.5f, z), new Vector3(-36.3f, 2, -19) }, .35f, zinc);
            }
            // The source downcomer stopped in free air at (26,26,18). Route it into a dust catcher.
            // The catcher is west of the charge-conveyor columns (x28..36), with real ground supports.
            g.Pipe(new[] { new Vector3(26, 26, 18), new Vector3(26, 23, 19.4f), new Vector3(21, 23, 19.4f), new Vector3(21, 20.8f, 19.4f) }, 2, metal);
            IndustrialFlange(g, new Vector3(26, 26, 18), new Vector3(0, -38, 18).normalized, 2, zinc);
            g.Rod(new Vector3(21, 6, 19.4f), new Vector3(21, 21, 19.4f), 3.2f, metal, 48);
            g.Frustum(new Vector3(21, 3, 19.4f), new Vector3(21, 6, 19.4f), 1.1f, 3.2f, metal, 40);
            g.Rod(new Vector3(21, 1.8f, 19.4f), new Vector3(21, 3, 19.4f), .65f, zinc, 24);
            g.Box(new Vector3(21, .9f, 19.4f), new Vector3(3.3f, 1.8f, 3.3f), ThemeMaterial(7, "paint", 1), .12f);
            foreach (float y in new[] { 6f, 13f, 20.7f }) g.Ring(new Vector3(21, y, 19.4f), Vector3.up, 3.3f, 3.18f, .2f, zinc, 40);
            foreach (float x in new[] { 18.6f, 23.4f }) foreach (float z in new[] { 17f, 21.8f })
            {
                g.IBeam(new Vector3(x, .5f, z), new Vector3(x, 6.5f, z), .28f, .35f, metal);
                g.Box(new Vector3(x, .25f, z), new Vector3(1.4f, .5f, 1.4f), ThemeMaterial(7, "concrete", 0), .05f);
            }
            // The inclined charge conveyor gains support rings, access rails and a receiving pit.
            var conveyorBottom = new Vector3(42, 5, 32); var conveyorTop = new Vector3(0, 65, 0);
            foreach (float y in new[] { .25f, .65f })
                g.Bar(Vector3.Lerp(conveyorBottom, conveyorTop, y) + new Vector3(-1, -.5f, 0), Vector3.Lerp(conveyorBottom, conveyorTop, y) + new Vector3(1, -.5f, 0), .22f, .35f, metal);
            foreach (float s in new[] { -1f, 1f }) g.Bar(conveyorBottom + new Vector3(s * 1.4f, 1.3f, 0), conveyorTop + new Vector3(s * 1.4f, 1.3f, 0), .09f, .09f, warn);
            g.Box(new Vector3(40, 1.1f, 30), new Vector3(6, 2.2f, 6), ThemeMaterial(7, "concrete", 0), .1f);
            g.Box(new Vector3(40, 2.3f, 30), new Vector3(4.8f, .25f, 4.8f), metal, .04f);
            g.Finish();
            // World metre canopy joins the existing FURNACE08 sign location to the building face.
            var canopy = new IndustrialGeometry(root, "Industrial B08 front emergency service canopy");
            canopy.Box(new Vector3(100, 12, 664.15f), new Vector3(16, .35f, 4), ThemeMaterial(7, "roof", 0), .06f);
            foreach (float x in new[] { 92.5f, 107.5f })
            { canopy.IBeam(new Vector3(x, .4f, 662.8f), new Vector3(x, 12, 662.8f), .35f, .45f, metal); canopy.Box(new Vector3(x, .25f, 662.8f), new Vector3(1.4f, .5f, 1.4f), agedConcrete, .05f); }
            canopy.Box(new Vector3(100, 11.3f, 662.12f), new Vector3(15.6f, 1.2f, .18f), metal, .035f);
            canopy.Finish();

            root = Existing("B09"); g = new IndustrialGeometry(root, "Industrial B09 scrubber filtration connections", root.localToWorldMatrix);
            metal = ThemeMaterial(8, "metal", 0); zinc = ThemeMaterial(8, "galvanized", 0); warn = ThemeMaterial(8, "warning", 0);
            foreach (float x in new[] { -19f, 0f, 19f })
            {
                foreach (float y in new[] { 7f, 14f, 21f, 27.4f }) g.Ring(new Vector3(x, y, 0), Vector3.up, 6.08f, 5.99f, .16f, metal, 48);
                IndustrialFlange(g, new Vector3(x, 26, 9), Vector3.forward, 1.2f, zinc);
                IndustrialFlange(g, new Vector3(x, 26, 15.5f), Vector3.forward, 1.2f, zinc);
                // Lower return loop and service valve relate the rear filter boxes to the vessels.
                g.Pipe(new[] { new Vector3(x, 5, 5.8f), new Vector3(x, 5, 12), new Vector3(x, 9, 12), new Vector3(x, 9, 16) }, .42f, zinc);
                IndustrialFlange(g, new Vector3(x, 5, 9), Vector3.forward, .42f, metal);
                g.Ring(new Vector3(x, 6.1f, 10), Vector3.up, .48f, .4f, .06f, warn, 20);
                g.Rod(new Vector3(x, 5, 10), new Vector3(x, 6.1f, 10), .065f, metal);
                foreach (float side in new[] { -1f, 1f })
                    IndustrialFacade(g, new Vector3(x + side * 6.05f, 7, 15.5f), new Vector3(x + side * 6.05f, 7, 28.5f), Vector3.right * side, 14, 8, -1, 6);
                IndustrialFacade(g, new Vector3(x - 6, 7, 28.55f), new Vector3(x + 6, 7, 28.55f), Vector3.forward, 14, 8);
                IndustrialVent(g, new Vector3(x, 18, 28.8f), Vector3.forward, 7.5f, 3.2f, 8);
                g.Box(new Vector3(x, 8.1f, 29.5f), new Vector3(11, .22f, 2), metal, .025f);
                IndustrialRail(g, new Vector3(x - 5.4f, 8.25f, 30.4f), new Vector3(x + 5.4f, 8.25f, 30.4f), metal, warn);
                foreach (float dx in new[] { -4.5f, 4.5f }) g.Bar(new Vector3(x + dx, 5.8f, 27.5f), new Vector3(x + dx, 8, 30), .18f, .24f, metal);
                IndustrialLadder(g, new Vector3(x - 5, .1f, 29.4f), 9.2f, zinc);
                IndustrialDoor(g, new Vector3(x, 8.25f, 28.72f), Vector3.forward, 1.25f, 2.4f, 8);
                g.Rod(new Vector3(x - 5.5f, 29.8f, -2), new Vector3(x - 5.5f, 30.65f, -2), .08f, metal);
            }
            foreach (float z in new[] { -1.4f, 1.4f }) IndustrialRail(g, new Vector3(-26, 30.65f, z), new Vector3(26, 30.65f, z), metal, warn);
            IndustrialLadder(g, new Vector3(-26.4f, .1f, 0), 31.6f, zinc);
            for (float y = 3; y < 30; y += 1.5f) g.Ring(new Vector3(-26.4f, y, -.4f), Vector3.up, .68f, .63f, .06f, metal, 16);
            foreach (float x in new[] { -26f, 26f }) g.Box(new Vector3(x, .25f, 0), new Vector3(1.8f, .5f, 1.8f), ThemeMaterial(8, "concrete", 0), .05f);
            g.Finish();
        }
    }
}
