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
        const string PhotorealEdgeRootName = "Photoreal Z1 ground connections";

        // Additive static contact geometry, called after ReconnectWideScene. The caller owns
        // scene saving, lighting and capture. Never changes traversal colliders or other roots.
        public static void BuildPhotorealZ1Edges()
        {
            if (structures == null) throw new InvalidOperationException("ReconnectWideScene must run before BuildPhotorealZ1Edges.");
            var b02 = Existing("B02"); var b03 = Existing("B03");
            var foundation = b02.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Foundation");
            if (foundation == null) throw new InvalidOperationException("B02 foundation is required for grounded contact geometry.");
            var concrete = PhotorealEdgeSource("GroundEdge_weathered curb concrete");
            var aggregate = PhotorealEdgeSource("GroundEdge_compacted shoulder aggregate");
            var fractured = PhotorealEdgeSource("GroundEdge_exposed concrete aggregate");
            var asphaltEdge = PhotorealEdgeSource("GroundEdge_fractured asphalt binder");
            var iron = PhotorealEdgeSource("GroundEdge_cast iron storm grates");
            var recess = PhotorealEdgeSource("GroundEdge_recess_and_silt");
            var old = structures.Find(PhotorealEdgeRootName);
            if (old != null) Obj.DestroyImmediate(old.gameObject);
            var owned = Group(PhotorealEdgeRootName, structures);
            // Assemblers use world-space metre vertices. Keep the owned root untransformed.
            owned.SetPositionAndRotation(Vector3.zero, Quaternion.identity); owned.localScale = Vector3.one;

            var ties = new GroundEdgeGeometry(owned, "Photoreal Z1 pavement connections");
            // B02 apron y=-.055 meets the existing Z1 y=0 slab at x=-60. This is a
            // chamfered seam, not another independent floor or a barrier to the service aisle.
            PhotorealJoinStrip(ties, new Vector3(-60.80f, -.043f, 9.2f), new Vector3(-60.80f, -.043f, 84.3f),
                Vector3.right, .90f, .012f, concrete, fractured, 473, .026f);

            // The departure slab ends at x=60 but B03's apron starts at x=62. Existing
            // aggregate remains the drainage strip; only two real service crossings span it.
            foreach (var interval in new[] { new Vector2(28.8f, 39.5f), new Vector2(66.4f, 75.4f) })
                PhotorealJoinStrip(ties, new Vector3(59.84f, .018f, interval.x), new Vector3(59.84f, .018f, interval.y),
                    Vector3.right, 2.32f, -.032f, concrete, fractured, 701 + (int)interval.x, .09f);

            // The delivery road overlaps the western apron and ends at x=-131,y=.025.
            // Dress that actual endpoint, not the hidden nominal apron edge at x=-132.
            PhotorealJoinStrip(ties, new Vector3(-131.04f, .045f, 40.2f), new Vector3(-131.04f, .045f, 47.8f),
                Vector3.right, .90f, -.043f, asphaltEdge, aggregate, 1091, .055f);
            ties.Finish();

            var drainage = new GroundEdgeGeometry(owned, "Photoreal Z1 pump service drainage");
            // The lower 2m seam is kept open away from the two crossings. Covers have
            // real bars and a dark bed above the uncut old ground, not claimed terrain cuts.
            foreach (var interval in new[] { new Vector2(15.0f, 27.9f), new Vector2(40.4f, 65.5f), new Vector2(76.3f, 83.8f) })
            {
                PhotorealAggregateRibbon(drainage, new Vector3(60.13f, -.029f, interval.x),
                    new Vector3(60.13f, -.029f, interval.y), Vector3.right, 1.71f, aggregate, 1327 + (int)interval.x);
                // A shallow dressed channel lies next to the transit lane, never across it.
                PhotorealShallowGutter(drainage, new Vector3(61.16f, -.017f, interval.x + .1f),
                    new Vector3(61.16f, -.017f, interval.y - .1f), .46f, concrete, recess);
            }
            foreach (float z in new[] { 28.25f, 40.0f, 66.0f, 75.85f })
                GroundEdgeCoveredChannel(drainage, new Vector3(60.99f, -.022f, z - .27f),
                    new Vector3(60.99f, -.022f, z + .27f), .48f, iron, recess, concrete);
            // Short connections end at the previously authored x=121 collector. The pump
            // frontage and existing pipe/nozzle area x[106,110],z[38,54] remain untouched.
            foreach (float z in new[] { 20.5f, 72.0f })
            {
                GroundEdgeCoveredChannel(drainage, new Vector3(117.0f, -.055f, z),
                    new Vector3(120.40f, -.055f, z), .34f, iron, recess, concrete);
                GroundEdgeCatchpit(drainage, new Vector3(120.35f, -.055f, z), .72f, iron, recess, concrete);
            }
            drainage.Finish();

            var margins = new GroundEdgeGeometry(owned, "Photoreal Z1 selected broken outer margins");
            // Selected outer edges only: no new tiled patch field. The existing flat apron
            // remains intact; these are chipped outer lips and lower aggregate shoulders.
            PhotorealBrokenMargin(margins, new Vector3(-128, -.055f, 86), new Vector3(-84, -.055f, 86),
                Vector3.forward, concrete, fractured, aggregate, 2081);
            PhotorealBrokenMargin(margins, new Vector3(66, -.055f, 13), new Vector3(114, -.055f, 13),
                Vector3.back, concrete, fractured, aggregate, 2713);
            margins.Finish();

            var footings = new GroundEdgeGeometry(owned, "Photoreal Z1 building base contacts");
            var bounds = foundation.bounds;
            // B02's front personnel stairs at x=-74.4 and east internal walkway stay open.
            // Exposed footing extends only 0.38m from the actual placed foundation face.
            PhotorealFooting(footings, new Vector3(bounds.min.x + .45f, -.055f, bounds.min.z - .018f),
                new Vector3(Mathf.Min(bounds.max.x - .5f, -77.2f), -.055f, bounds.min.z - .018f),
                Vector3.back, concrete, fractured, 3181);
            PhotorealFooting(footings, new Vector3(bounds.max.x + .018f, -.055f, bounds.min.z + 1.0f),
                new Vector3(bounds.max.x + .018f, -.055f, Mathf.Min(bounds.max.z - .4f, 47.7f)),
                Vector3.right, concrete, fractured, 3467);

            // B03 endpoints follow the same source transform as CompletePumpBuilding.
            // Split at the south and west doors; do not make a raised sill across entrances.
            foreach (var span in new[] { new Vector2(-17.6f, -11.0f), new Vector2(-6.8f, 17.5f) })
                PhotorealPumpFooting(footings, b03, new Vector3(span.x, 0, -13.30f),
                    new Vector3(span.y, 0, -13.30f), Vector3.back, concrete, fractured, 3701 + (int)span.x);
            foreach (var span in new[] { new Vector2(-12.6f, -10.4f), new Vector2(-7.1f, 14.5f) })
                PhotorealPumpFooting(footings, b03, new Vector3(-18.30f, 0, span.x),
                    new Vector3(-18.30f, 0, span.y), Vector3.left, concrete, fractured, 3911 + (int)span.x);
            footings.Finish();

            var filters = owned.GetComponentsInChildren<MeshFilter>();
            foreach (var renderer in owned.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            foreach (var filter in filters)
            {
                if (!AssetDatabase.Contains(filter.sharedMesh)) throw new InvalidOperationException("Photoreal edge mesh did not persist as an asset.");
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var p = filter.transform.TransformPoint(vertex);
                    if (float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y)
                        || float.IsNaN(p.z) || float.IsInfinity(p.z)) throw new InvalidOperationException("Nonfinite photoreal edge vertex.");
                    if (p.x > -22 && p.x < 22 && p.z > -2 && p.z < 180)
                        throw new InvalidOperationException("Photoreal edges entered protected central travel space.");
                }
            }
            if (owned.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Photoreal contact dressing must not create traversal colliders.");
            Debug.Log("PHOTOREAL_Z1_EDGES_READY renderers=" + filters.Length
                + " vertices=" + filters.Sum(f => f.sharedMesh.vertexCount)
                + " triangles=" + filters.Sum(f => f.sharedMesh.triangles.Length / 3)
                + " colliders=0 central_route_unchanged=true existing_materials_unchanged=true");
        }

        static Material PhotorealEdgeSource(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Base + "Materials/" + name + ".mat");
            if (material == null) throw new InvalidOperationException("Photoreal edge source missing: " + name);
            return material;
        }

        static void PhotorealJoinStrip(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward,
            float width, float farHeight, Material top, Material edge, int seed, float irregularity)
        {
            var rng = new System.Random(seed); float length = Vector3.Distance(a, b);
            int count = Mathf.Max(2, Mathf.CeilToInt(length / .82f));
            var near = new Vector3[count + 1]; var far = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)count);
                float fade = Mathf.Min(1, Mathf.Min(i, count - i) * .5f);
                near[i] = p + outward * (((float)rng.NextDouble() - .5f) * irregularity * fade);
                far[i] = p + outward * (width + ((float)rng.NextDouble() - .5f) * irregularity * fade);
                far[i].y = farHeight;
            }
            for (int i = 0; i < count; i++)
                g.Prism(new[] { near[i], near[i + 1], far[i + 1], far[i] }, Mathf.Min(a.y, farHeight) - .045f, top, edge);
        }

        static void PhotorealAggregateRibbon(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward,
            float width, Material material, int seed)
        {
            var rng = new System.Random(seed); int count = Mathf.CeilToInt(Vector3.Distance(a, b) / .7f);
            var left = new Vector3[count + 1]; var mid = new Vector3[count + 1]; var right = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)count);
                left[i] = p + outward * ((float)rng.NextDouble() * .045f);
                mid[i] = p + outward * width * .47f - Vector3.up * (.006f + (float)rng.NextDouble() * .008f);
                right[i] = p + outward * (width - (float)rng.NextDouble() * .045f) - Vector3.up * .009f;
            }
            for (int i = 0; i < count; i++)
            {
                g.Face(material, Vector3.up, left[i], left[i + 1], mid[i + 1], mid[i]);
                g.Face(material, Vector3.up, mid[i], mid[i + 1], right[i + 1], right[i]);
                // A few 3–8cm stones sit in the aggregate, avoiding the channel centre.
                if (i % 3 == 1)
                {
                    float w = i % 2 == 0 ? .25f : 1.42f;
                    var p = Vector3.Lerp(a, b, (i + .43f) / count) + outward * w;
                    p.y -= .014f;
                    GroundEdgeChip(g, p, .035f + (float)rng.NextDouble() * .045f, .047f, .025f, i * .57f, material);
                }
            }
        }

        static void PhotorealShallowGutter(GroundEdgeGeometry g, Vector3 a, Vector3 b,
            float width, Material concrete, Material darkBed)
        {
            var along = (b - a).normalized; var right = Vector3.Cross(Vector3.up, along);
            // The bed stays above the already authored aggregate. This narrow channel is
            // an additive surface fitting; cutting/draining the terrain is outside this pass.
            g.Bar(a - Vector3.up * .004f, b - Vector3.up * .004f, width * .58f, .014f, darkBed);
            foreach (int sign in new[] { -1, 1 })
            {
                var inner = right * sign * width * .29f; var outer = right * sign * width * .64f;
                g.Face(concrete, Vector3.up, a + inner, b + inner, b + outer + Vector3.up * .032f, a + outer + Vector3.up * .032f);
                g.Bar(a + outer + Vector3.up * .020f, b + outer + Vector3.up * .020f, .06f, .042f, concrete);
            }
        }

        static void PhotorealBrokenMargin(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward,
            Material concrete, Material fractured, Material aggregate, int seed)
        {
            var rng = new System.Random(seed); var along = (b - a).normalized;
            int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 1.35f); float step = Vector3.Distance(a, b) / count;
            for (int i = 0; i < count; i++)
            {
                var p = a + along * (i * step); var next = p + along * step;
                float lip = .07f + (float)rng.NextDouble() * .19f;
                var top = new[] { p - outward * .12f + Vector3.up * .026f,
                    p + outward * lip + Vector3.up * .018f,
                    p + along * step * .46f + outward * lip * .72f + Vector3.up * .022f,
                    next + outward * lip * .87f + Vector3.up * .021f,
                    next - outward * .12f + Vector3.up * .026f };
                g.Prism(top, a.y - .082f, concrete, fractured);
                // Close the exposed edge to the old shoulder with a real sloping section.
                g.Face(aggregate, Vector3.up, p + outward * lip + Vector3.up * .010f,
                    next + outward * lip * .87f + Vector3.up * .010f,
                    next + outward * .92f - Vector3.up * .004f,
                    p + outward * .92f - Vector3.up * .004f);
                if (i % 4 != 1) continue;
                var chip = p + along * step * .61f + outward * .54f; chip.y -= .022f;
                GroundEdgeChip(g, chip, .09f, .065f, .032f, i * .41f, fractured);
            }
        }

        static void PhotorealPumpFooting(GroundEdgeGeometry g, Transform root, Vector3 localA, Vector3 localB,
            Vector3 outward, Material concrete, Material fractured, int seed)
        {
            var a = root.TransformPoint(localA); var b = root.TransformPoint(localB); a.y = b.y = -.055f;
            PhotorealFooting(g, a, b, root.TransformDirection(outward).normalized, concrete, fractured, seed);
        }

        static void PhotorealFooting(GroundEdgeGeometry g, Vector3 a, Vector3 b, Vector3 outward,
            Material concrete, Material fractured, int seed)
        {
            var rng = new System.Random(seed); float length = Vector3.Distance(a, b);
            if (length < .2f) return;
            int count = Mathf.CeilToInt(length / 1.1f);
            var wall = new Vector3[count + 1]; var toe = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)count);
                wall[i] = p + Vector3.up * (.19f + (float)rng.NextDouble() * .065f);
                toe[i] = p + outward * (.24f + (float)rng.NextDouble() * .10f) + Vector3.up * .018f;
            }
            for (int i = 0; i < count; i++)
            {
                g.Face(concrete, Vector3.up, wall[i], wall[i + 1], toe[i + 1], toe[i]);
                g.Face(fractured, outward, toe[i], toe[i + 1], toe[i + 1] - Vector3.up * .065f, toe[i] - Vector3.up * .065f);
            }
        }
    }
}
