using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        [Serializable] sealed class MeshyPackageRecord
        {
            public string kind, sourceScene, prefab, prefabSha256, pivotContract, limitations;
            public Vector3 position, eulerDegrees, localScale, worldBoundsMin, worldBoundsMax;
            public int renderers, vertices, triangles, colliderCount, originalSceneColliders;
            public bool protectedFilesPreserved, camerasPreserved, anchorsPreserved, originalCollidersPreserved, lookPreserved, prefabTransformVerified;
        }
        [Serializable] sealed class MeshyPlacementRecord
        {
            public string sourceScene, reviewScene, reviewSceneSha256, look, tag, limitations;
            public Vector3 yawPivot, originalPosition, resultPosition, originalEuler, resultEuler, uniformScale, worldBoundsMin, worldBoundsMax;
            public float yawDelta;
            public int captures, originalColliders;
            public bool protectedFilesPreserved, camerasPreserved, anchorsPreserved, originalCollidersPreserved;
        }

        // No texture reimport, model rebuild, lighting bake or scene overwrite.
        public static void PackageB03MeshyCandidates()
        {
            string output = MeshyReviewOutput + "/packages"; Directory.CreateDirectory(output);
            var records = new List<MeshyPackageRecord>();
            foreach (string kind in new[] { "Retexture", "Generated" })
            {
                EditorSceneManager.OpenScene(MrScenePath(kind)); ReconnectWideScene();
                var root = GameObject.Find("B03 Meshy " + kind + " candidate");
                if (root == null || root.transform.parent != null) throw new InvalidOperationException("Expected an unparented Meshy candidate root: " + kind);
                var files = MpProtectedFiles(); string cameras = ExtB03CameraSignature(), anchors = PhotoAnchorSignature(), look = FacadeLookSignature();
                var colliders = RepColliderStates();
                if (root.GetComponentsInChildren<Collider>(true).Length != 0) throw new InvalidOperationException("Visual candidate unexpectedly contains colliders");
                string folder = MrFolder(kind) + "/Prefabs"; PurposeFolder(folder);
                string path = folder + "/B03_Meshy" + kind + "_WorldPlacement.prefab";
                Vector3 position = root.transform.position, scale = root.transform.localScale; Quaternion rotation = root.transform.rotation;
                Bounds bounds = MpBounds(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null) throw new InvalidOperationException("Prefab save failed: " + path);
                AssetDatabase.SaveAssets();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool poseOK = Vector3.Distance(prefab.transform.localPosition, position) < .0001f && Quaternion.Angle(prefab.transform.localRotation, rotation) < .001f && Vector3.Distance(prefab.transform.localScale, scale) < .00001f;
                if (!poseOK || MpHierarchySignature(root) != MpHierarchySignature(prefab)) throw new InvalidOperationException("Prefab did not retain the candidate pose/mesh/material hierarchy");
                bool colliderOK = MpCollidersUnchanged(colliders);
                bool cameraOK = cameras == ExtB03CameraSignature(), anchorOK = anchors == PhotoAnchorSignature(), lookOK = look == FacadeLookSignature();
                if (!colliderOK || !cameraOK || !anchorOK || !lookOK) throw new InvalidOperationException("Packaging changed source scene state");
                MpCheckFiles(files, output + "/" + kind + "-protected-files.csv");
                var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Where(m => m != null).ToArray();
                records.Add(new MeshyPackageRecord
                {
                    kind = kind, sourceScene = MrScenePath(kind), prefab = path, prefabSha256 = AuditHash(path),
                    pivotContract = "Root retains saved world position/rotation/uniform scale. Child mesh positions remain normalized glTF-to-Unity coordinates. Instantiate under a scene root and retain this root transform for exact B03 placement. Setting position to zero or scale to one changes placement/size. Source GLB normalization is not metres until the root uniform scale is applied.",
                    limitations = "Visual asset only, no colliders, interior usability, LOD, GI bake, HMD or gameplay acceptance. Standard materials/textures/mesh assets are referenced in their same candidate folder, not embedded in the prefab. Source opaque glass/backface-culling limitations remain.",
                    position = position, eulerDegrees = rotation.eulerAngles, localScale = scale, worldBoundsMin = bounds.min, worldBoundsMax = bounds.max,
                    renderers = root.GetComponentsInChildren<Renderer>(true).Length, vertices = meshes.Sum(m => m.vertexCount), triangles = meshes.Sum(m => m.triangles.Length / 3), colliderCount = 0, originalSceneColliders = colliders.Count,
                    protectedFilesPreserved = true, camerasPreserved = cameraOK, anchorsPreserved = anchorOK, originalCollidersPreserved = colliderOK, lookPreserved = lookOK, prefabTransformVerified = poseOK
                });
                File.WriteAllText(output + "/" + kind + "-prefab.json", JsonUtility.ToJson(records.Last(), true));
            }
            File.WriteAllText(output + "/README.txt", "Two reusable visual prefabs saved from their immutable original-look candidate scenes.\n" +
                string.Join("\n", records.Select(r => r.kind + ": " + r.prefab + " | position=" + r.position.ToString("R") + " | euler=" + r.eulerDegrees.ToString("R") + " | scale=" + r.localScale.ToString("R"))) +
                "\nUse each complete candidate asset folder with the prefab; texture/mesh references must remain available. Neither source scene was saved. Production and source files verified by SHA256. This does not promote either candidate into production.\n");
            Debug.Log("B03_MESHY_PACKAGES_OK count=" + records.Count);
        }

        // One call per fresh process. Always loads original saved Generated, never the
        // previous yaw/look output. Optional pivot keeps a chosen architectural anchor fixed.
        public static void ReviewB03MeshyGeneratedPlacement()
        {
            string look = RepArg("-meshyReviewLook", "original");
            if (!new[] { "original", "adjusted", "ambient" }.Contains(look)) throw new ArgumentException("-meshyReviewLook original|adjusted|ambient");
            float delta = MrArg("-meshyYawDelta", 0);
            if (float.IsNaN(delta) || float.IsInfinity(delta) || Mathf.Abs(delta) > 20) throw new ArgumentOutOfRangeException("-meshyYawDelta", "Bounded review allows +/-20 degrees only");
            string defaultTag = "yaw_" + delta.ToString("0.###", Inv).Replace("-", "m").Replace(".", "p") + "_" + look;
            string tag = RepArg("-meshyReviewTag", defaultTag);
            if (string.IsNullOrEmpty(tag) || tag.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-')) throw new ArgumentException("Review tag must contain only letters/digits/_/-");
            string assets = MrFolder("Generated") + "/PlacementReviews/" + tag;
            string output = MeshyReviewOutput + "/Generated/placement-" + tag;
            string scene = assets + "/B03_MeshyGenerated_Review.unity";
            if (File.Exists(scene) || File.Exists(output + "/review.json")) throw new InvalidOperationException("Review tag already exists; use a new -meshyReviewTag to preserve previous evidence");
            EditorSceneManager.OpenScene(MrScenePath("Generated")); ReconnectWideScene();
            var root = GameObject.Find("B03 Meshy Generated candidate");
            if (root == null) throw new InvalidOperationException("Generated candidate root missing");
            var protectedFiles = MpProtectedFiles(); var colliders = RepColliderStates();
            string cameraSignature = ExtB03CameraSignature(), anchors = PhotoAnchorSignature(), beforeLook = FacadeLookSignature();
            var report = new StringBuilder("Fresh-process Generated placement review. Original saved Generated scene is the only starting point.\n");
            Directory.CreateDirectory(output); PurposeFolder(assets); PurposeFolder(assets + "/Profiles");
            Bounds beforeBounds = MpBounds(root);
            Vector3 oldPosition = root.transform.position, oldEuler = root.transform.eulerAngles;
            Vector3 pivot = new Vector3(MrArg("-meshyPivotX", beforeBounds.center.x), beforeBounds.min.y, MrArg("-meshyPivotZ", beforeBounds.center.z));
            root.transform.RotateAround(pivot, Vector3.up, delta);
            if (look == "adjusted") B03PhotoAdjustCandidate(assets, viewer.views[52], report);
            if (look == "ambient") MpAmbient(root, report);
            if (look == "original" && beforeLook != FacadeLookSignature()) throw new InvalidOperationException("Original-look review changed global look");
            report.AppendLine("Yaw delta=" + delta.ToString("R", Inv) + "; pivot world=" + pivot.ToString("R") + "; uniform scale=" + root.transform.localScale.ToString("R"));
            report.AppendLine("Yaw changes candidate visual geometry only. Existing original B03 colliders/anchors stay fixed; this review does not establish their fit to the generated model.");
            RepHideOverlays(); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene);
            var camera = viewer.views[52]; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            float fov = camera.fieldOfView, aspect = camera.aspect;
            try
            {
                if (look == "ambient") MpSetAmbientSH();
                SaveWearCamera(camera, output + "/camera52-center.png");
                camera.transform.position = position - camera.transform.right * 2;
                if (look == "ambient") MpSetAmbientSH();
                SaveWearCamera(camera, output + "/camera52-left2m.png");
                camera.transform.position = position + camera.transform.right * 2;
                if (look == "ambient") MpSetAmbientSH();
                SaveWearCamera(camera, output + "/camera52-right2m.png");
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov; camera.aspect = aspect; }
            foreach (int index in new[] { 0, 3, 9 })
            {
                if (look == "ambient") MpSetAmbientSH();
                SaveWearCamera(viewer.views[index], output + "/camera" + index.ToString("00") + ".png");
            }
            bool camerasOK = cameraSignature == ExtB03CameraSignature(), anchorsOK = anchors == PhotoAnchorSignature(), colliderOK = MpCollidersUnchanged(colliders);
            if (!camerasOK || !anchorsOK || !colliderOK) throw new InvalidOperationException("Review changed original cameras/anchors/colliders");
            MpCheckFiles(protectedFiles, output + "/protected-files.csv");
            Bounds bounds = MpBounds(root);
            var record = new MeshyPlacementRecord
            {
                sourceScene = MrScenePath("Generated"), reviewScene = scene, reviewSceneSha256 = AuditHash(scene), look = look, tag = tag,
                limitations = "Candidate placement/lighting review only. No texture reimport, geometry rebuild, collider alignment claim, GI bake, gameplay or HMD validation. +/-2m views translate along camera52 right while retaining original rotation/FOV. Ambient mode is explicit flat SH illumination with original exposure, not a production lighting recommendation.",
                yawPivot = pivot, originalPosition = oldPosition, resultPosition = root.transform.position, originalEuler = oldEuler, resultEuler = root.transform.eulerAngles,
                uniformScale = root.transform.localScale, worldBoundsMin = bounds.min, worldBoundsMax = bounds.max, yawDelta = delta, captures = 6, originalColliders = colliders.Count,
                protectedFilesPreserved = true, camerasPreserved = camerasOK, anchorsPreserved = anchorsOK, originalCollidersPreserved = colliderOK
            };
            File.WriteAllText(output + "/review.json", JsonUtility.ToJson(record, true));
            report.AppendLine("Camera52 original position=" + position.ToString("R") + "; rotation=" + rotation.ToString("R") + "; FOV=" + fov.ToString("R", Inv));
            report.AppendLine("Captured center, left2m, right2m, 0,3,9; original scene camera poses restored. Protected files unchanged=" + protectedFiles.Count);
            File.WriteAllText(output + "/report.txt", report.ToString());
            Debug.Log("B03_MESHY_PLACEMENT_REVIEW_OK " + tag + " captures=6");
        }

        static void MpAmbient(GameObject root, StringBuilder report)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.lightProbeUsage = LightProbeUsage.Off;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.8f, .8f, .8f, 1); RenderSettings.ambientIntensity = 1;
            MpSetAmbientSH();
            report.AppendLine("Ambient diagnostic: candidate lightProbeUsage=Off; RenderSettings ambientMode=Flat, ambientLight sRGB(.8,.8,.8), ambientIntensity=1; explicit SH AddAmbientLight(sRGB(.8).linear). Original exposure, direct lights, fog, reflections and materials retained. Global ambient may affect other unbaked renderers. No DynamicGI.UpdateEnvironment overwrite during captures.");
            report.AppendLine("Explicit ambient linear RGB=" + new Color(.8f, .8f, .8f, 1).linear.ToString("R"));
        }
        static void MpSetAmbientSH()
        {
            var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(new Color(.8f, .8f, .8f, 1).linear); RenderSettings.ambientProbe = probe;
        }
        static Bounds MpBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true); if (renderers.Length == 0) throw new InvalidOperationException("Candidate has no renderers");
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static bool MpCollidersUnchanged(Dictionary<Collider, string> colliders) => colliders.Count == RepColliderStates().Count && colliders.All(p => p.Key != null && RepColliderState(p.Key) == p.Value);
        static string MpHierarchySignature(GameObject root)
        {
            return string.Join("\n", root.GetComponentsInChildren<MeshFilter>(true).Select(f =>
            {
                var r = f.GetComponent<MeshRenderer>();
                return B03MeshyPath(f.transform, root.transform) + "|" + f.transform.localPosition.ToString("R") + "|" + f.transform.localRotation.ToString("R") + "|" + f.transform.localScale.ToString("R") + "|" + AssetDatabase.GetAssetPath(f.sharedMesh) + "|" + string.Join(";", r.sharedMaterials.Select(m => AssetDatabase.GetAssetPath(m)));
            }).OrderBy(s => s, StringComparer.Ordinal));
        }
        static Dictionary<string, string> MpProtectedFiles()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { WideScene, ExtB03Scene, MrScenePath("Retexture"), MrScenePath("Generated") };
            foreach (string kind in new[] { "Retexture", "Generated" })
            {
                paths.Add(MrInput(kind) + "/result.glb"); paths.Add(MrInput(kind) + "/download-manifest.json");
                string scene = MrScenePath(kind);
                if (!File.Exists(scene)) throw new FileNotFoundException("Both original candidate scenes are required", scene);
                foreach (string path in AssetDatabase.GetDependencies(scene, true))
                    if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || path.StartsWith(MeshyReviewAssets + "/", StringComparison.Ordinal)) paths.Add(path);
            }
            foreach (string scene in new[] { WideScene, ExtB03Scene })
                foreach (string path in AssetDatabase.GetDependencies(scene, true))
                    if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && path.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0) paths.Add(path);
            paths.Add(MeshyReviewSource + "/export-manifest.json");
            foreach (string path in paths.ToArray()) if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            return paths.Where(File.Exists).OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => p, AuditHash);
        }
        static void MpCheckFiles(Dictionary<string, string> files, string output)
        {
            var report = new StringBuilder("path,before_sha256,after_sha256,unchanged\n"); var failures = new List<string>();
            foreach (var pair in files)
            {
                string after = AuditHash(pair.Key); bool unchanged = after == pair.Value;
                report.AppendLine(FacadeCsv(pair.Key) + "," + pair.Value + "," + after + "," + unchanged);
                if (!unchanged) failures.Add(pair.Key);
            }
            File.WriteAllText(output, report.ToString());
            if (failures.Count > 0) throw new InvalidOperationException("Protected source files changed: " + string.Join(", ", failures));
        }
    }
}
