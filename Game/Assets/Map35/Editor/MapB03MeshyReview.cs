using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string MeshyReviewAssets = ExtB03 + "/Meshy";
        static string MeshyReviewSource => Path.GetFullPath("../SourceAssets/B03MeshyTest");
        static string MeshyReviewOutput => Path.GetFullPath("../docs/validation/b03-meshy-review-2026-11-15");

        // This small, deliberately strict GLB reader avoids FBX centimetre conversion and
        // importer-dependent reflections. Unsupported compression/skins fail explicitly.
        [Serializable] sealed class MrGlb
        {
            public MrNode[] nodes; public MrScene[] scenes; public int scene;
            public MrMesh[] meshes; public MrAccessor[] accessors; public MrBufferView[] bufferViews;
            public MrMaterial[] materials; public string[] extensionsRequired;
        }
        [Serializable] sealed class MrScene { public int[] nodes; }
        [Serializable] sealed class MrNode
        {
            public string name; public int mesh = -1, skin = -1; public int[] children;
            public float[] matrix, translation, rotation, scale;
        }
        [Serializable] sealed class MrMesh { public string name; public MrPrimitive[] primitives; }
        [Serializable] sealed class MrPrimitive
        {
            public MrAttributes attributes; public int indices = -1, material = 0, mode = 4;
        }
        [Serializable] sealed class MrAttributes { public int POSITION = -1, NORMAL = -1, TEXCOORD_0 = -1, TANGENT = -1; }
        [Serializable] sealed class MrAccessor
        {
            public int bufferView = -1, byteOffset, componentType, count; public string type;
            public bool normalized; public MrSparse sparse;
        }
        [Serializable] sealed class MrSparse { public int count; }
        [Serializable] sealed class MrBufferView { public int buffer, byteOffset, byteLength, byteStride; }
        [Serializable] sealed class MrMaterial { public bool doubleSided; public string alphaMode; }
        [Serializable] sealed class MrExport
        {
            public string sourceScene; public Vector3 unityWorldBoundsMin, unityWorldBoundsMax, unityWorldPivot;
        }
        [Serializable] sealed class MrTextureRecord
        {
            public string role, asset, sha256; public int width, height; public bool srgb;
        }
        [Serializable] sealed class MrReview
        {
            public string kind, sourceGlb, sourceGlbSha256, candidateScene, candidateSceneSha256;
            public string productionSceneSha256, baselineCandidateSha256, coordinateContract, limitations;
            public Vector3 rawBoundsMin, rawBoundsMax, worldBoundsMin, worldBoundsMax, translation;
            public float uniformScale, yawDegrees;
            public int renderers, vertices, triangles, materials, originalColliders, cameras;
            public bool productionPreserved, baselineCandidatePreserved, originalMaterialsPreserved, originalCollidersPreserved, anchorsPreserved, camerasPreserved, globalLookPreserved;
            public MrTextureRecord[] textures;
        }
        sealed class MrPart { public Mesh mesh; public int material; public string name; }

        public static void BuildB03MeshyRetexture() { MrBuild("Retexture"); }
        public static void BuildB03MeshyGenerated() { MrBuild("Generated"); }
        public static void CaptureB03MeshyRetexture() { MrCapture("Retexture"); }
        public static void CaptureB03MeshyGenerated() { MrCapture("Generated"); }

        static string MrFolder(string kind) => MeshyReviewAssets + "/" + kind;
        static string MrScenePath(string kind) => ExtB03 + "/Scenes/B03_Meshy" + kind + ".unity";
        static string MrInput(string kind) => MeshyReviewSource + (kind == "Retexture" ? "/retexture-8k-01" : "/image-to-3d-01");
        static float MrArg(string name, float fallback) => float.Parse(RepArg(name, fallback.ToString("R", Inv)), Inv);

        static void MrBuild(string kind)
        {
            string source = MrInput(kind), glbPath = source + "/result.glb", folder = MrFolder(kind);
            if (!File.Exists(glbPath)) throw new FileNotFoundException("Completed Meshy GLB download required", glbPath);
            if (!File.Exists(source + "/download-manifest.json")) throw new FileNotFoundException("Completed download manifest required", source);
            if (!File.Exists(ExtB03Scene)) throw new FileNotFoundException("Saved stage02 candidate required", ExtB03Scene);
            string productionHash = AuditHash(WideScene), baselineHash = AuditHash(ExtB03Scene);
            Directory.CreateDirectory(MeshyReviewOutput + "/" + kind);
            foreach (string sub in new[] { "", "/Sources", "/Textures", "/Materials", "/Meshes", "/Profiles" }) PurposeFolder(folder + sub);

            var textures = new List<MrTextureRecord>();
            // Copy only to this candidate namespace. FBX is retained externally as evidence;
            // the GLB geometry is parsed directly and saved as ordinary Unity mesh assets.
            foreach (string file in Directory.GetFiles(source).Where(p => Path.GetFileName(p).StartsWith("texture_", StringComparison.Ordinal) && new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(p).ToLowerInvariant())))
            {
                string dest = folder + "/Textures/" + Path.GetFileName(file);
                if (!File.Exists(dest) || AuditHash(dest) != AuditHash(file)) File.Copy(file, dest, true);
            }
            string sourceCopy = folder + "/Sources/result.glb.bytes";
            if (!File.Exists(sourceCopy) || AuditHash(sourceCopy) != AuditHash(glbPath)) File.Copy(glbPath, sourceCopy, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Scene switches unload unreferenced native objects. Open the baseline
            // before creating temporary meshes, then persist each mesh before texture
            // reimports can perform another unused-asset sweep.
            EditorSceneManager.OpenScene(ExtB03Scene); ReconnectWideScene();
            string look = FacadeLookSignature(), anchors = PhotoAnchorSignature(), cameras = ExtB03CameraSignature();
            var colliders = RepColliderStates(); var originalMaterials = RepMaterialHashes();

            MrGlb glb; byte[] binary; MrReadGlb(glbPath, out glb, out binary);
            var parts = new List<MrPart>();
            int[] roots = glb.scenes != null && glb.scenes.Length > glb.scene ? glb.scenes[glb.scene].nodes : null;
            if (roots == null) throw new InvalidOperationException("GLB default scene roots absent");
            foreach (int index in roots) MrNodeMeshes(glb, binary, index, Matrix4x4.identity, parts, new HashSet<int>());
            if (parts.Count == 0) throw new InvalidOperationException("GLB has no static triangle meshes");
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i]; string meshPath = folder + "/Meshes/part_" + i.ToString("000") + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved == null) { saved = part.mesh; AssetDatabase.CreateAsset(saved, meshPath); }
                else { EditorUtility.CopySerialized(part.mesh, saved); Obj.DestroyImmediate(part.mesh); EditorUtility.SetDirty(saved); }
                part.mesh = saved;
            }
            AssetDatabase.SaveAssets();
            Bounds raw = parts[0].mesh.bounds; foreach (var part in parts.Skip(1)) raw.Encapsulate(part.mesh.bounds);
            var export = JsonUtility.FromJson<MrExport>(File.ReadAllText(MeshyReviewSource + "/export-manifest.json"));
            var target = new Bounds((export.unityWorldBoundsMin + export.unityWorldBoundsMax) * .5f, export.unityWorldBoundsMax - export.unityWorldBoundsMin);
            float yaw = kind == "Retexture" ? 0 : MrArg("-meshyYaw", 0);
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            float scale = kind == "Retexture" ? target.size.x / raw.size.x : MrArg("-meshyScale", target.size.y / raw.size.y);
            if (!(scale > 0 && scale < 10000)) throw new InvalidOperationException("Invalid uniform scale");
            Vector3 translation;
            if (kind == "Retexture")
            {
                translation = target.center - raw.center * scale;
                var delta = raw.size * scale - target.size;
                if (Mathf.Abs(delta.y) > .02f || Mathf.Abs(delta.z) > .02f)
                    throw new InvalidOperationException("Retexture dimensions are not a uniform normalization of the exported building: " + delta);
            }
            else
            {
                // Generated geometry is not asserted to be the source building. Fit one
                // scale by height, rotate around Y, then ground and center the complete model.
                Bounds rotated = MrTransformedBounds(parts, Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one * scale));
                translation = new Vector3(MrArg("-meshyX", target.center.x) - rotated.center.x,
                    MrArg("-meshyGroundY", target.min.y) - rotated.min.y,
                    MrArg("-meshyZ", target.center.z) - rotated.center.z);
            }
            var materialMap = new Dictionary<int, Material>();
            foreach (int index in parts.Select(p => p.material).Distinct()) materialMap[index] = MrMaterialAsset(folder, index, textures);

            var old = GameObject.Find(ExtB03Root);
            if (old == null) throw new InvalidOperationException("Stage02 B03 candidate visuals missing");
            foreach (var renderer in old.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var lod in old.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
            var root = new GameObject("B03 Meshy " + kind + " candidate");
            root.transform.SetPositionAndRotation(translation, rotation); root.transform.localScale = Vector3.one * scale;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Meshes/part_" + i.ToString("000") + ".asset");
                if (saved == null) throw new InvalidOperationException("Persisted Meshy mesh missing after material imports");
                part.mesh = saved;
                var go = new GameObject(part.name); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = saved;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materialMap[part.material];
                renderer.receiveGI = ReceiveGI.LightProbes; renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes; renderer.shadowCastingMode = ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
            bool matsOK = originalMaterials.All(p => AuditHash(p.Key) == p.Value);
            bool collidersOK = colliders.All(p => p.Key != null && RepColliderState(p.Key) == p.Value) && RepColliderStates().Count == colliders.Count;
            bool anchorsOK = anchors == PhotoAnchorSignature(), camerasOK = cameras == ExtB03CameraSignature(), lookOK = look == FacadeLookSignature();
            if (!matsOK || !collidersOK || !anchorsOK || !camerasOK || !lookOK) throw new InvalidOperationException("Protected candidate baseline changed");
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MrScenePath(kind));
            Bounds world = root.GetComponentInChildren<MeshRenderer>().bounds;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) world.Encapsulate(renderer.bounds);
            var review = new MrReview
            {
                kind = kind, sourceGlb = glbPath, sourceGlbSha256 = AuditHash(glbPath), candidateScene = MrScenePath(kind), candidateSceneSha256 = AuditHash(MrScenePath(kind)),
                productionSceneSha256 = productionHash, baselineCandidateSha256 = baselineHash,
                coordinateContract = "glTF right-handed Y-up => Unity X-reflection + reversed triangles. UV0 V flipped for Unity sampling; tangent basis calculated before V flip. Node transforms baked. Retexture restored by uniform bbox X scale and center; Generated uniform height-fit + documented Y rotation/grounding, no cropping or nonuniform scale.",
                limitations = "Candidate only. No new collider/gameplay/LOD/GI/HMD validation. Source Meshy OPAQUE glass is not recovered. Built-in Standard uses backface culling although source GLB may declare doubleSided. Generated shape/orientation is a first fit requiring paired image review. Original baked environment/light probes retained, no candidate GI bake.",
                rawBoundsMin = raw.min, rawBoundsMax = raw.max, worldBoundsMin = world.min, worldBoundsMax = world.max,
                translation = translation, uniformScale = scale, yawDegrees = yaw,
                renderers = parts.Count, vertices = parts.Sum(p => p.mesh.vertexCount), triangles = parts.Sum(p => p.mesh.triangles.Length / 3), materials = materialMap.Count,
                originalColliders = colliders.Count, cameras = viewer.views.Length, productionPreserved = productionHash == AuditHash(WideScene), baselineCandidatePreserved = baselineHash == AuditHash(ExtB03Scene),
                originalMaterialsPreserved = matsOK, originalCollidersPreserved = collidersOK, anchorsPreserved = anchorsOK, camerasPreserved = camerasOK, globalLookPreserved = lookOK, textures = textures.ToArray()
            };
            File.WriteAllText(MeshyReviewOutput + "/" + kind + "/import-review.json", JsonUtility.ToJson(review, true));
            if (!review.productionPreserved || !review.baselineCandidatePreserved) throw new InvalidOperationException("Protected scene file changed");
            Debug.Log("B03_MESHY_" + kind.ToUpperInvariant() + "_READY " + MrScenePath(kind) + " scale=" + scale + " bounds=" + world);
        }

        static void MrCapture(string kind)
        {
            string phase = RepArg("-meshyLook", "original");
            if (phase != "original" && phase != "adjusted") throw new ArgumentException("-meshyLook original|adjusted; use separate fresh Unity process per look");
            string productionHash = AuditHash(WideScene), baselineHash = AuditHash(ExtB03Scene), candidateHash = AuditHash(MrScenePath(kind));
            EditorSceneManager.OpenScene(MrScenePath(kind)); ReconnectWideScene();
            var report = new StringBuilder(); string poses = ExtB03CameraSignature(), folder = MrFolder(kind);
            report.AppendLine("kind=" + kind + "; look=" + phase + "; fresh-process capture; camera52 fixed; 1600x1000");
            report.AppendLine("Production SHA256=" + productionHash + "; source candidate=" + baselineHash + "; Meshy candidate=" + candidateHash);
            if (phase == "adjusted")
            {
                PurposeFolder(folder + "/Profiles"); B03PhotoAdjustCandidate(folder, viewer.views[52], report);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ExtB03 + "/Scenes/B03_Meshy" + kind + "_Adjusted.unity");
            }
            RepHideOverlays();
            string output = MeshyReviewOutput + "/" + kind; Directory.CreateDirectory(output);
            foreach (int index in new[] { 52, 9, 0, 3 }) SaveWearCamera(viewer.views[index], output + "/" + phase + "-" + index.ToString("00") + ".png");
            if (poses != ExtB03CameraSignature() || productionHash != AuditHash(WideScene) || baselineHash != AuditHash(ExtB03Scene) || candidateHash != AuditHash(MrScenePath(kind)))
                throw new InvalidOperationException("Capture changed protected saved scene/camera");
            report.AppendLine("Original production/source candidate/Meshy original-look scene files and camera poses preserved. Capture count=4. Adjusted look is a separate scene only.");
            File.WriteAllText(output + "/capture-" + phase + ".txt", report.ToString());
            Debug.Log("B03_MESHY_" + kind.ToUpperInvariant() + "_CAPTURE_" + phase.ToUpperInvariant() + "_OK");
        }

        static Material MrMaterialAsset(string folder, int index, List<MrTextureRecord> report)
        {
            string prefix = folder + "/Textures/texture_" + index + "_";
            Texture2D colour = MrTexture(prefix, "base_color", true, false, report);
            Texture2D normal = MrTexture(prefix, "normal", false, true, report);
            Texture2D metallic = MrTexture(prefix, "metallic", false, false, report);
            Texture2D roughness = MrTexture(prefix, "roughness", false, false, report);
            if (colour == null || roughness == null || normal == null || metallic == null) throw new InvalidOperationException("Four downloaded PBR channels required for texture_" + index);
            if (metallic.width != roughness.width || metallic.height != roughness.height) throw new InvalidOperationException("Metallic/roughness dimensions differ; resampling must be reviewed explicitly");
            string packedPath = folder + "/Textures/packed_metal_smooth_" + index + ".png";
            // Source data maps are imported uncompressed/readable/linear. Preserve exact
            // native dimensions and use bytes, avoiding an 8K Color[] allocation.
            Color32[] metals = metallic.GetPixels32(), rough = roughness.GetPixels32();
            for (int i = 0; i < metals.Length; i++) metals[i] = new Color32(metals[i].r, 0, 0, (byte)(255 - rough[i].r));
            var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(metals); packed.Apply(false, false); File.WriteAllBytes(packedPath, packed.EncodeToPNG()); Obj.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
            MrConfigureTexture(packedPath, false, false, false);
            var gloss = AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
            report.Add(new MrTextureRecord { role = "metallic_R_smoothness_A", asset = packedPath, width = gloss.width, height = gloss.height, srgb = false, sha256 = AuditHash(packedPath) });
            // Release CPU copies of original scalar maps after packing; no pixels changed.
            foreach (var tex in new[] { metallic, roughness })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)); ti.isReadable = false; ti.SaveAndReimport();
            }
            string path = folder + "/Materials/Meshy_" + index + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Standard"); material.SetColor("_Color", Color.white); material.SetTexture("_MainTex", colour);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", gloss); material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_GlossMapScale", 1); material.SetFloat("_SmoothnessTextureChannel", 0); material.SetFloat("_Mode", 0);
            material.SetInt("_SrcBlend", (int)BlendMode.One); material.SetInt("_DstBlend", (int)BlendMode.Zero); material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHABLEND_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); material.renderQueue = -1;
            material.SetOverrideTag("MeshyReviewSource", prefix); EditorUtility.SetDirty(material); return material;
        }
        static Texture2D MrTexture(string prefix, string role, bool srgb, bool normal, List<MrTextureRecord> report)
        {
            string directory = Path.GetDirectoryName(prefix), name = Path.GetFileName(prefix) + role;
            string path = Directory.GetFiles(directory).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name && !p.EndsWith(".meta"));
            if (path == null) return null; path = path.Replace('\\', '/');
            MrConfigureTexture(path, srgb, normal, role == "roughness" || role == "metallic");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            report.Add(new MrTextureRecord { role = role, asset = path, width = texture.width, height = texture.height, srgb = srgb, sha256 = AuditHash(path) });
            return texture;
        }
        static void MrConfigureTexture(string path, bool srgb, bool normal, bool readable)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.convertToNormalmap = false; ti.sRGBTexture = srgb; ti.isReadable = readable;
            ti.maxTextureSize = 16384; ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.crunchedCompression = false; ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = true; ti.streamingMipmaps = false; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Repeat;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.ClearPlatformTextureSettings("Standalone"); ti.SaveAndReimport();
        }

        static void MrReadGlb(string path, out MrGlb glb, out byte[] binary)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546c67 || BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length) throw new InvalidDataException("Invalid GLB 2 header");
            string json = null; binary = null;
            for (int offset = 12; offset < bytes.Length;)
            {
                int length = checked((int)BitConverter.ToUInt32(bytes, offset)); uint type = BitConverter.ToUInt32(bytes, offset + 4); offset += 8;
                if (offset + length > bytes.Length) throw new InvalidDataException("Truncated GLB chunk");
                if (type == 0x4e4f534a) json = Encoding.UTF8.GetString(bytes, offset, length);
                if (type == 0x004e4942) { binary = new byte[length]; Buffer.BlockCopy(bytes, offset, binary, 0, length); }
                offset += length;
            }
            if (json == null || binary == null) throw new InvalidDataException("GLB JSON/BIN absent");
            glb = JsonUtility.FromJson<MrGlb>(json);
            if (glb.extensionsRequired != null && glb.extensionsRequired.Length > 0) throw new InvalidDataException("Unsupported required GLB extensions: " + string.Join(",", glb.extensionsRequired));
            // Both downloaded results were inspected as one node/mesh/primitive. Keep
            // this review importer bounded to that verified shape, and assign absent
            // glTF defaults explicitly instead of relying on JsonUtility constructors.
            if (glb.nodes == null || glb.nodes.Length != 1 || glb.meshes == null || glb.meshes.Length != 1 || glb.meshes[0].primitives.Length != 1)
                throw new InvalidDataException("Meshy review expects the inspected single static mesh; review changed export structure before importing");
            var node = glb.nodes[0]; var primitive = glb.meshes[0].primitives[0];
            if (!Regex.IsMatch(json, "\"mesh\"\\s*:")) throw new InvalidDataException("GLB node mesh missing");
            if (!Regex.IsMatch(json, "\"skin\"\\s*:")) node.skin = -1;
            if (!Regex.IsMatch(json, "\"mode\"\\s*:")) primitive.mode = 4;
            if (!Regex.IsMatch(json, "\"indices\"\\s*:")) primitive.indices = -1;
            foreach (string required in new[] { "POSITION", "NORMAL", "TEXCOORD_0" })
                if (!Regex.IsMatch(json, "\"" + required + "\"\\s*:")) throw new InvalidDataException("Required GLB attribute absent: " + required);
            if (!Regex.IsMatch(json, "\"TANGENT\"\\s*:")) primitive.attributes.TANGENT = -1;
        }
        static Matrix4x4 MrNodeMatrix(MrNode n)
        {
            if (n.matrix != null && n.matrix.Length == 16)
            {
                var result = new Matrix4x4(); for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) result[r, c] = n.matrix[c * 4 + r]; return result;
            }
            Vector3 p = n.translation != null && n.translation.Length == 3 ? new Vector3(n.translation[0], n.translation[1], n.translation[2]) : Vector3.zero;
            Quaternion q = n.rotation != null && n.rotation.Length == 4 ? new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3]) : Quaternion.identity;
            Vector3 s = n.scale != null && n.scale.Length == 3 ? new Vector3(n.scale[0], n.scale[1], n.scale[2]) : Vector3.one;
            return Matrix4x4.TRS(p, q, s);
        }
        static void MrNodeMeshes(MrGlb g, byte[] bytes, int index, Matrix4x4 parent, List<MrPart> result, HashSet<int> ancestry)
        {
            if (!ancestry.Add(index)) throw new InvalidDataException("Cyclic GLB hierarchy");
            var node = g.nodes[index]; if (node.skin >= 0) throw new InvalidDataException("Skinned Meshy model not supported by static building review");
            Matrix4x4 world = parent * MrNodeMatrix(node);
            if (node.mesh >= 0)
            {
                foreach (var primitive in g.meshes[node.mesh].primitives)
                {
                    if (primitive.mode != 4) throw new InvalidDataException("Nontriangle GLB primitive");
                    var a = primitive.attributes; float[] positions = MrFloats(g, bytes, a.POSITION, 3), normals = MrFloats(g, bytes, a.NORMAL, 3), uvs = MrFloats(g, bytes, a.TEXCOORD_0, 2);
                    int count = positions.Length / 3; var v = new Vector3[count]; var n = new Vector3[count]; var uv = new Vector2[count];
                    if (normals.Length / 3 != count || uvs.Length / 2 != count) throw new InvalidDataException("POSITION/NORMAL/UV count mismatch");
                    for (int i = 0; i < count; i++) { v[i] = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]); n[i] = new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]); uv[i] = new Vector2(uvs[i * 2], uvs[i * 2 + 1]); }
                    int[] triangles = MrIndices(g, bytes, primitive.indices, count);
                    var mesh = new Mesh { name = "Meshy part " + result.Count, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = triangles;
                    if (a.TANGENT >= 0)
                    {
                        float[] source = MrFloats(g, bytes, a.TANGENT, 4); var t = new Vector4[count];
                        if (source.Length != count * 4) throw new InvalidDataException("Tangent count mismatch");
                        for (int i = 0; i < count; i++) t[i] = new Vector4(source[i * 4], source[i * 4 + 1], source[i * 4 + 2], source[i * 4 + 3]); mesh.tangents = t;
                    }
                    else mesh.RecalculateTangents(); // Before UV V flip, retaining source tangent-space convention.
                    var tangents = mesh.tangents; Matrix4x4 matrix = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * world, nm = matrix.inverse.transpose;
                    if (Mathf.Abs(matrix.determinant) < 1e-10f) throw new InvalidDataException("Singular GLB node");
                    for (int i = 0; i < count; i++)
                    {
                        v[i] = matrix.MultiplyPoint3x4(v[i]); n[i] = nm.MultiplyVector(n[i]).normalized;
                        Vector3 t = matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w * (matrix.determinant < 0 ? -1 : 1)); uv[i].y = 1 - uv[i].y;
                        if (!B03MeshyFinite(v[i]) || !B03MeshyFinite(n[i])) throw new InvalidDataException("Nonfinite GLB vertex/normal");
                    }
                    if (matrix.determinant < 0) for (int i = 0; i < triangles.Length; i += 3) { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
                    mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.tangents = tangents; mesh.triangles = triangles; mesh.RecalculateBounds();
                    result.Add(new MrPart { mesh = mesh, material = primitive.material, name = "Meshy " + (node.name ?? "building") + " " + result.Count });
                }
            }
            if (node.children != null) foreach (int child in node.children) MrNodeMeshes(g, bytes, child, world, result, ancestry);
            ancestry.Remove(index);
        }
        static float[] MrFloats(MrGlb g, byte[] bytes, int index, int components)
        {
            if (index < 0) throw new InvalidDataException("Required GLB vertex attribute missing");
            var a = g.accessors[index]; var b = MrAccessorView(g, a);
            if (a.componentType != 5126 || a.normalized || a.type != "VEC" + components) throw new InvalidDataException("Expected float VEC" + components);
            int stride = b.byteStride > 0 ? b.byteStride : components * 4, offset = b.byteOffset + a.byteOffset;
            var result = new float[checked(a.count * components)];
            if (stride < components * 4 || offset < 0 || (long)offset + (long)(a.count - 1) * stride + components * 4 > bytes.Length) throw new InvalidDataException("GLB accessor outside BIN");
            for (int i = 0; i < a.count; i++) for (int c = 0; c < components; c++) result[i * components + c] = BitConverter.ToSingle(bytes, offset + i * stride + c * 4);
            return result;
        }
        static MrBufferView MrAccessorView(MrGlb g, MrAccessor a)
        {
            if (a.bufferView < 0 || a.sparse != null && a.sparse.count > 0) throw new InvalidDataException("Sparse/implicit GLB accessors unsupported");
            var b = g.bufferViews[a.bufferView]; if (b.buffer != 0) throw new InvalidDataException("External GLB buffer unsupported"); return b;
        }
        static int[] MrIndices(MrGlb g, byte[] bytes, int index, int count)
        {
            if (index < 0) { if (count % 3 != 0) throw new InvalidDataException("Incomplete triangle sequence"); return Enumerable.Range(0, count).ToArray(); }
            var a = g.accessors[index]; var b = MrAccessorView(g, a);
            if (a.type != "SCALAR" || a.count % 3 != 0) throw new InvalidDataException("Triangle index accessor invalid");
            int size = a.componentType == 5125 ? 4 : a.componentType == 5123 ? 2 : a.componentType == 5121 ? 1 : 0;
            if (size == 0) throw new InvalidDataException("Index component type unsupported");
            int stride = b.byteStride > 0 ? b.byteStride : size, offset = b.byteOffset + a.byteOffset; var result = new int[a.count];
            if ((long)offset + (long)(a.count - 1) * stride + size > bytes.Length) throw new InvalidDataException("Index accessor outside BIN");
            for (int i = 0; i < a.count; i++)
            {
                int p = offset + i * stride; result[i] = size == 4 ? checked((int)BitConverter.ToUInt32(bytes, p)) : size == 2 ? BitConverter.ToUInt16(bytes, p) : bytes[p];
                if (result[i] < 0 || result[i] >= count) throw new InvalidDataException("Triangle index outside vertices");
            }
            return result;
        }
        static Bounds MrTransformedBounds(List<MrPart> parts, Matrix4x4 matrix)
        {
            bool first = true; Bounds bounds = new Bounds();
            foreach (var part in parts) foreach (var p in part.mesh.vertices)
            {
                Vector3 world = matrix.MultiplyPoint3x4(p); if (first) { bounds = new Bounds(world, Vector3.zero); first = false; } else bounds.Encapsulate(world);
            }
            return bounds;
        }
    }
}
