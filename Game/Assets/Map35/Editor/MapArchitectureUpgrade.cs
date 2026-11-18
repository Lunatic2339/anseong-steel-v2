using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Run after RealismDetails, before physical UV generation and prefab/LOD creation.
        // All dimensions below are world metres, including children of scaled legacy prefabs.
        static readonly Dictionary<string, Mesh> architectureMeshes = new Dictionary<string, Mesh>();
        static int architectureBeamCount;

        static void UpgradeArchitecture()
        {
            architectureMeshes.Clear();
            architectureBeamCount = 0;
            Directory.CreateDirectory(Base + "Meshes");
            var report = new StringBuilder("Architecture upgrade / world metres / native Unity mesh assets\n");
            UpgradeHangarArchitecture();
            UpgradeDamagedWarehouse();
            UpgradeSecondaryArchitecture();
            UpgradeCraneArchitecture();
            foreach (string id in new[] { "B01", "B02", "B03", "B04", "B05", "B06", "B07", "B08", "B09" })
            {
                var building = Existing(id);
                int meshes = building.GetComponentsInChildren<MeshFilter>().Count(f => f.gameObject.activeInHierarchy);
                var bounds = LegacyEnvironmentAudit.BoundsOf(building.gameObject);
                report.AppendLine(id + " render meshes=" + meshes + ", bounds=" + bounds.size.ToString("F2")
                    + ", physical colliders=" + building.GetComponentsInChildren<Collider>().Length);
            }
            report.AppendLine("I-section structural members=" + architectureBeamCount);
            report.AppendLine("B01 clear door remains x[-12,12], y[0,42]. B02 solid aggregate collider removed; active structural surfaces have individual static mesh colliders.");
            report.AppendLine("Damage is authored static scenery, not runtime destruction. Secondary buildings retain their source combined meshes. No HMD performance or structural engineering certification is implied.");
            File.WriteAllText(WideOutput + "/architecture-upgrade.txt", report.ToString());
        }

        static Transform ArchitectureGroup(string name, Transform parent)
        {
            // A world-identity group keeps world metre helpers correct under 0.75/1.286 legacy scales.
            var previous = parent.Find(name);
            if (previous != null) Obj.DestroyImmediate(previous.gameObject);
            var group = new GameObject(name).transform;
            group.SetParent(parent, true);
            return group;
        }

        static GameObject ArchitectureBox(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = size;
            go.transform.SetParent(parent, true);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) Obj.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void ArchitectureQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        static Mesh ArchitectureSection(float width, float depth)
        {
            string key = "Architecture_I_" + width.ToString("F3", Inv).Replace('.', '_') + "_" + depth.ToString("F3", Inv).Replace('.', '_');
            if (architectureMeshes.TryGetValue(key, out var cached)) return cached;
            float x = width * .5f, y = depth * .5f, flange = depth * .12f, web = width * .16f;
            var profile = new[] {
                new Vector2(-x,-y),new Vector2(x,-y),new Vector2(x,-y+flange),new Vector2(web*.5f,-y+flange),
                new Vector2(web*.5f,y-flange),new Vector2(x,y-flange),new Vector2(x,y),new Vector2(-x,y),
                new Vector2(-x,y-flange),new Vector2(-web*.5f,y-flange),new Vector2(-web*.5f,-y+flange),new Vector2(-x,-y+flange)
            };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < profile.Length; i++)
            {
                var a = profile[i]; var b = profile[(i + 1) % profile.Length];
                ArchitectureQuad(vertices, triangles, new Vector3(a.x, a.y, -.5f), new Vector3(b.x, b.y, -.5f),
                    new Vector3(b.x, b.y, .5f), new Vector3(a.x, a.y, .5f));
            }
            // Three non-overlapping rectangles close the I section without a concave polygon fan.
            foreach (float sign in new[] { -1f, 1f })
            {
                ArchitectureCap(vertices, triangles, -x, x, -y, -y + flange, sign);
                ArchitectureCap(vertices, triangles, -web * .5f, web * .5f, -y + flange, y - flange, sign);
                ArchitectureCap(vertices, triangles, -x, x, y - flange, y, sign);
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            cached = SaveDetailMesh(mesh, key); architectureMeshes[key] = cached;
            return cached;
        }

        static void ArchitectureCap(List<Vector3> vertices, List<int> triangles, float x0, float x1, float y0, float y1, float sign)
        {
            var a = new Vector3(x0, y0, sign * .5f); var b = new Vector3(x1, y0, sign * .5f);
            var c = new Vector3(x1, y1, sign * .5f); var d = new Vector3(x0, y1, sign * .5f);
            if (sign > 0) ArchitectureQuad(vertices, triangles, a, b, c, d);
            else ArchitectureQuad(vertices, triangles, d, c, b, a);
        }

        static GameObject ArchitectureBeam(string name, Vector3 start, Vector3 end, float width, float depth, Transform parent,
            Material material = null, bool collision = false, Vector3? sectionUp = null)
        {
            var direction = end - start;
            if (direction.sqrMagnitude < .001f) throw new ArgumentException("Structural member has no length: " + name);
            var up = sectionUp ?? (Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .97f ? Vector3.forward : Vector3.up);
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation((start + end) * .5f, Quaternion.LookRotation(direction, up));
            go.transform.localScale = new Vector3(1, 1, direction.magnitude);
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = ArchitectureSection(width, depth);
            go.AddComponent<MeshRenderer>().sharedMaterial = material != null ? material : structuralSteel;
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            architectureBeamCount++;
            return go;
        }

        static void ArchitectureBase(Vector3 floor, float size, Transform parent)
        {
            ArchitectureBox("column reinforced concrete pedestal", floor + Vector3.up * .35f, new Vector3(size, .7f, size), damagedConcrete, parent, true);
            ArchitectureBox("column base bearing plate", floor + Vector3.up * .76f, new Vector3(size * .8f, .12f, size * .8f), structuralSteel, parent);
            // Four readable anchor heads at the near base, rather than dense cosmetic bolt arrays.
            foreach (float x in new[] { -.28f, .28f }) foreach (float z in new[] { -.28f, .28f })
                ArchitectureBox("rivet anchor head", floor + new Vector3(x * size, .87f, z * size), new Vector3(.12f, .12f, .12f), agedSteel, parent);
        }

        static void ArchitectureJoint(Vector3 center, Quaternion rotation, float width, float height, Transform parent)
        {
            var go = ArchitectureBox("bolted steel splice plate", center, new Vector3(width, height, .065f), agedSteel, parent);
            go.transform.rotation = rotation;
            foreach (float x in new[] { -.32f, .32f }) foreach (float y in new[] { -.34f, .34f })
            {
                var bolt = ArchitectureBox("rivet splice fastener", center + rotation * new Vector3(x * width, y * height, -.065f),
                    new Vector3(.085f, .085f, .075f), structuralSteel, parent);
                bolt.transform.rotation = rotation;
            }
        }

        static void ArchitectureRail(Vector3 start, Vector3 end, Transform parent)
        {
            float distance = Vector3.Distance(start, end);
            for (int i = 0; i <= Mathf.CeilToInt(distance / 2); i++)
            {
                var p = Vector3.Lerp(start, end, i / (float)Mathf.CeilToInt(distance / 2));
                ArchitectureBox("service guardrail post", p + Vector3.up * .55f, new Vector3(.07f, 1.1f, .07f), agedYellow, parent);
            }
            foreach (float height in new[] { .55f, 1.1f })
            {
                var go = ArchitectureBox("service guardrail", (start + end) * .5f + Vector3.up * height, new Vector3(.065f, .065f, distance), agedYellow, parent);
                go.transform.rotation = Quaternion.LookRotation(end - start);
            }
            var toe = ArchitectureBox("service walkway toe board", (start + end) * .5f + Vector3.up * .08f, new Vector3(.05f, .15f, distance), agedSteel, parent);
            toe.transform.rotation = Quaternion.LookRotation(end - start);
        }

        static void UpgradeHangarArchitecture()
        {
            var root = Existing("B01");
            var group = ArchitectureGroup("Architecture B01 structural envelope", root);
            // Lower the plate inside the existing 48 m envelope; monitors and parapet are not added above it.
            var roof = root.Find("roof");
            roof.position = new Vector3(0, 46.1f, -27.5f); roof.localScale = new Vector3(59.5f, .3f, 54.5f);
            foreach (var t in root.Cast<Transform>().Where(t => t.name == "ceiling frame").ToArray()) Obj.DestroyImmediate(t.gameObject);
            foreach (float z in new[] { -5f, -16f, -27f, -38f, -49f })
            {
                ArchitectureBeam("hangar ceiling I girder", new Vector3(-28.7f, 44.9f, z), new Vector3(28.7f, 44.9f, z), .65f, 1.25f, group);
                foreach (float x in new[] { -28.7f, 28.7f })
                {
                    ArchitectureBeam("hangar interior I column", new Vector3(x, .8f, z), new Vector3(x, 45, z), .8f, 1, group);
                    ArchitectureBase(new Vector3(x, 0, z), 1.5f, group);
                    ArchitectureJoint(new Vector3(x, 43.7f, z - .56f), Quaternion.identity, .6f, 1.7f, group);
                }
            }
            foreach (float x in new[] { -29.6f, 29.6f })
            {
                ArchitectureBox("hangar concrete wall plinth", new Vector3(x, 1.15f, -27.5f), new Vector3(.8f, 2.3f, 54.8f), agedConcrete, group);
                ArchitectureBox("hangar boxed eave fascia", new Vector3(x, 47.7f, -27.5f), new Vector3(.8f, .5f, 54.8f), agedSteel, group);
                ArchitectureBox("hangar rain gutter", new Vector3(x, 46.5f, -27.5f), new Vector3(.7f, .35f, 54.2f), structuralSteel, group);
                foreach (float z in new[] { -8f, -19f, -30f, -41f, -52f })
                    ArchitectureBox("hangar facade cassette joint", new Vector3(x + Mathf.Sign(x) * .37f, 24, z), new Vector3(.07f, 43, .16f), structuralSteel, group);
                foreach (float z in new[] { -3f, -51f })
                    ArchitectureBox("hangar rainwater downpipe", new Vector3(x - Mathf.Sign(x) * .15f, 23.1f, z), new Vector3(.26f, 45.5f, .26f), agedSteel, group);
            }
            foreach (float x in new[] { -21f, 21f })
            {
                ArchitectureBox("hangar front concrete plinth", new Vector3(x, 1.1f, -.05f), new Vector3(17.6f, 2.2f, .5f), agedConcrete, group);
                foreach (float y in new[] { 12f, 24f, 36f })
                    ArchitectureBox("hangar facade horizontal flashing", new Vector3(x, y, .035f), new Vector3(17.7f, .14f, .14f), structuralSteel, group);
            }
            // 24 m-wide / 42 m-high opening is deliberately not crossed by a new beam.
            foreach (float x in new[] { -12.55f, 12.55f })
                ArchitectureBeam("large door recessed guide H section", new Vector3(x, .8f, -.1f), new Vector3(x, 41.8f, -.1f), .7f, .7f, group);
            ArchitectureBox("hangar head drip flashing", new Vector3(0, 47.7f, -.1f), new Vector3(59.5f, .35f, .7f), agedSteel, group);
            foreach (float x in new[] { -18f, 0f, 18f })
            {
                ArchitectureBox("recessed roof monitor curb", new Vector3(x, 46.65f, -29), new Vector3(4.8f, .8f, 18), structuralSteel, group);
                ArchitectureBox("roof monitor folded cover", new Vector3(x, 47.35f, -29), new Vector3(5.2f, .22f, 18.4f), agedSteel, group);
                for (int i = 0; i < 9; i++)
                    ArchitectureBox("roof monitor louvre frame", new Vector3(x, 47.05f, -37 + i * 2), new Vector3(4.85f, .6f, .065f), agedSteel, group);
            }
            for (int i = 0; i < 14; i++)
                ArchitectureBox("roof standing seam", new Vector3(-27 + i * 4, 46.3f, -27.5f), new Vector3(.06f, .11f, 53.5f), agedSteel, group);
            ArchitecturePersonnelDoor(new Vector3(24, .1f, .22f), 180, group);
            for (int i = 0; i < 3; i++)
            {
                float y = 9.15f + i * 4;
                ArchitectureRail(new Vector3(-25.45f, y, -39.8f), new Vector3(-25.45f, y, -30.2f), group);
                ArchitectureRail(new Vector3(-25.45f, y, -30.2f), new Vector3(-20.55f, y, -30.2f), group);
                ArchitectureBeam("repair deck diagonal bracket", new Vector3(-28.6f, y - 2.5f, -33), new Vector3(-21.5f, y - .2f, -33), .18f, .24f, group);
            }
        }

        static void ArchitecturePersonnelDoor(Vector3 ground, float yaw, Transform parent)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            Action<string, Vector3, Vector3, Material> part = (name, offset, size, material) =>
            {
                var go = ArchitectureBox(name, ground + rotation * offset, size, material, parent);
                go.transform.rotation = rotation;
            };
            part("personnel door 1x2.2m", new Vector3(0, 1.1f, 0), new Vector3(1, 2.2f, .11f), agedRoof);
            foreach (float x in new[] { -.57f, .57f }) part("personnel door steel reveal", new Vector3(x, 1.15f, -.03f), new Vector3(.12f, 2.3f, .2f), agedSteel);
            part("personnel door header", new Vector3(0, 2.32f, -.03f), new Vector3(1.25f, .14f, .2f), agedSteel);
            part("personnel door push bar", new Vector3(0, 1, -.09f), new Vector3(.8f, .045f, .08f), agedYellow);
            part("personnel door rain hood", new Vector3(0, 2.55f, -.32f), new Vector3(1.65f, .12f, .9f), agedSteel);
        }

        static void UpgradeDamagedWarehouse()
        {
            var root = Existing("B02");
            var group = ArchitectureGroup("Architecture B02 retained and failed structure", root);
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "B02 torn cladding south"
                || t.name == "B02 curled cladding edge" || t.name == "B02 fallen roof sheet").ToArray()) Obj.DestroyImmediate(t.gameObject);
            foreach (var t in ruins.Cast<Transform>().Where(t => t.name == "fallen roof truss").ToArray()) Obj.DestroyImmediate(t.gameObject);
            int replaced = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true).ToArray())
            {
                string name = filter.name;
                bool column = name.StartsWith("Column_");
                if (!column && !name.StartsWith("RoofTruss_") && name != "LongitudinalBeam" && name != "GableStud") continue;
                var t = filter.transform; var bounds = filter.sharedMesh.bounds;
                Vector3 a = t.TransformPoint(bounds.center - Vector3.up * bounds.extents.y);
                Vector3 b = t.TransformPoint(bounds.center + Vector3.up * bounds.extents.y);
                float width = Mathf.Max(.16f, bounds.size.x * Mathf.Abs(t.lossyScale.x));
                float depth = Mathf.Max(.2f, bounds.size.z * Mathf.Abs(t.lossyScale.z));
                // One roof rafter has buckled inward; its ridge connection remains attached.
                if (name == "RoofTruss_F_5")
                {
                    var kink = Vector3.Lerp(a, b, .53f) + new Vector3(-1.1f, -3.7f, 0);
                    ArchitectureBeam("failed rafter lower H section", a, kink, width, depth, group, agedRust);
                    ArchitectureBeam("failed rafter ridge H section", kink, b, width, depth, group, structuralSteel);
                    ArchitectureJoint(kink, Quaternion.Euler(0, 90, 18), .6f, 1.1f, group);
                }
                else ArchitectureBeam("retained " + name + " H section", a, b, width, depth, group);
                if (column)
                {
                    var bottom = a.y < b.y ? a : b;
                    var top = a.y > b.y ? a : b;
                    ArchitectureBase(new Vector3(bottom.x, .1f, bottom.z), 1.25f, group);
                    ArchitectureJoint(top + new Vector3(0, -.65f, -.39f), Quaternion.identity, .6f, 1.35f, group);
                }
                Obj.DestroyImmediate(t.gameObject); replaced++;
            }
            if (replaced < 20) throw new InvalidOperationException("B02 frame names changed; refusing an incomplete architecture replacement.");
            foreach (float z in new[] { 29f, 61f })
            {
                // Purlins sit behind remaining cladding, exposing fastened wall bays at the breach.
                foreach (float y in new[] { 9f, 17f, 24.4f })
                    ArchitectureBeam("exposed wall girt H section", new Vector3(-118.7f, y, z), new Vector3(-71.3f, y, z), .16f, .24f, group);
                ArchitectureBox("remaining shallow steel eave gutter", new Vector3(-108, 24.95f, z), new Vector3(21.5f, .22f, .34f), agedSteel, group);
            }
            foreach (float z in new[] { 32f, 44f, 58f })
                ArchitectureBeam("roof purlin with intact west anchorage", new Vector3(-118.8f, 25 + (16 - Mathf.Abs(z - 45)) * .5f, z),
                    new Vector3(-95.5f, 25 + (16 - Mathf.Abs(z - 45)) * .5f, z), .18f, .3f, group);
            ArchitectureFoldedPanel("B02 fastened torn south panel", new Vector3(-104.3f, 24.8f, 28.55f), 3.1f, 5.2f, 28, Quaternion.identity, agedSteel, group);
            ArchitectureFoldedPanel("B02 short folded east sheet", new Vector3(-70.65f, 24.5f, 48.7f), 3.4f, 4.3f, -38, Quaternion.Euler(0, 90, 0), agedRust, group);
            ArchitectureFoldedPanel("B02 sheet trapped on fallen rafter", new Vector3(-86, 5.1f, 37), 4.4f, 6.4f, 49, Quaternion.Euler(71, 19, 13), agedRoof, group);
            ArchitectureFoldedPanel("B02 detached roofing fragment", new Vector3(-97, 1.35f, 38), 3.3f, 4.2f, -25, Quaternion.Euler(84, -24, 0), agedSteel, group);
            ArchitectureBeam("fallen rafter retained straight end", new Vector3(-94, 1.1f, 38), new Vector3(-85, 2.7f, 42), .45f, .7f, group, agedRust, true);
            ArchitectureBeam("fallen rafter plastically bent end", new Vector3(-85, 2.7f, 42), new Vector3(-77, 1.3f, 48), .45f, .7f, group, agedRust, true);
            ArchitectureBeam("detached purlin beside breach", new Vector3(-98, 1.25f, 32), new Vector3(-88, 1.5f, 37), .2f, .3f, group, structuralSteel, true);
            ArchitectureJoint(new Vector3(-85, 2.7f, 41.6f), Quaternion.Euler(8, 0, -8), .9f, 1.1f, group);
            ArchitecturePersonnelDoor(new Vector3(-114, 1.03f, 28.58f), 0, group);
            // The old single box closed the entire damaged interior. Use the actual active solid surfaces.
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Obj.DestroyImmediate(collider);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !filter.gameObject.activeInHierarchy) continue;
                string name = filter.name.ToLowerInvariant();
                if (name.Contains("rivet") || name.Contains("burn") || name.Contains("vent slit") || name.Contains("ladder") || name.Contains("push bar")) continue;
                if (renderer.sharedMaterials.Any(m => m != null && (m.shader.name.Contains("SurfaceSoot") || m.name.ToLowerInvariant().Contains("glass")))) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            if (root.GetComponent<BoxCollider>() != null) throw new InvalidOperationException("B02 aggregate collider unexpectedly remains.");
        }

        static void ArchitectureFoldedPanel(string name, Vector3 anchor, float width, float length, float bend, Quaternion rotation, Material material, Transform parent)
        {
            // Rigid corrugated segments meet at a crease. The sheet has closed thickness and no cloth-like sine curl.
            int columns = Mathf.Max(16, Mathf.CeilToInt(width / .25f) * 4);
            float angle = bend * Mathf.Deg2Rad, thickness = .026f;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Func<int, int, int, Vector3> point = (column, row, side) =>
            {
                float u = column / (float)columns;
                float distance = row == 0 ? 0 : row == 1 ? length * .56f : length;
                float corrugation = column % 4 == 1 || column % 4 == 2 ? .07f : 0;
                float tail = Mathf.Max(0, distance - length * .56f);
                float cut = row == 2 && column > columns * .66f ? .2f : 0;
                return new Vector3((u - .5f) * width, -distance + tail * (1 - Mathf.Cos(angle)) + cut,
                    tail * Mathf.Sin(angle) + corrugation + side * thickness);
            };
            for (int col = 0; col < columns; col++) for (int row = 0; row < 2; row++)
            {
                ArchitectureQuad(vertices, triangles, point(col, row, 0), point(col, row + 1, 0), point(col + 1, row + 1, 0), point(col + 1, row, 0));
                ArchitectureQuad(vertices, triangles, point(col + 1, row, 1), point(col + 1, row + 1, 1), point(col, row + 1, 1), point(col, row, 1));
            }
            for (int col = 0; col < columns; col++)
            {
                ArchitectureQuad(vertices, triangles, point(col, 0, 1), point(col, 0, 0), point(col + 1, 0, 0), point(col + 1, 0, 1));
                ArchitectureQuad(vertices, triangles, point(col + 1, 2, 1), point(col + 1, 2, 0), point(col, 2, 0), point(col, 2, 1));
            }
            for (int row = 0; row < 2; row++)
            {
                ArchitectureQuad(vertices, triangles, point(0, row, 1), point(0, row + 1, 1), point(0, row + 1, 0), point(0, row, 0));
                ArchitectureQuad(vertices, triangles, point(columns, row, 0), point(columns, row + 1, 0), point(columns, row + 1, 1), point(columns, row, 1));
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            var go = new GameObject(name); go.transform.SetPositionAndRotation(anchor, rotation); go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = SaveDetailMesh(mesh, name.Replace(' ', '_'));
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            // The exposed upper fastener strip explains why hanging remnants remain attached.
            if (anchor.y > 10)
            {
                var strip = ArchitectureBox("sheet retained fastening strip", anchor + rotation * new Vector3(0, -.08f, -.06f), new Vector3(width, .12f, .12f), structuralSteel, parent);
                strip.transform.rotation = rotation;
            }
        }

        static void UpgradeSecondaryArchitecture()
        {
            foreach (string id in new[] { "B03", "B04", "B05", "B06", "B08", "B09" })
            {
                var root = Existing(id); var bounds = LegacyEnvironmentAudit.BoundsOf(root.gameObject);
                var group = ArchitectureGroup("Architecture " + id + " foundations and service frontage", root);
                float x0 = bounds.min.x + .35f, x1 = bounds.max.x - .35f;
                float z0 = bounds.min.z + .35f, z1 = bounds.max.z - .35f;
                // Retained perimeter / construction joints remain inside the original building envelope.
                foreach (float x in new[] { x0, x1 })
                    ArchitectureBox("foundation edge curb", new Vector3(x, .3f, bounds.center.z), new Vector3(.45f, .6f, z1 - z0), agedConcrete, group);
                foreach (float z in new[] { z0, z1 })
                    ArchitectureBox("foundation edge curb", new Vector3(bounds.center.x, .3f, z), new Vector3(x1 - x0, .6f, .45f), agedConcrete, group);
                for (float x = x0 + 4; x < x1 - 2; x += 8)
                    ArchitectureBox("foundation expansion joint", new Vector3(x, .315f, z0 - .23f), new Vector3(.035f, .54f, .012f), agedRoof, group);
                float centerX = x0 + Mathf.Min(9, bounds.size.x * .3f);
                float length = Mathf.Min(13, bounds.size.x * .65f);
                float front = z0 + .7f;
                ArchitectureBox("service access landing", new Vector3(centerX, 1.05f, front + .6f), new Vector3(length, .18f, 1.4f), structuralSteel, group);
                ArchitectureRail(new Vector3(centerX - length * .5f, 1.15f, front), new Vector3(centerX + length * .5f - 1.4f, 1.15f, front), group);
                foreach (float x in new[] { centerX - length * .42f, centerX + length * .42f })
                {
                    ArchitectureBeam("service landing I support", new Vector3(x, .2f, front + .5f), new Vector3(x, 1.05f, front + .5f), .18f, .22f, group);
                    ArchitectureBox("landing support base plate", new Vector3(x, .22f, front + .5f), new Vector3(.6f, .08f, .6f), agedSteel, group);
                }
                for (int i = 0; i < 5; i++)
                    ArchitectureBox("service access stair tread", new Vector3(centerX + length * .5f - .65f, .18f + .19f * i, front - .05f + i * .27f),
                        new Vector3(1.1f, .12f, .3f), agedSteel, group);
            }
            // Lower-frequency facade structure for the large halls; sections are kept within the footprint.
            ArchitectureHallFront("B04", new Vector3(85, 0, 145), 72, 66, 26);
            ArchitectureHallFront("B05", new Vector3(-155, 0, 235), 67, 105, 14);
        }

        static void ArchitectureHallFront(string id, Vector3 center, float width, float depth, float height)
        {
            var root = Existing(id); var group = ArchitectureGroup("Architecture " + id + " facade depth", root);
            float z = center.z - depth * .5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = center.x + side * (width * .5f - .8f);
                ArchitectureBeam("hall exterior portal I column", new Vector3(x, .8f, z), new Vector3(x, height, z), .65f, .8f, group);
                ArchitectureBase(new Vector3(x, 0, z), 1.6f, group);
                ArchitectureJoint(new Vector3(x, height - .5f, z - .42f), Quaternion.identity, .6f, 1.1f, group);
                ArchitectureBox("hall downpipe", new Vector3(x - side * 1.15f, height * .5f, z), new Vector3(.22f, height - .6f, .22f), agedSteel, group);
            }
            ArchitectureBeam("hall eaves I section", new Vector3(center.x - width * .5f + .8f, height, z),
                new Vector3(center.x + width * .5f - .8f, height, z), .45f, .75f, group);
        }

        static void UpgradeCraneArchitecture()
        {
            var root = Existing("B07"); var group = ArchitectureGroup("Architecture B07 crane joints", root);
            foreach (var t in root.Cast<Transform>().Where(t => t.name == "crane column").ToArray())
            {
                Vector3 a = t.position - Vector3.up * t.lossyScale.y * .5f;
                Vector3 b = t.position + Vector3.up * t.lossyScale.y * .5f;
                ArchitectureBeam("crane built-up H column", a, b, 3, 3, group, agedRust, true);
                ArchitectureBox("crane anchor bearing plate", new Vector3(a.x, 2.05f, a.z), new Vector3(4, .18f, 4), structuralSteel, group);
                foreach (float height in new[] { 16f, 32f, 48f, 61f })
                {
                    ArchitectureJoint(new Vector3(a.x, height, a.z - 1.52f), Quaternion.identity, 2.2f, 2.1f, group);
                    ArchitectureBox("crane column web stiffener", new Vector3(a.x, height, a.z), new Vector3(2.7f, .13f, 2.65f), agedRust, group);
                }
                Obj.DestroyImmediate(t.gameObject);
            }
            foreach (float z in new[] { 525f, 585f })
                for (float x = 43; x < 140; x += 8)
                    foreach (float side in new[] { -1f, 1f })
                        ArchitectureBox("crane girder web stiffener", new Vector3(x, 65, z + side * 1.96f), new Vector3(.13f, 5.6f, .12f), structuralSteel, group);
            // Service rail stays on the existing east deck, away from the P02 latch and coil drop volume.
            ArchitectureRail(new Vector3(146.8f, 46.2f, 549.3f), new Vector3(146.8f, 46.2f, 560.7f), group);
        }
    }
}
