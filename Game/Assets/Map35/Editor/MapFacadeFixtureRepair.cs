using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static void RepairFacadeLampSupport()
        {
            var building = Existing("B03");
            const string groupName = "B03 roof-mounted luminaire support";
            if (building.Find(groupName) != null) return;
            var ray = new Ray(new Vector3(83, 30, 35.7f), Vector3.down);
            float best = float.PositiveInfinity; Vector3 normal = Vector3.up; string roofMesh = "";
            foreach (var filter in building.GetComponentsInChildren<MeshFilter>())
            {
                if (FacadeIsFarLod(filter.transform) || filter.sharedMesh == null) continue;
                var verts = filter.sharedMesh.vertices; var faces = filter.sharedMesh.triangles;
                for (int i = 0; i < faces.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(verts[faces[i]]);
                    var b = filter.transform.TransformPoint(verts[faces[i + 1]]);
                    var c = filter.transform.TransformPoint(verts[faces[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(n.y) < .45f || !FacadeTriangleHit(ray, a, b, c, out float distance) || distance >= best) continue;
                    best = distance; normal = n.y < 0 ? -n : n; roofMesh = PurposeGroundPath(filter.transform);
                }
            }
            if (float.IsInfinity(best)) throw new InvalidOperationException("B03 lamp support has no rendered roof below it.");
            var contact = ray.GetPoint(best);
            if (contact.y < 4 || contact.y > 10.7f) throw new InvalidOperationException("Unexpected B03 roof height: " + contact);
            var material = AssetDatabase.LoadAssetAtPath<Material>(FacadeFolder + "/Materials/Photo_Wide_Palette_B03_metal_existing-surface.mat");
            if (material == null) throw new InvalidOperationException("B03 calibrated structural metal is missing.");
            var root = new GameObject(groupName).transform; root.SetParent(building, false);
            var top = new Vector3(83, 11, 35.7f); var arm = new Vector3(83, 11, 34.2f);
            FacadeSupportBox("roof mounting plate", contact + normal * .045f, new Vector3(.58f, .10f, .58f), Quaternion.FromToRotation(Vector3.up, normal), material, root);
            FacadeSupportBeam("roof-mounted steel stanchion", contact + normal * .05f, top, .16f, material, root);
            FacadeSupportBeam("connected lamp outrigger extension", top, arm, .14f, material, root);
            FacadeSupportBeam("lamp outrigger diagonal brace", Vector3.Lerp(contact, top, .3f), arm, .10f, material, root);
            var slope = Quaternion.FromToRotation(Vector3.up, normal);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                FacadeSupportBox("mounting bolt", contact + normal * .12f + slope * new Vector3(x * .20f, 0, z * .20f), new Vector3(.065f, .065f, .065f), slope, material, root);
            File.WriteAllText(FacadeOutput + "/fixture-support.txt", "Existing floating Pump service entrance light kept at (83,11,32); light settings and aim unchanged.\n"
                + "Rendered roof=" + roofMesh + "\nRoof contact=" + contact.ToString("F5") + "\nRoof normal=" + normal.ToString("F5")
                + "\nConnected to old arm at=" + arm.ToString("F5") + "\nAdded renderers=8\nAdded colliders=0\nSource mesh/prefab assets unchanged.\n");
        }
        static bool FacadeTriangleHit(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0; var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(ray.direction, e2); float determinant = Vector3.Dot(e1, p);
            if (Mathf.Abs(determinant) < .000001f) return false;
            float inverse = 1 / determinant; var t = ray.origin - a; float u = Vector3.Dot(t, p) * inverse;
            if (u < -.00001f || u > 1.00001f) return false;
            var q = Vector3.Cross(t, e1); float v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < -.00001f || u + v > 1.00001f) return false;
            distance = Vector3.Dot(e2, q) * inverse; return distance > 0;
        }
        static void FacadeSupportBeam(string name, Vector3 a, Vector3 b, float thickness, Material material, Transform parent)
        { FacadeSupportBox(name, (a + b) * .5f, new Vector3(thickness, Vector3.Distance(a, b), thickness), Quaternion.FromToRotation(Vector3.up, b - a), material, parent); }
        static void FacadeSupportBox(string name, Vector3 position, Vector3 size, Quaternion rotation, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetPositionAndRotation(position, rotation); go.transform.localScale = size; go.transform.SetParent(parent, true);
            Obj.DestroyImmediate(go.GetComponent<Collider>());
            var filter = go.GetComponent<MeshFilter>(); filter.sharedMesh = MakePhysicalUv(filter.sharedMesh, go.transform.lossyScale);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
