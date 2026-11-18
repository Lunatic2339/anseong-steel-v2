using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const long B03MeshyByteLimit = 100000000;
        static readonly CultureInfo B03MeshyCulture = CultureInfo.InvariantCulture;

        [Serializable] sealed class B03MeshyPart
        {
            public string name, sourceMesh;
            public int vertices, triangles, materialGroups;
            public bool uv0Present, reversedWinding;
        }

        [Serializable] sealed class B03MeshyManifest
        {
            public string sourceScene, sourceSceneSha256, productionSceneSha256;
            public string status = "stage02 provisional candidate; local export only; no Meshy upload or generation";
            public string coordinates = "OBJ right-handed Y-up metres: obj=(-worldX+pivotX, worldY-pivotY, worldZ-pivotZ). X reflection; winding adjusted for transform determinant.";
            public string uvStatus = "Original per-mesh UV0 preserved. Overlapping/repeated/material-local UVs are expected; not a unique atlas. Retexture recommendation: enable_original_uv=false.";
            public string materialStatus = "MTL preserves slot identity/basic diffuse colour and alpha only. Existing photographic maps are not bundled.";
            public string normals = "Source vertex normals transformed with inverse-transpose and X reflection; normalized. No source mesh mutation.";
            public Vector3 unityWorldPivot, unityWorldBoundsMin, unityWorldBoundsMax;
            public int rendererCount, vertexCount, triangleCount, materialCount, degenerateTriangles;
            public int triangleNormalDisagreementCount;
            public long objBytes, mtlBytes;
            public string objSha256, mtlSha256;
            public bool sourceScenePreserved, productionScenePreserved;
            public B03MeshyPart[] parts;
        }

        // Opens the saved stage02 candidate for reading. Never rebuilds, saves the scene,
        // creates colliders, changes source meshes/materials, or invokes any online service.
        public static void ExportB03MeshyInput()
        {
            string output = Path.GetFullPath("../SourceAssets/B03MeshyTest");
            string sourceHash = B03MeshyHash(ExtB03Scene), productionHash = B03MeshyHash(WideScene);
            if (string.IsNullOrEmpty(sourceHash)) throw new FileNotFoundException("Saved B03 candidate scene is required", ExtB03Scene);
            EditorSceneManager.OpenScene(ExtB03Scene);
            var root = GameObject.Find(ExtB03Root);
            if (root == null) throw new InvalidOperationException("External B03 candidate root missing.");
            var forwardRoof = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Annex forward low roof");
            if (forwardRoof == null || !forwardRoof.gameObject.activeInHierarchy)
                throw new InvalidOperationException("This is not the required stage02 candidate: forward low wing is missing.");

            var renderers = root.GetComponentsInChildren<MeshRenderer>(false)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() != null)
                .OrderBy(r => B03MeshyPath(r.transform, root.transform), StringComparer.Ordinal).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No enabled candidate building meshes.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.x > 45 || bounds.size.z > 45 || bounds.size.y < 14 || bounds.size.y > 18)
                throw new InvalidOperationException("Unexpected stage02 building bounds: " + bounds);
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var materialIds = new Dictionary<Material, string>();
            foreach (var r in renderers)
                foreach (var material in r.sharedMaterials)
                    if (material != null && !materialIds.ContainsKey(material))
                        materialIds.Add(material, "mat_" + materialIds.Count.ToString("000", B03MeshyCulture) + "_" + B03MeshySafe(material.name));

            Directory.CreateDirectory(output);
            string objPath = Path.Combine(output, "B03_reference_candidate.obj");
            string mtlPath = Path.Combine(output, "B03_reference_candidate.mtl");
            var manifest = new B03MeshyManifest
            {
                sourceScene = ExtB03Scene, sourceSceneSha256 = sourceHash,
                productionSceneSha256 = productionHash, unityWorldPivot = pivot,
                unityWorldBoundsMin = bounds.min, unityWorldBoundsMax = bounds.max,
                rendererCount = renderers.Length, materialCount = materialIds.Count
            };
            var parts = new List<B03MeshyPart>();
            int offset = 1;
            using (var writer = new StreamWriter(objPath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("# Anseong Steel B03 stage02 provisional Meshy Retexture input");
                writer.WriteLine("# Metres, Y-up, right-handed; see export-manifest.json for Unity restoration.");
                writer.WriteLine("# Original UV0 overlaps are preserved; this is NOT a unified UV atlas.");
                writer.WriteLine("mtllib B03_reference_candidate.mtl");
                for (int ri = 0; ri < renderers.Length; ri++)
                {
                    var renderer = renderers[ri];
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    if (mesh == null || !mesh.isReadable) throw new InvalidOperationException("Unreadable candidate mesh: " + renderer.name);
                    var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
                    if (normals.Length != vertices.Length) throw new InvalidOperationException("Missing source normals: " + renderer.name);
                    var matrix = renderer.localToWorldMatrix;
                    float determinant = matrix.determinant;
                    if (Mathf.Abs(determinant) < 1e-8f) throw new InvalidOperationException("Singular mesh transform: " + renderer.name);
                    var normalMatrix = matrix.inverse.transpose;
                    // Reflection contributes determinant -1. Reversing faces compensates it.
                    bool reverse = determinant > 0;
                    bool hasUV = uv.Length == vertices.Length;
                    var positions = new Vector3[vertices.Length]; var outNormals = new Vector3[vertices.Length];
                    writer.WriteLine("o " + ri.ToString("0000", B03MeshyCulture) + "_" + B03MeshySafe(renderer.name));
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var p = matrix.MultiplyPoint3x4(vertices[i]) - pivot; p.x = -p.x;
                        if (!B03MeshyFinite(p)) throw new InvalidOperationException("Nonfinite vertex: " + renderer.name);
                        positions[i] = p;
                        writer.WriteLine("v " + B03MeshyVector(p));
                    }
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var t = hasUV ? uv[i] : Vector2.zero;
                        if (!B03MeshyFinite(new Vector3(t.x, t.y, 0))) throw new InvalidOperationException("Nonfinite UV: " + renderer.name);
                        writer.WriteLine("vt " + B03MeshyNumber(t.x) + " " + B03MeshyNumber(t.y));
                    }
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var n = normalMatrix.MultiplyVector(normals[i]); n.x = -n.x;
                        if (!B03MeshyFinite(n) || n.sqrMagnitude < 1e-12f) throw new InvalidOperationException("Invalid normal: " + renderer.name);
                        outNormals[i] = n.normalized;
                        writer.WriteLine("vn " + B03MeshyVector(outNormals[i]));
                    }
                    var part = new B03MeshyPart
                    {
                        name = B03MeshyPath(renderer.transform, root.transform), sourceMesh = mesh.name,
                        vertices = vertices.Length, materialGroups = mesh.subMeshCount,
                        uv0Present = hasUV, reversedWinding = reverse
                    };
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                            throw new InvalidOperationException("Non-triangle submesh: " + renderer.name);
                        var mats = renderer.sharedMaterials;
                        var material = sub < mats.Length ? mats[sub] : (mats.Length > 0 ? mats[mats.Length - 1] : null);
                        writer.WriteLine("g part_" + ri.ToString("0000", B03MeshyCulture) + "_slot_" + sub);
                        writer.WriteLine("usemtl " + (material != null ? materialIds[material] : "mat_null"));
                        var indices = mesh.GetTriangles(sub);
                        for (int t = 0; t < indices.Length; t += 3)
                        {
                            int a = indices[t], b = indices[t + (reverse ? 2 : 1)], c = indices[t + (reverse ? 1 : 2)];
                            var cross = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                            if (cross.sqrMagnitude < 1e-16f) manifest.degenerateTriangles++;
                            else if (Vector3.Dot(cross.normalized, outNormals[a] + outNormals[b] + outNormals[c]) < -.01f)
                                manifest.triangleNormalDisagreementCount++;
                            writer.WriteLine("f " + B03MeshyIndex(a + offset) + " " + B03MeshyIndex(b + offset) + " " + B03MeshyIndex(c + offset));
                            part.triangles++;
                        }
                    }
                    offset += vertices.Length; manifest.vertexCount += vertices.Length;
                    manifest.triangleCount += part.triangles; parts.Add(part);
                }
            }
            using (var writer = new StreamWriter(mtlPath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("# Basic role colours only; original photographic texture files are not bundled.");
                writer.WriteLine("newmtl mat_null\nKd 0.6 0.6 0.6\nd 1\nillum 2\n");
                foreach (var pair in materialIds)
                {
                    var m = pair.Key;
                    Color colour = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.gray);
                    writer.WriteLine("newmtl " + pair.Value);
                    writer.WriteLine("# Unity source material: " + m.name.Replace('\n', ' '));
                    writer.WriteLine("Kd " + B03MeshyVector(new Vector3(colour.r, colour.g, colour.b)));
                    writer.WriteLine("d " + B03MeshyNumber(colour.a));
                    writer.WriteLine("illum 2\n");
                }
            }
            manifest.objBytes = new FileInfo(objPath).Length; manifest.mtlBytes = new FileInfo(mtlPath).Length;
            manifest.objSha256 = B03MeshyHash(objPath); manifest.mtlSha256 = B03MeshyHash(mtlPath);
            manifest.parts = parts.ToArray();
            manifest.sourceScenePreserved = sourceHash == B03MeshyHash(ExtB03Scene);
            manifest.productionScenePreserved = productionHash == B03MeshyHash(WideScene);
            File.WriteAllText(Path.Combine(output, "export-manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            if (!manifest.sourceScenePreserved || !manifest.productionScenePreserved)
                throw new InvalidOperationException("Scene file changed during read-only export.");
            if (manifest.objBytes + manifest.mtlBytes >= B03MeshyByteLimit)
                throw new InvalidOperationException("OBJ+MTL exceeds conservative 100 MB input target; do not upload. Split/optimize the export copy first.");
            Debug.Log("B03 Meshy input exported locally: " + objPath + " | renderers=" + manifest.rendererCount +
                " triangles=" + manifest.triangleCount + " bytes=" + (manifest.objBytes + manifest.mtlBytes) +
                " | existing scenes unchanged; no online action.");
        }

        static bool B03MeshyFinite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        static string B03MeshyNumber(float value) => value.ToString("R", B03MeshyCulture);
        static string B03MeshyVector(Vector3 value) => B03MeshyNumber(value.x) + " " + B03MeshyNumber(value.y) + " " + B03MeshyNumber(value.z);
        static string B03MeshyIndex(int i) { string n = i.ToString(B03MeshyCulture); return n + "/" + n + "/" + n; }
        static string B03MeshySafe(string text) => new string(text.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        static string B03MeshyPath(Transform t, Transform root)
        {
            var names = new List<string>();
            while (t != null && t != root) { names.Add(t.name); t = t.parent; }
            names.Reverse(); return string.Join("/", names);
        }
        static string B03MeshyHash(string path)
        {
            if (!File.Exists(path)) return "";
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
