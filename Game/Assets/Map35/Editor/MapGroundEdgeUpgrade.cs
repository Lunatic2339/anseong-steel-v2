using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Local contact geometry complements PurposeGround's large/mid-scale wear masks.
        // This pass is safe both during GenerateWide and after reopening the saved scene.
        // It neither replaces physics floors nor adds obstacles to the 32m connector.
        static void ApplyGroundEdgeUpgrade()
        {
            if (structures == null)
            {
                var found = GameObject.Find("01 Buildings and traversable ground");
                if (found == null) throw new InvalidOperationException("Ground edge pass needs the wide steelworks scene.");
                structures = found.transform;
            }
            var owned = ArchitectureGroup("Ground edge completion Z1", structures);
            var curb = GroundEdgeMaterial("weathered curb concrete", "Wide_Palette_B02_concrete", new Color(.70f, .68f, .62f));
            var fracture = GroundEdgeMaterial("exposed concrete aggregate", "Wide_Palette_B02_concrete", new Color(.78f, .75f, .66f));
            var iron = GroundEdgeMaterial("cast iron storm grates", "Wide_Palette_B02_metal", new Color(.27f, .29f, .28f));
            var asphaltEdge = GroundEdgeMaterial("fractured asphalt binder", "Landscape_surface_weathered aggregate asphalt", new Color(.53f, .53f, .50f));
            var aggregate = GroundEdgeMaterial("compacted shoulder aggregate", "Landscape_surface_dark compacted slag gravel", new Color(.86f, .80f, .69f));
            var recess = Material("GroundEdge_recess_and_silt", new Color(.065f, .071f, .064f));
            recess.SetFloat("_Glossiness", .18f);

            var maintenance = new GroundEdgeGeometry(owned, "B02 drainage confluence");
            // The older transverse drain ends at x=-66; this joins it to x=-62 runoff.
            // The inset is only 22mm above apron (-.055), below its removable covers.
            GroundEdgeCoveredChannel(maintenance, new Vector3(-66, -.055f, 24), new Vector3(-62, -.055f, 24), .44f, iron, recess, curb);
            GroundEdgeCatchpit(maintenance, new Vector3(-62, -.055f, 24), 1.30f, iron, recess, curb);
            GroundEdgeCatchpit(maintenance, new Vector3(-62, -.055f, 70), 1.06f, iron, recess, curb);
            // Small slab joint collars keep the collector connected to the existing channel.
            GroundEdgeCollectorNeck(maintenance, new Vector3(-62, -.055f, 24.67f), new Vector3(-62, -.055f, 26.1f), .61f, curb, recess);
            GroundEdgeCollectorNeck(maintenance, new Vector3(-62, -.055f, 22.0f), new Vector3(-62, -.055f, 23.33f), .61f, curb, recess);
            GroundEdgeCurbRun(maintenance, new Vector3(-130, -.055f, 7.84f), new Vector3(-82, -.055f, 7.84f), Vector3.back, curb, fracture, 271);
            GroundEdgeAggregateBand(maintenance, new Vector3(-130, -.055f, 7.69f), new Vector3(-82, -.055f, 7.69f), Vector3.back, 1.8f, aggregate, 733, -.13f);
            GroundEdgeStoneScatter(maintenance, new Vector3(-128, -.07f, 7.35f), new Vector3(-85, -.07f, 7.35f), Vector3.back, 1.2f, 40, aggregate, fracture, 509);
            maintenance.Finish();

            var departure = new GroundEdgeGeometry(owned, "Departure apron northern edges");
            // Keep x[-22,+22] clear: the 32m road plus 6m additional shoulder on each side.
            foreach (int sign in new[] { -1, 1 })
            {
                float x0 = sign < 0 ? -59 : 22, x1 = sign < 0 ? -22 : 59;
                var a = new Vector3(x0, 0, 100.20f); var b = new Vector3(x1, 0, 100.20f);
                GroundEdgeCurbRun(departure, a, b, Vector3.forward, curb, fracture, 411 + sign * 23);
                GroundEdgeAggregateBand(departure, a + Vector3.forward * .17f, b + Vector3.forward * .17f,
                    Vector3.forward, 2.15f, aggregate, 821 + sign * 19, -.18f);
                GroundEdgeStoneScatter(departure, a + Vector3.forward * .45f - Vector3.up * .035f,
                    b + Vector3.forward * .45f - Vector3.up * .035f, Vector3.forward, 1.4f, 34, aggregate, fracture, 1123 + sign * 7);
                // Flush inlet hardware is on the slab, not a barrier across the travel lane.
                GroundEdgeCatchpit(departure, new Vector3(sign * 28, 0, 98.8f), 1.22f, iron, recess, curb);
                GroundEdgeCoveredChannel(departure, new Vector3(sign * 28, 0, 99.45f),
                    new Vector3(sign * 28, 0, 100.12f), .36f, iron, recess, curb);
            }
            departure.Finish();

            var connector = new GroundEdgeGeometry(owned, "Departure connector broken shoulders");
            // Surface road top=.06. East-side geometry stops before the existing drain at x=18.5.
            foreach (int sign in new[] { -1, 1 })
            {
                var outward = Vector3.right * sign;
                var start = new Vector3(sign * 16, .06f, 104);
                var end = new Vector3(sign * 16, .06f, 176);
                GroundEdgeAggregateBand(connector, start + outward * .015f, end + outward * .015f,
                    outward, 1.77f, aggregate, 1201 + sign * 17, -.095f);
                GroundEdgeAsphaltLip(connector, start, end, outward, asphaltEdge, aggregate, 1421 + sign * 23);
                GroundEdgeStoneScatter(connector, start + outward * .55f - Vector3.up * .025f,
                    end + outward * .55f - Vector3.up * .025f, outward, .95f, 62, aggregate, asphaltEdge, 1693 + sign * 13);
            }
            connector.Finish();

            // Geometry is visual contact dressing only; the existing stable traversal stays intact.
            foreach (var renderer in owned.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            if (owned.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Ground edge decoration unexpectedly acquired a gameplay collider.");
            foreach (var filter in owned.GetComponentsInChildren<MeshFilter>())
                foreach (var v in filter.sharedMesh.vertices)
                    if (v.x > -15.95f && v.x < 15.95f && v.z >= 0 && v.z <= 180)
                        throw new InvalidOperationException("Ground edge geometry entered the protected central route.");
            Debug.Log("GROUND_EDGE_UPGRADE_OK renderers=" + owned.GetComponentsInChildren<MeshRenderer>().Length
                + " vertices=" + owned.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.vertexCount)
                + " colliders=0 central_route_unchanged=true");
        }

        static Material GroundEdgeMaterial(string suffix, string sourceName, Color tint)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Base + "Materials/" + sourceName + ".mat");
            if (source == null) throw new InvalidOperationException("Ground edge material source missing: " + sourceName);
            var result = Material("GroundEdge_" + suffix, tint);
            result.shader = source.shader; result.CopyPropertiesFromMaterial(source); result.color = tint;
            if (result.HasProperty("_WearAmount")) result.SetFloat("_WearAmount", 0);
            if (result.HasProperty("_GrimeStrength")) result.SetFloat("_GrimeStrength", .12f);
            if (result.HasProperty("_MacroStrength")) result.SetFloat("_MacroStrength", .06f);
            if (result.HasProperty("_SurfaceMix")) result.SetFloat("_SurfaceMix", .12f);
            if (result.HasProperty("_MacroContrast")) result.SetFloat("_MacroContrast", .07f);
            result.enableInstancing = true; EditorUtility.SetDirty(result); return result;
        }

        // The small mesh assembler batches by region/material and carries its own metre UV0.
        // Faces have independent vertices so concrete breaks and gravel remain angular.
        sealed class GroundEdgeGeometry
        {
            sealed class Part
            {
                public readonly List<Vector3> vertices = new List<Vector3>();
                public readonly List<Vector2> uv = new List<Vector2>();
                public readonly List<int> triangles = new List<int>();
            }
            readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();
            readonly Transform parent;
            readonly string name;
            public GroundEdgeGeometry(Transform parent, string name) { this.parent = parent; this.name = name; }
            public void Face(Material material, Vector3 normal, params Vector3[] points)
            {
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0) Array.Reverse(points);
                if (!parts.TryGetValue(material, out var part)) { part = new Part(); parts.Add(material, part); }
                int first = part.vertices.Count;
                Vector3 n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                foreach (var p in points)
                {
                    part.vertices.Add(p);
                    part.uv.Add(n.y >= n.x && n.y >= n.z ? new Vector2(p.x, p.z)
                        : n.x > n.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                }
                for (int i = 1; i < points.Length - 1; i++) part.triangles.AddRange(new[] { first, first + i, first + i + 1 });
            }
            public void Box(Vector3 center, Vector3 size, Material material, Quaternion? rotation = null)
            {
                var h = size * .5f; var q = rotation ?? Quaternion.identity;
                for (int axis = 0; axis < 3; axis++) foreach (int sign in new[] { -1, 1 })
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3; var normal = Vector3.zero; normal[axis] = sign;
                    var points = new Vector3[4]; int i = 0;
                    foreach (var pair in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                    {
                        var p = Vector3.zero; p[axis] = h[axis] * sign; p[u] = h[u] * pair.x; p[v] = h[v] * pair.y;
                        points[i++] = center + q * p;
                    }
                    Face(material, q * normal, points);
                }
            }
            public void Bar(Vector3 a, Vector3 b, float width, float height, Material material)
            {
                var direction = b - a;
                Box((a + b) * .5f, new Vector3(width, height, direction.magnitude), material,
                    Quaternion.LookRotation(direction, Vector3.up));
            }
            public void Prism(Vector3[] top, float bottom, Material surface, Material edge)
            {
                Face(surface, Vector3.up, top);
                var center = top.Aggregate(Vector3.zero, (a, b) => a + b) / top.Length;
                for (int i = 0; i < top.Length; i++)
                {
                    var a = top[i]; var b = top[(i + 1) % top.Length]; var normal = (a + b) * .5f - center; normal.y = 0;
                    Face(edge, normal, a, new Vector3(a.x, bottom, a.z), new Vector3(b.x, bottom, b.z), b);
                }
            }
            public void Finish()
            {
                int index = 0;
                foreach (var pair in parts)
                {
                    var mesh = new Mesh { indexFormat = pair.Value.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(pair.Value.vertices); mesh.SetTriangles(pair.Value.triangles, 0); mesh.SetUVs(0, pair.Value.uv);
                    mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                    var saved = SaveDetailMesh(mesh, "GroundEdge_" + name.Replace(' ', '_') + "_" + index);
                    var child = new GameObject("Ground edge " + name + " material " + index++);
                    child.transform.SetParent(parent, false);
                    child.AddComponent<MeshFilter>().sharedMesh = saved; child.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
                }
            }
        }

        static void GroundEdgeCoveredChannel(GroundEdgeGeometry g, Vector3 a, Vector3 b, float width, Material iron, Material recess, Material concrete)
        {
            var direction = (b - a).normalized; var side = Vector3.Cross(Vector3.up, direction); float length = Vector3.Distance(a, b);
            var inset = Vector3.up * .022f; var frame = Vector3.up * .050f;
            g.Bar(a + inset, b + inset, width, .012f, recess);
            foreach (int sign in new[] { -1, 1 })
            {
                g.Bar(a + side * sign * (width * .5f + .035f) + frame, b + side * sign * (width * .5f + .035f) + frame, .065f, .045f, iron);
                g.Bar(a + side * sign * (width * .5f + .12f) + Vector3.up * .027f,
                    b + side * sign * (width * .5f + .12f) + Vector3.up * .027f, .105f, .045f, concrete);
            }
            int count = Mathf.Max(2, Mathf.CeilToInt(length / .145f));
            for (int i = 0; i <= count; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)count) + Vector3.up * .049f;
                g.Bar(p - side * width * .5f, p + side * width * .5f, .037f, .042f, iron);
            }
            for (float d = 1.2f; d < length; d += 1.2f)
            {
                var p = a + direction * d + frame;
                g.Bar(p - side * (width * .5f + .06f), p + side * (width * .5f + .06f), .065f, .045f, iron);
            }
        }

        static void GroundEdgeCatchpit(GroundEdgeGeometry g, Vector3 p, float size, Material iron, Material recess, Material concrete)
        {
            float half = size * .5f;
            // The dark surface is above the old uncut slab; small actual ribs create the inset depth.
            g.Box(p + Vector3.up * .017f, new Vector3(size - .12f, .018f, size - .12f), recess);
            foreach (int side in new[] { -1, 1 })
            {
                g.Box(p + new Vector3(side * half, .047f, 0), new Vector3(.085f, .066f, size + .085f), iron);
                g.Box(p + new Vector3(0, .047f, side * half), new Vector3(size - .085f, .066f, .085f), iron);
                g.Box(p + new Vector3(side * (half + .105f), .026f, 0), new Vector3(.125f, .052f, size + .29f), concrete);
                g.Box(p + new Vector3(0, .026f, side * (half + .105f)), new Vector3(size + .085f, .052f, .125f), concrete);
            }
            int bars = Mathf.FloorToInt((size - .16f) / .12f);
            for (int i = 0; i <= bars; i++)
                g.Box(p + new Vector3(0, .046f, Mathf.Lerp(-half + .09f, half - .09f, i / (float)bars)),
                    new Vector3(size - .08f, .052f, .035f), iron);
            // Two cross ribs and recessed lifting pockets distinguish a cover from a metal grid decal.
            foreach (float dx in new[] { -.22f, .22f })
                g.Box(p + new Vector3(dx, .036f, 0), new Vector3(.035f, .036f, size - .08f), iron);
        }

        static void GroundEdgeCollectorNeck(GroundEdgeGeometry g, Vector3 a, Vector3 b, float width, Material concrete, Material recess)
        {
            g.Bar(a + Vector3.up * .008f, b + Vector3.up * .008f, width - .13f, .012f, recess);
            foreach (int side in new[] { -1, 1 })
                g.Bar(a + new Vector3(side * width * .5f, .023f, 0), b + new Vector3(side * width * .5f, .023f, 0), .10f, .046f, concrete);
        }

        static void GroundEdgeCurbRun(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward, Material body, Material fracture, int seed)
        {
            var rng = new System.Random(seed); var along = (b - a).normalized; float length = Vector3.Distance(a, b);
            int segments = Mathf.CeilToInt(length / 1.85f); float span = length / segments;
            for (int i = 0; i < segments; i++)
            {
                // Two local failed sections per run, with exposed rough ends instead of a uniform stripe.
                bool broken = i % 11 == 6 || i % 13 == 9;
                if (i % 17 == 12) continue;
                var start = a + along * (i * span + .025f); var end = a + along * ((i + 1) * span - .025f);
                // Leave the northern inlet outlet physically open through the curb line.
                float drainX = a.x < 0 ? -28 : 28;
                if (a.z > 99 && a.z < 101 && start.x < drainX + .45f && end.x > drainX - .45f) continue;
                float height = broken ? .055f : .125f + (float)rng.NextDouble() * .015f;
                float bite = broken ? span * .24f : .045f;
                var top = new[]
                {
                    start - outward * .14f + Vector3.up * height,
                    start + outward * .13f + along * bite + Vector3.up * (height - (broken ? .025f : 0)),
                    end + outward * .14f - along * bite * .8f + Vector3.up * height,
                    end - outward * .14f + Vector3.up * (height - (broken ? .018f : 0))
                };
                g.Prism(top, a.y - .18f, body, broken ? fracture : body);
                if (!broken) continue;
                var center = (start + end) * .5f + outward * .38f;
                GroundEdgeChip(g, center - Vector3.up * .07f, .24f, .13f, .07f, 17 + i * .63f, fracture);
                GroundEdgeChip(g, center + along * .32f + outward * .19f - Vector3.up * .08f, .14f, .10f, .06f, 2 + i * .77f, body);
            }
        }

        static void GroundEdgeAggregateBand(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward, float width, Material mat, int seed, float outerDrop)
        {
            var rng = new System.Random(seed); float length = Vector3.Distance(a, b); int count = Mathf.CeilToInt(length / 1.5f);
            var inner = new List<Vector3>(); var middle = new List<Vector3>(); var outer = new List<Vector3>();
            for (int i = 0; i <= count; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)count);
                float widthHere = width * (.87f + (float)rng.NextDouble() * .13f);
                inner.Add(p + Vector3.up * .006f);
                middle.Add(p + outward * widthHere * .45f + Vector3.up * (-.018f + (float)rng.NextDouble() * .013f));
                outer.Add(p + outward * widthHere + Vector3.up * outerDrop);
            }
            for (int i = 0; i < count; i++)
            {
                g.Face(mat, Vector3.up, inner[i], inner[i + 1], middle[i + 1], middle[i]);
                g.Face(mat, Vector3.up, middle[i], middle[i + 1], outer[i + 1], outer[i]);
            }
        }

        static void GroundEdgeAsphaltLip(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward, Material asphalt, Material aggregate, int seed)
        {
            var rng = new System.Random(seed); var along = (b - a).normalized; float length = Vector3.Distance(a, b);
            int count = Mathf.CeilToInt(length / 1.7f); float span = length / count;
            for (int i = 0; i < count; i++)
            {
                var p0 = a + along * i * span; var p1 = a + along * (i + 1) * span;
                float extension = .08f + (float)rng.NextDouble() * .21f;
                float chip = .02f + (float)rng.NextDouble() * .08f;
                // Inner two centimetres overlap the road edge; the damage stays outside its clear width.
                var top = new[]
                {
                    p0 - outward * .018f + Vector3.up * .006f,
                    p0 + outward * extension + along * chip + Vector3.up * .004f,
                    Vector3.Lerp(p0, p1, .58f) + outward * (extension * .72f) + Vector3.up * .007f,
                    p1 + outward * (extension + chip) - along * chip + Vector3.up * .005f,
                    p1 - outward * .018f + Vector3.up * .006f
                };
                g.Prism(top, a.y - .08f, asphalt, aggregate);
                if (i % 3 == 1)
                    GroundEdgeChip(g, (p0 + p1) * .5f + outward * (.45f + chip) - Vector3.up * .018f,
                        .18f + chip, .13f, .035f, i * .38f, asphalt);
            }
        }

        static void GroundEdgeStoneScatter(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward, float spread, int count, Material stone, Material accent, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float t = (i + (float)rng.NextDouble()) / count;
                float distance = spread * (float)rng.NextDouble();
                var p = Vector3.Lerp(a, b, t) + outward * distance - Vector3.up * (distance * .055f);
                float size = .045f + Mathf.Pow((float)rng.NextDouble(), 2) * .13f;
                GroundEdgeChip(g, p, size, size * (.6f + (float)rng.NextDouble() * .5f), size * .45f,
                    (float)rng.NextDouble() * Mathf.PI * 2, i % 7 == 0 ? accent : stone);
            }
        }

        static void GroundEdgeChip(GroundEdgeGeometry g, Vector3 center, float rx, float rz, float height, float angle, Material material)
        {
            var points = new Vector3[5]; var q = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                float r = i % 2 == 0 ? 1 : .71f;
                points[i] = center + q * new Vector3(Mathf.Cos(a) * rx * r, height * (i == 3 ? .68f : 1), Mathf.Sin(a) * rz * r);
            }
            g.Prism(points, center.y - .025f, material, material);
        }
    }
}
