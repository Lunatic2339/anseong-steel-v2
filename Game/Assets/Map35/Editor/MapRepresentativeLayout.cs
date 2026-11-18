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
        const string RepLayoutMarker = "Representative section layout applied";
        const string RepLayoutRoot = "Representative section split service stock";
        const string RepLayoutMeshFolder = "Assets/Map35/Meshes/RepSection";

        sealed class RepLayoutPart
        {
            public Mesh mesh;
            public int[] sourceVertex;
            public int[][] sourceTriangles;
        }

        // Placement-only trial. The caller owns scene loading, saving, lighting and captures.
        // Original mesh/material assets are never edited. East B03 reel geometry stays in place.
        public static void RepApplyLayout()
        {
            if (structures == null) throw new InvalidOperationException("ReconnectWideScene must precede RepApplyLayout.");
            if (structures.Find(RepLayoutMarker) != null)
            {
                Debug.Log("REP_LAYOUT_ALREADY_APPLIED: no duplicate movement or geometry.");
                return;
            }
            if (structures.Find(RepLayoutRoot) != null)
                throw new InvalidOperationException("Incomplete representative layout exists; reload the saved baseline before retrying.");

            var carrier = RepLayoutFind("Scenario repair carrier close silhouette");
            var islands = RepLayoutFind("Scenario Z1 service islands");
            var barriers = new[] { 11, 12, 13 }.Select(i => RepLayoutFind("Acquired concrete_road_barrier_02 #" + i)).ToArray();
            var islandRenderers = islands.GetComponentsInChildren<MeshRenderer>(true);
            if (islandRenderers.Length == 0) throw new InvalidOperationException("No service island surfaces found.");
            foreach (var t in barriers.Concat(new[] { carrier, islands }))
                if (t.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Layout trial would move or duplicate a collider: " + t.name);
            foreach (var r in islandRenderers)
                if (!r.enabled || r.transform.childCount != 0 || r.GetComponent<MeshFilter>() == null)
                    throw new InvalidOperationException("Service island source is not an enabled leaf mesh: " + r.name);

            var protectedPaths = islandRenderers.Concat(carrier.GetComponentsInChildren<MeshRenderer>(true))
                .Concat(barriers.SelectMany(t => t.GetComponentsInChildren<MeshRenderer>(true)))
                .SelectMany(r => r.sharedMaterials.Cast<Obj>().Concat(new Obj[] { r.GetComponent<MeshFilter>()?.sharedMesh }))
                .Where(o => o != null).Select(AssetDatabase.GetAssetPath).Where(File.Exists).Distinct()
                .ToDictionary(p => p, AuditHash);
            Directory.CreateDirectory(RepLayoutMeshFolder);
            Directory.CreateDirectory(RepOutput);
            AssetDatabase.Refresh();

            var owned = new GameObject(RepLayoutRoot).transform;
            owned.SetParent(structures, false);
            owned.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            owned.localScale = Vector3.one;
            var westRoot = new GameObject("Relocated existing west pipe stock").transform;
            westRoot.SetParent(owned, false);
            var eastRoot = new GameObject("Preserved existing B03 service reels").transform;
            eastRoot.SetParent(owned, false);
            var report = new StringBuilder("source_mesh,source_vertices,west_vertices,east_vertices,source_triangles,west_triangles,east_triangles,east_world_max_error,materials_preserved\n");
            int sourceVertices = 0, outputVertices = 0, sourceTriangles = 0, outputTriangles = 0;
            int westSurfaces = 0, eastSurfaces = 0;
            float eastMaxError = 0;

            foreach (var r in islandRenderers)
            {
                var source = r.GetComponent<MeshFilter>().sharedMesh;
                if (source == null || !source.isReadable || source.blendShapeCount != 0 || source.bindposes.Length != 0)
                    throw new InvalidOperationException("Expected a readable rigid service mesh: " + r.name);
                if (source.subMeshCount != r.sharedMaterials.Length)
                    throw new InvalidOperationException("Unexpected service mesh/material slot count: " + r.name);
                var vertices = source.vertices;
                var westTriangles = new List<int>[source.subMeshCount];
                var eastTriangles = new List<int>[source.subMeshCount];
                for (int s = 0; s < source.subMeshCount; s++)
                {
                    if (source.GetTopology(s) != MeshTopology.Triangles)
                        throw new InvalidOperationException("Non-triangle service mesh: " + r.name);
                    westTriangles[s] = new List<int>(); eastTriangles[s] = new List<int>();
                    var triangles = source.GetTriangles(s);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        bool a = r.transform.TransformPoint(vertices[triangles[i]]).x < 0;
                        bool b = r.transform.TransformPoint(vertices[triangles[i + 1]]).x < 0;
                        bool c = r.transform.TransformPoint(vertices[triangles[i + 2]]).x < 0;
                        if (a != b || b != c) throw new InvalidOperationException("Service triangle crosses west/east partition; refusing to cut geometry.");
                        var list = a ? westTriangles[s] : eastTriangles[s];
                        list.Add(triangles[i]); list.Add(triangles[i + 1]); list.Add(triangles[i + 2]);
                    }
                }
                var west = RepLayoutSubset(source, westTriangles, "West");
                var east = RepLayoutSubset(source, eastTriangles, "East");
                int wc = west?.sourceVertex.Length ?? 0, ec = east?.sourceVertex.Length ?? 0;
                if (wc + ec != source.vertexCount)
                    throw new InvalidOperationException("Unused or cross-partition shared vertices would change vertex accounting: " + source.name);
                int originalTris = source.triangles.Length / 3;
                int wt = westTriangles.Sum(t => t.Count) / 3, et = eastTriangles.Sum(t => t.Count) / 3;
                if (wt + et != originalTris) throw new InvalidOperationException("Triangle accounting changed during split.");
                float error = 0;
                if (west != null) { RepLayoutSurface(r, west, westRoot, "West"); westSurfaces++; }
                if (east != null)
                {
                    var clone = RepLayoutSurface(r, east, eastRoot, "East"); eastSurfaces++;
                    var actual = clone.GetComponent<MeshFilter>().sharedMesh.vertices;
                    for (int i = 0; i < actual.Length; i++)
                    {
                        var before = r.transform.TransformPoint(vertices[east.sourceVertex[i]]);
                        var after = clone.transform.TransformPoint(actual[i]);
                        error = Mathf.Max(error, Vector3.Distance(before, after));
                        if (!before.Equals(after)) throw new InvalidOperationException("East service geometry moved during split: " + r.name);
                    }
                }
                eastMaxError = Mathf.Max(eastMaxError, error);
                sourceVertices += source.vertexCount; outputVertices += wc + ec;
                sourceTriangles += originalTris; outputTriangles += wt + et;
                report.AppendLine(FacadeCsv(AssetDatabase.GetAssetPath(source)) + "," + source.vertexCount + "," + wc + "," + ec + "," + originalTris + "," + wt + "," + et + "," + error.ToString("R", Inv) + ",True");
                // Keep the source objects and asset assignments for provenance, but render each
                // triangle only once using the split copies. No original mesh data is overwritten.
                r.enabled = false;
            }
            if (westSurfaces == 0 || eastSurfaces == 0 || sourceVertices != outputVertices || sourceTriangles != outputTriangles)
                throw new InvalidOperationException("Incomplete service-island partition.");

            var moves = new StringBuilder("object,delta_x,delta_y,delta_z,before_min_x,before_min_y,before_min_z,before_max_x,before_max_y,before_max_z,after_min_x,after_min_y,after_min_z,after_max_x,after_max_y,after_max_z\n");
            // Source centre (-55,0,84) -> (-44,0,42). Keep the entire stock pad,
            // racks, pipe lengths and markings together with their original dimensions.
            RepLayoutMove(westRoot, new Vector3(11, 0, -42), moves);
            // Carrier vertices already include the original (-64,-.055,58), yaw22 frame.
            // Moving its zero-origin root by this delta preserves the existing orientation.
            // Its lowest tread vertices were y=-.075 on the old apron. Lift those
            // vertices to the Z1 slab top instead of preserving the old penetration.
            RepLayoutMove(carrier, new Vector3(8, .075f, -28), moves);
            string carrierContact = RepLayoutCarrierContact(carrier);
            float[] barrierZ = { 22, 31, 40 };
            for (int i = 0; i < barriers.Length; i++)
            {
                // Original barriers sit on apron y=-.055; their original ground clearance is
                // retained on the Z1 top y=0 by lifting the complete LOD root by .055m.
                var old = barriers[i].position;
                RepLayoutMove(barriers[i], new Vector3(-36 - old.x, .055f, barrierZ[i] - old.z), moves);
            }
            var stockBounds = RepLayoutBounds(westRoot);
            if (stockBounds.min.x < -49.1f || stockBounds.max.x > -38.9f || stockBounds.min.z < 35.4f || stockBounds.max.z > 48.6f)
                throw new InvalidOperationException("Relocated stock does not match the requested 10x13m pad envelope: " + stockBounds);
            foreach (var t in barriers.Concat(new[] { carrier, westRoot }))
                if (RepLayoutBounds(t).max.x >= -22)
                    throw new InvalidOperationException("Layout trial entered protected central travel space: " + t.name);
            if (protectedPaths.Any(p => AuditHash(p.Key) != p.Value))
                throw new InvalidOperationException("Layout trial changed an original mesh or material file.");

            File.WriteAllText(RepOutput + "/layout-mesh-partition.csv", report.ToString());
            File.WriteAllText(RepOutput + "/layout-moves.csv", moves.ToString());
            File.WriteAllText(RepOutput + "/layout-validation.txt",
                "Placement-only representative trial; no new geometry triangles or material changes.\n"
                + "Source vertices=" + sourceVertices + "\nSplit vertices=" + outputVertices
                + "\nSource triangles=" + sourceTriangles + "\nSplit triangles=" + outputTriangles
                + "\nWest surfaces=" + westSurfaces + "\nEast surfaces=" + eastSurfaces
                + "\nEast world-position maximum error=" + eastMaxError.ToString("R", Inv)
                + "\nOriginal protected mesh/material files=" + protectedPaths.Count + "; unchanged=True"
                + "\nCarrier old reference=(-64,-.055,58); new reference=(-56,.020,30), retained yaw22."
                + "\n" + carrierContact
                + "\nStock target centre=(-44,0,42); actual bounds=" + stockBounds
                + "\nBarriers target x=-36,z=22/31/40; old orientation/LOD retained."
                + "\nOriginal service mesh renderers disabled; asset data preserved. East replacement geometry/materials are exact."
                + "\nColliders added/moved=0. Utility island, stairs, lights and scenario anchors untouched."
                + "\nNo scene save, bake, gameplay or HMD validation performed by this method.\n");
            new GameObject(RepLayoutMarker).transform.SetParent(structures, false);
            Debug.Log("REP_LAYOUT_READY vertices=" + outputVertices + " triangles=" + outputTriangles + " east_error=" + eastMaxError.ToString("R", Inv));
        }

        static Transform RepLayoutFind(string name)
        {
            var matches = Obj.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one layout object: " + name + "; found " + matches.Length);
            return matches[0];
        }

        static RepLayoutPart RepLayoutSubset(Mesh source, List<int>[] triangles, string label)
        {
            var used = triangles.SelectMany(t => t).Distinct().OrderBy(i => i).ToArray();
            if (used.Length == 0) return null;
            var remap = new Dictionary<int, int>();
            for (int i = 0; i < used.Length; i++) remap.Add(used[i], i);
            var mesh = new Mesh { name = "Rep_" + label + "_" + source.name, indexFormat = used.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var positions = source.vertices; mesh.vertices = used.Select(i => positions[i]).ToArray();
            var normals = source.normals; if (normals.Length > 0) mesh.normals = used.Select(i => normals[i]).ToArray();
            var tangents = source.tangents; if (tangents.Length > 0) mesh.tangents = used.Select(i => tangents[i]).ToArray();
            var colors = source.colors; if (colors.Length > 0) mesh.colors = used.Select(i => colors[i]).ToArray();
            for (int channel = 0; channel < 8; channel++)
            {
                var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
                if (!source.HasVertexAttribute(attribute)) continue;
                int dimensions = source.GetVertexAttributeDimension(attribute);
                var uv = new List<Vector4>(); source.GetUVs(channel, uv);
                if (dimensions == 2) mesh.SetUVs(channel, used.Select(i => new Vector2(uv[i].x, uv[i].y)).ToList());
                else if (dimensions == 3) mesh.SetUVs(channel, used.Select(i => new Vector3(uv[i].x, uv[i].y, uv[i].z)).ToList());
                else if (dimensions == 4) mesh.SetUVs(channel, used.Select(i => uv[i]).ToList());
                else throw new InvalidOperationException("Unsupported service mesh UV channel dimension.");
            }
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < triangles.Length; s++)
            {
                var mapped = triangles[s].Select(i => remap[i]).ToArray();
                mesh.SetTriangles(mapped, s, false);
                if (!mesh.GetTriangles(s).Select(i => used[i]).SequenceEqual(triangles[s]))
                    throw new InvalidOperationException("Service triangle winding/index order changed.");
            }
            mesh.RecalculateBounds();
            return new RepLayoutPart { mesh = mesh, sourceVertex = used, sourceTriangles = triangles.Select(t => t.ToArray()).ToArray() };
        }

        static MeshRenderer RepLayoutSurface(MeshRenderer source, RepLayoutPart part, Transform parent, string side)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source.GetComponent<MeshFilter>().sharedMesh);
            string key = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Service source mesh has no persistent asset GUID.");
            string path = RepLayoutMeshFolder + "/" + side + "_" + key + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { saved = part.mesh; AssetDatabase.CreateAsset(saved, path); }
            else { EditorUtility.CopySerialized(part.mesh, saved); Obj.DestroyImmediate(part.mesh); EditorUtility.SetDirty(saved); }
            var clone = Obj.Instantiate(source.gameObject);
            clone.name = side + " " + source.name;
            // Set the original local matrix under its original parent first; then preserve the
            // exact world transform when adopting the zero-origin owned partition root.
            clone.transform.SetParent(source.transform.parent, false);
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
            clone.transform.SetParent(parent, true);
            clone.GetComponent<MeshFilter>().sharedMesh = saved;
            var renderer = clone.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            if (!renderer.sharedMaterials.SequenceEqual(source.sharedMaterials))
                throw new InvalidOperationException("Service split changed material slots.");
            return renderer;
        }

        static Bounds RepLayoutBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Layout object has no enabled mesh surfaces: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static string RepLayoutCarrierContact(Transform carrier)
        {
            var slab = RepLayoutFind("Z1 departure 120x100").GetComponent<MeshRenderer>();
            if (slab == null) throw new InvalidOperationException("Z1 support slab renderer is missing.");
            var slabBounds = slab.bounds;
            float top = slabBounds.max.y;
            if (Mathf.Abs(top) > .001f) throw new InvalidOperationException("Z1 slab top changed; carrier lift requires new measurement.");
            var wheels = carrier.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.sharedMaterials.Any(m => m != null && m.name == "ScenarioEnv_vehicle_rubber")).ToArray();
            if (wheels.Length != 1) throw new InvalidOperationException("Expected one original combined carrier tyre surface.");
            var wheelBounds = wheels[0].bounds;
            if (wheelBounds.min.x < slabBounds.min.x || wheelBounds.max.x > slabBounds.max.x
                || wheelBounds.min.z < slabBounds.min.z || wheelBounds.max.z > slabBounds.max.z)
                throw new InvalidOperationException("Carrier wheels straddle the lower B02 apron; a uniform Z1 lift is invalid.");
            int lowerVertices = 0;
            float lowest = float.PositiveInfinity;
            foreach (var filter in carrier.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                    throw new InvalidOperationException("Carrier contact verification needs readable meshes.");
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    lowest = Mathf.Min(lowest, point.y);
                    if (point.y > top + .08f) continue;
                    lowerVertices++;
                    if (point.x < slabBounds.min.x - .0001f || point.x > slabBounds.max.x + .0001f
                        || point.z < slabBounds.min.z - .0001f || point.z > slabBounds.max.z + .0001f)
                        throw new InvalidOperationException("A low carrier tread vertex falls outside the supporting Z1 slab.");
                }
            }
            if (lowerVertices == 0 || Mathf.Abs(lowest - top) > .001f)
                throw new InvalidOperationException("Carrier tread bottom does not meet the Z1 slab: " + lowest.ToString("R", Inv));
            File.WriteAllText(RepOutput + "/layout-carrier-contact.csv",
                "old_reference_x,old_reference_y,old_reference_z,new_reference_x,new_reference_y,new_reference_z,delta_x,delta_y,delta_z,slab_top,lowest_tread_y,tyre_min_x,tyre_max_x,tyre_min_z,tyre_max_z,tyre_min_y,checked_low_vertices,all_wheels_over_Z1\n"
                + "-64,-0.055,58,-56,0.020,30,8,0.075,-28," + top.ToString("R", Inv) + "," + lowest.ToString("R", Inv)
                + "," + wheelBounds.min.x.ToString("R", Inv) + "," + wheelBounds.max.x.ToString("R", Inv)
                + "," + wheelBounds.min.z.ToString("R", Inv) + "," + wheelBounds.max.z.ToString("R", Inv)
                + "," + wheelBounds.min.y.ToString("R", Inv) + "," + lowerVertices + ",True\n");
            return "Carrier tread bottom=" + lowest.ToString("R", Inv) + "; Z1 slab top=" + top.ToString("R", Inv)
                + "; all tyre bounds and " + lowerVertices + " low vertices lie over Z1; source tyre shape retained (approximately .02m above tread bottom)."
                + " Static mesh contact only, no suspension or driving validation.";
        }

        static void RepLayoutMove(Transform root, Vector3 delta, StringBuilder report)
        {
            var before = RepLayoutBounds(root);
            root.position += delta;
            var after = RepLayoutBounds(root);
            if ((after.size - before.size).sqrMagnitude > .0001f)
                throw new InvalidOperationException("Rigid layout move changed bounds dimensions: " + root.name);
            report.AppendLine(FacadeCsv(root.name) + "," + RepVec(delta) + "," + RepVec(before.min) + "," + RepVec(before.max) + "," + RepVec(after.min) + "," + RepVec(after.max));
        }
    }
}
