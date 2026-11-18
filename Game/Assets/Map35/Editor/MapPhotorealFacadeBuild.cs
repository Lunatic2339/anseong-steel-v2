using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string FacadeFolder = "Assets/Map35/PhotorealFacades";
        const string FacadeSources = "Assets/ThirdParty/FreePBR/PhotorealFacades/";
        const string FacadeShaderName = "Anseong/PhotographicFacade";
        static string FacadeOutput => Path.GetFullPath("../docs/validation/map-photoreal-facades");
        static readonly int[] FacadeExistingViews = { 2, 5, 9, 12, 17, 20, 28, 30, 35, 37, 38, 39, 40, 47, 48, 49 };
        static readonly Vector3[] FacadeReflectance = {
            new Vector3(.35f,.36f,.32f),new Vector3(.22f,.27f,.29f),new Vector3(.17f,.27f,.23f),
            new Vector3(.27f,.13f,.085f),new Vector3(.20f,.26f,.31f),new Vector3(.32f,.33f,.31f),
            new Vector3(.48f,.29f,.06f),new Vector3(.13f,.15f,.16f),new Vector3(.25f,.30f,.23f) };
        static readonly Regex FacadePalettePattern = new Regex(@"^Wide_Palette_B(0[1-9])_(paint_[012]|roof|rust|metal|galvanized|concrete)$");
        static readonly Dictionary<string, float> facadeDiffuseMeans = new Dictionary<string, float>();

        [Serializable] sealed class FacadeIntegrity
        {
            public string scene_sha256, colliders, scenario_anchors, ground_materials, global_look, original_camera_poses;
        }
        [Serializable] sealed class FacadeViewRecord
        {
            public int index; public string name, file; public Vector3 position, forward; public float field_of_view;
        }
        [Serializable] sealed class FacadeBuildRecord
        {
            public string scope, generated_utc, scene_sha256, color_space, build_result;
            public int materials, source_materials, assigned_slots, inspection_cameras, before_captures, preview_captures, after_captures, lightmaps, reflection_probes;
            public bool source_materials_unchanged, colliders_unchanged, anchors_unchanged, ground_materials_unchanged, global_look_unchanged;
        }

        [MenuItem("Anseong/Map35/Apply Photographic Facades")]
        public static void ApplyPhotographicFacades()
        {
            EditorSceneManager.OpenScene(WideScene); ReconnectWideScene(); Directory.CreateDirectory(FacadeOutput);
            if (PlayerSettings.colorSpace != ColorSpace.Linear) throw new InvalidOperationException("Facade comparison requires the current Linear project.");
            var shader = FacadeCheckedShader();
            string backup = Path.GetFullPath("../SourceAssets/PhotorealFacades/BeforeApplication"); Directory.CreateDirectory(backup);
            string backupScene = backup + "/Steelworks35m_WideRuins.unity";
            if (!File.Exists(backupScene)) File.Copy(WideScene, backupScene);
            var before = FacadeReadIntegrity();
            string firstIntegrity = FacadeOutput + "/before-integrity.json";
            if (!File.Exists(firstIntegrity)) File.WriteAllText(firstIntegrity, JsonUtility.ToJson(before, true));
            else FacadeAssertIntegrity(JsonUtility.FromJson<FacadeIntegrity>(File.ReadAllText(firstIntegrity)), false);
            FacadeAddInspectionCameras();
            if (before.original_camera_poses != FacadeCameraSignature()) throw new InvalidOperationException("Adding facade views changed an existing camera.");
            FacadeCaptureViews("before", true);
            FacadeImportSources(); PurposeFolder(FacadeFolder + "/Materials"); facadeDiffuseMeans.Clear();
            var originals = new Dictionary<string, string>(); var derived = new Dictionary<string, Material>();
            var assignments = new StringBuilder("renderer,slot,source_material,derived_material,role,photo_source,physical_metres,linear_diffuse_mean,material_treatment\n");
            int slotsChanged = 0;
            var renderers = new HashSet<MeshRenderer>();
            for (int i = 1; i <= 9; i++) foreach (var r in Existing("B" + i.ToString("00")).GetComponentsInChildren<MeshRenderer>(true)) renderers.Add(r);
            if (districts != null) foreach (var r in districts.GetComponentsInChildren<MeshRenderer>(true)) renderers.Add(r);
            foreach (var renderer in renderers.OrderBy(r => PurposeGroundPath(r.transform), StringComparer.Ordinal))
            {
                if (FacadeProtectedRenderer(renderer) || FacadeIsFarLod(renderer.transform)) continue;
                var slots = renderer.sharedMaterials; bool changed = false;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    var current = slots[slot]; if (current == null || current.shader == null) continue;
                    if (current.shader.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string sourcePath = current.GetTag("FacadeSource", false, "");
                    var source = string.IsNullOrEmpty(sourcePath) ? current : AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                    if (source == null) throw new InvalidOperationException("Cannot resolve original facade material: " + sourcePath);
                    var match = FacadePalettePattern.Match(source.name); if (!match.Success) continue;
                    string role = match.Groups[2].Value; int theme = int.Parse(match.Groups[1].Value) - 1;
                    if (role == "concrete" && !FacadeAboveGroundConcrete(renderer)) continue;
                    bool wallPhoto = role.StartsWith("paint_", StringComparison.Ordinal) && FacadeIsCladding(renderer, theme, slot);
                    string treatment = role == "concrete" ? "wall-concrete" : wallPhoto ? "wall-cladding" : "existing-surface";
                    sourcePath = AssetDatabase.GetAssetPath(source);
                    if (!originals.ContainsKey(sourcePath)) originals.Add(sourcePath, AuditHash(sourcePath));
                    string variantKey = sourcePath + "|" + treatment;
                    if (!derived.TryGetValue(variantKey, out var material))
                    {
                        material = FacadeMaterial(source, sourcePath, shader, theme, role, wallPhoto, treatment);
                        derived.Add(variantKey, material);
                    }
                    slots[slot] = material; changed = true; slotsChanged++;
                    assignments.AppendLine(FacadeCsv(PurposeGroundPath(renderer.transform)) + "," + slot + "," + FacadeCsv(sourcePath) + ","
                        + FacadeCsv(AssetDatabase.GetAssetPath(material)) + "," + role + "," + FacadeCsv(AssetDatabase.GetAssetPath(material.mainTexture))
                        + "," + material.GetFloat("_TileMetres").ToString("R", Inv) + "," + material.GetFloat("_PhotoMean").ToString("R", Inv) + "," + treatment);
                }
                if (changed) renderer.sharedMaterials = slots;
            }
            // Existing far meshes combine wall sheets and fittings under one old palette
            // material. Regroup exactly the existing close geometry by the new material
            // choices so corrugation cannot leak onto distant stairs/supports.
            var lodRoots = Enumerable.Range(1, 9).Select(i => Existing("B" + i.ToString("00"))).ToList();
            if (districts != null) lodRoots.AddRange(districts.GetComponentsInChildren<LODGroup>(true).Select(l => l.transform));
            File.WriteAllText(FacadeOutput + "/lod-regroup.csv", "building,old_renderers,new_renderers,old_triangles,new_triangles,old_canonical_min,old_canonical_max,new_canonical_min,new_canonical_max,lod0_transition,lod1_transition,old_far_local_position,old_far_local_rotation,old_far_local_scale,world_center_delta,old_world_min,old_world_max,new_world_min,new_world_max,scene_instance_unpacked\n");
            foreach (var root in lodRoots.Distinct()) slotsChanged += FacadeRegroupFarLod(root, assignments);
            if (slotsChanged < 100 || derived.Count < 20) throw new InvalidOperationException("Facade assignment unexpectedly incomplete: " + slotsChanged + " slots / " + derived.Count + " materials.");
            AssetDatabase.SaveAssets(); FacadeAssertSources(originals);
            FacadeAssertIntegrity(before, false); ValidateFiniteMapGeometry(); FacadeCheckedShader();
            File.WriteAllText(FacadeOutput + "/material-assignments.csv", assignments.ToString());
            File.WriteAllText(FacadeOutput + "/source-material-hashes.csv", "path,sha256\n" + string.Join("\n", originals.OrderBy(p => p.Key).Select(p => FacadeCsv(p.Key) + "," + p.Value)) + "\n");
            File.WriteAllText(FacadeOutput + "/application-stats.txt", "Materials=" + derived.Count + "\nAssigned slots=" + slotsChanged + "\nNear building geometry, colliders, ground assignments, original palette materials, global exposure and scenario anchors preserved. Far LOD material grouping and legacy transform misalignment corrected; canonical triangle totals/bounds checked against the old mesh and close geometry. Original prefab/mesh assets preserved.\n");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), WideScene);
            var after = FacadeReadIntegrity(); File.WriteAllText(FacadeOutput + "/applied-integrity.json", JsonUtility.ToJson(after, true));
            FacadeCaptureViews("preview", false); FacadeAssertIntegrity(before, false); FacadeAssertSources(originals);
            var applied = new FacadeBuildRecord {
                scope = "Applied facade material copies and corrected existing far-LOD transform misalignment while preserving canonical triangle totals/bounds. 25 fixed Linear before views and 25 unbaked previews. Existing GI retained pending bake. Original source prefab/mesh/material assets, near geometry, ground, colliders, original cameras, global look and scenario anchors preserved. No HMD validation.",
                generated_utc = DateTime.UtcNow.ToString("O"), scene_sha256 = AuditHash(WideScene), color_space = PlayerSettings.colorSpace.ToString(), build_result = "not_built",
                materials = derived.Count, source_materials = originals.Count, assigned_slots = slotsChanged, inspection_cameras = 9,
                before_captures = Directory.GetFiles(FacadeOutput, "before-*.png").Length, preview_captures = Directory.GetFiles(FacadeOutput, "preview-*.png").Length,
                after_captures = Directory.GetFiles(FacadeOutput, "after-*.png").Length, lightmaps = LightmapSettings.lightmaps.Length,
                reflection_probes = Obj.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length,
                source_materials_unchanged = true, colliders_unchanged = true, anchors_unchanged = true, ground_materials_unchanged = true, global_look_unchanged = true };
            if (applied.before_captures != 25 || applied.preview_captures != 25) throw new InvalidOperationException("Facade before/preview capture set incomplete.");
            File.WriteAllText(FacadeOutput + "/application-validation.json", JsonUtility.ToJson(applied, true));
            Debug.Log("PHOTOGRAPHIC_FACADES_APPLIED materials=" + derived.Count + " slots=" + slotsChanged + " cameras=" + viewer.views.Length);
        }

        static Shader FacadeCheckedShader()
        {
            var shader = Shader.Find(FacadeShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("PhotographicFacade shader missing or contains compilation errors.");
            return shader;
        }
        static string FacadeCsv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        static bool FacadeProtectedRenderer(MeshRenderer r)
        {
            if (r.GetComponent<TextMesh>() != null) return true;
            foreach (var t in new[] { proxies, labels, routes, volumes, viewer.plate, viewer.coil, viewer.lateCoil == null ? null : viewer.lateCoil.transform })
                if (t != null && (r.transform == t || r.transform.IsChildOf(t))) return true;
            return viewer.scenario != null && r.transform.IsChildOf(viewer.scenario.transform);
        }
        static bool FacadeAboveGroundConcrete(MeshRenderer r)
        {
            string n = r.name.ToLowerInvariant();
            if (new[] { "foundation", "footing", "ground", "floor", "slab", "pavement", "hardstand", "apron", "walkway", "stair", "step", "landing" }.Any(n.Contains)) return false;
            if (r.transform.GetComponentsInParent<Transform>(true).Any(t => t.name == "LOD1 combined structure")) return false;
            return r.bounds.max.y > 1.15f && r.bounds.size.y > .4f;
        }
        static bool FacadeIsFarLod(Transform t) => t.GetComponentsInParent<Transform>(true).Any(p => p.name == "LOD1 combined structure");
        static bool FacadeIsCladding(MeshRenderer r, int theme, int slot)
        {
            // Source palette "paint" also means tread, fastener, base plate and vessel.
            // Only independently identifiable sheets receive a corrugated photograph.
            if (theme >= 5) return false; // B06 cylinder, B07 crane, B08/B09 mixed vessels.
            string n = r.name;
            if (n == "Source authored envelope") return (theme == 2 && (slot == 0 || slot == 3)) || ((theme == 3 || theme == 4) && slot == 0);
            if (n.StartsWith("SideWall_", StringComparison.Ordinal) || n.StartsWith("EndWall_", StringComparison.Ordinal)) return true;
            if (theme == 0 && new[] { "west wall", "east wall", "back wall", "left front pier", "right front pier", "door lintel 42m clear" }.Contains(n)) return true;
            return false;
        }
        static int FacadeRegroupFarLod(Transform root, StringBuilder assignments)
        {
            bool unpacked = PrefabUtility.IsPartOfPrefabInstance(root.gameObject);
            if (unpacked)
            {
                var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(root.gameObject);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            var group = root.GetComponent<LODGroup>(); if (group == null) return 0;
            var levels = group.GetLODs(); if (levels.Length != 2) throw new InvalidOperationException("Unexpected LOD layout: " + root.name);
            var oldFar = root.Find("LOD1 combined structure");
            if (oldFar == null) throw new InvalidOperationException("Missing existing far LOD: " + root.name);
            var oldRenderers = levels[1].renderers.OfType<MeshRenderer>().Where(r => r != null).ToArray();
            if (oldRenderers.Length == 0) throw new InvalidOperationException("Empty far LOD: " + root.name);
            var oldPosition = oldFar.localPosition; var oldRotation = oldFar.localRotation; var oldScale = oldFar.localScale;
            var buckets = new Dictionary<Material, List<CombineInstance>>();
            foreach (var renderer in levels[0].renderers.OfType<MeshRenderer>())
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled || FacadeProtectedRenderer(renderer)) continue;
                var filter = renderer.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) continue;
                string n = renderer.name.ToLowerInvariant();
                if (n.Contains("rivet") || n.Contains("vent slit") || n.Contains("ladder rung") || n.Contains("walkway grate")) continue;
                for (int slot = 0; slot < filter.sharedMesh.subMeshCount && slot < renderer.sharedMaterials.Length; slot++)
                {
                    var material = renderer.sharedMaterials[slot]; if (material == null) continue;
                    if (!buckets.TryGetValue(material, out var list)) { list = new List<CombineInstance>(); buckets.Add(material, list); }
                    list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = slot, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            PurposeFolder(FacadeFolder + "/Meshes");
            var pending = Group("Facade LOD regroup pending", root); pending.localPosition = Vector3.zero; pending.localRotation = Quaternion.identity; pending.localScale = Vector3.one;
            var fresh = new List<Renderer>(); long oldTriangles = 0, newTriangles = 0, closeTriangles = 0, ignored = 0;
            Bounds oldBounds = new Bounds(), newBounds = new Bounds(), closeBounds = new Bounds(), oldWorld = new Bounds(), newWorld = new Bounds();
            bool oldHas = false, newHas = false, closeHas = false, oldWorldHas = false, newWorldHas = false;
            // The old Group helper kept the far group's world origin/scale while meshes
            // had already been combined in building-local coordinates. Compare the old
            // raw far coordinate frame with the new identity frame; world displacement
            // below records the deliberate fix of that historical translation/scale bug.
            foreach (var renderer in oldRenderers)
            {
                FacadeMeshStats(renderer.GetComponent<MeshFilter>(), oldFar, ref oldTriangles, ref oldBounds, ref oldHas);
                FacadeMeshStats(renderer.GetComponent<MeshFilter>(), null, ref ignored, ref oldWorld, ref oldWorldHas);
            }
            foreach (var instance in buckets.Values.SelectMany(v => v))
            {
                closeTriangles += (long)instance.mesh.GetIndexCount(instance.subMeshIndex) / 3;
                foreach (var v in instance.mesh.vertices)
                { var p = instance.transform.MultiplyPoint3x4(v); if (!closeHas) { closeBounds = new Bounds(p, Vector3.zero); closeHas = true; } else closeBounds.Encapsulate(p); }
            }
            try
            {
                foreach (var pair in buckets.OrderBy(p => AssetDatabase.GetAssetPath(p.Key), StringComparer.Ordinal))
                {
                    string source = pair.Key.GetTag("FacadeSource", false, AssetDatabase.GetAssetPath(pair.Key));
                    var template = oldRenderers.FirstOrDefault(r => r.sharedMaterials.Any(m => m != null && m.GetTag("FacadeSource", false, AssetDatabase.GetAssetPath(m)) == source)) ?? oldRenderers[0];
                    var mesh = new Mesh { name = root.name.Split(' ')[0] + " facade LOD material regroup", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds();
                    string key = Hash128.Compute(PurposeGroundPath(root) + "/" + AssetDatabase.GetAssetPath(pair.Key)).ToString().Substring(0, 16);
                    string asset = FacadeFolder + "/Meshes/LOD1_" + key + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, asset); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); Obj.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                    var go = new GameObject("facade material group " + pair.Key.name); go.transform.SetParent(pending, false); go.layer = template.gameObject.layer;
                    go.AddComponent<MeshFilter>().sharedMesh = saved; var r = go.AddComponent<MeshRenderer>(); EditorUtility.CopySerialized(template, r); r.sharedMaterial = pair.Key;
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(template.gameObject)); fresh.Add(r);
                    FacadeMeshStats(go.GetComponent<MeshFilter>(), pending, ref newTriangles, ref newBounds, ref newHas);
                    FacadeMeshStats(go.GetComponent<MeshFilter>(), null, ref ignored, ref newWorld, ref newWorldHas);
                }
                if (oldTriangles != newTriangles || !oldHas || !newHas || Vector3.Distance(oldBounds.min, newBounds.min) > .005f || Vector3.Distance(oldBounds.max, newBounds.max) > .005f)
                    throw new InvalidOperationException("Facade LOD regroup changed geometry for " + root.name + ": triangles " + oldTriangles + " -> " + newTriangles + "; bounds " + oldBounds + " -> " + newBounds);
                if (newTriangles != closeTriangles || !closeHas || Vector3.Distance(closeBounds.min, newBounds.min) > .005f || Vector3.Distance(closeBounds.max, newBounds.max) > .005f)
                    throw new InvalidOperationException("Facade far LOD does not align with close eligible geometry: " + root.name);
                // The same triangle positions/UV channels are combined again, only their material grouping changes.
                Obj.DestroyImmediate(oldFar.gameObject); pending.name = "LOD1 combined structure";
                levels[1].renderers = fresh.ToArray(); group.SetLODs(levels); group.RecalculateBounds(); EditorUtility.SetDirty(group);
                string report = FacadeOutput + "/lod-regroup.csv";
                File.AppendAllText(report, FacadeCsv(root.name) + "," + oldRenderers.Length + "," + fresh.Count + "," + oldTriangles + "," + newTriangles
                    + "," + FacadeCsv(oldBounds.min.ToString("F5")) + "," + FacadeCsv(oldBounds.max.ToString("F5")) + "," + FacadeCsv(newBounds.min.ToString("F5")) + "," + FacadeCsv(newBounds.max.ToString("F5"))
                    + "," + levels[0].screenRelativeTransitionHeight.ToString("R", Inv) + "," + levels[1].screenRelativeTransitionHeight.ToString("R", Inv)
                    + "," + FacadeCsv(oldPosition.ToString("F5")) + "," + FacadeCsv(oldRotation.ToString("F5")) + "," + FacadeCsv(oldScale.ToString("F5"))
                    + "," + FacadeCsv((newWorld.center - oldWorld.center).ToString("F5")) + "," + FacadeCsv(oldWorld.min.ToString("F5")) + "," + FacadeCsv(oldWorld.max.ToString("F5"))
                    + "," + FacadeCsv(newWorld.min.ToString("F5")) + "," + FacadeCsv(newWorld.max.ToString("F5")) + "," + unpacked + "\n");
                int assigned = 0;
                foreach (var renderer in fresh.OfType<MeshRenderer>())
                {
                    var material = renderer.sharedMaterial; if (material.shader.name != FacadeShaderName) continue;
                    string source = material.GetTag("FacadeSource", false, ""); var original = AssetDatabase.LoadAssetAtPath<Material>(source);
                    var match = FacadePalettePattern.Match(original.name); string role = match.Groups[2].Value;
                    assignments.AppendLine(FacadeCsv(PurposeGroundPath(renderer.transform)) + ",0," + FacadeCsv(source) + "," + FacadeCsv(AssetDatabase.GetAssetPath(material)) + "," + role + ","
                        + FacadeCsv(AssetDatabase.GetAssetPath(material.mainTexture)) + "," + material.GetFloat("_TileMetres").ToString("R", Inv) + "," + material.GetFloat("_PhotoMean").ToString("R", Inv) + "," + material.GetTag("FacadeTreatment", false, "")); assigned++;
                }
                return assigned;
            }
            catch { if (pending != null && pending.name != "LOD1 combined structure") Obj.DestroyImmediate(pending.gameObject); throw; }
        }
        static void FacadeMeshStats(MeshFilter filter, Transform root, ref long triangles, ref Bounds bounds, ref bool hasBounds)
        {
            if (filter == null || filter.sharedMesh == null) return; var mesh = filter.sharedMesh;
            for (int i = 0; i < mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i) / 3;
            var transform = root == null ? filter.transform.localToWorldMatrix : root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (var v in mesh.vertices)
            { var p = transform.MultiplyPoint3x4(v); if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; } else bounds.Encapsulate(p); }
        }
        static void FacadeImportSources()
        {
            AssetDatabase.Refresh();
            foreach (string id in new[] { "concrete_wall_007", "corrugated_iron_02", "worn_corrugated_iron" })
            {
                string folder = FacadeSources + id;
                if (!Directory.Exists(folder)) throw new InvalidOperationException("Downloaded facade source folder missing: " + folder);
                foreach (string path0 in Directory.GetFiles(folder).Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
                {
                    string path = path0.Replace('\\', '/'); var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Texture importer missing: " + path);
                    importer.textureType = path.Contains("_nor_gl_") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = path.Contains("_diff_"); importer.convertToNormalmap = false; importer.isReadable = false;
                    importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.anisoLevel = 8;
                    importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
                }
                FacadeTexture(id, "diff", true); FacadeTexture(id, "nor_gl", true); FacadeTexture(id, "rough", true);
            }
        }
        static Texture2D FacadeTexture(string id, string channel, bool required = false)
        {
            string path = FacadeSources + id + "/" + id + "_" + channel + "_2k." + (channel == "diff" ? "jpg" : "png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (required && texture == null) throw new InvalidOperationException("Facade texture missing: " + path);
            return texture;
        }
        static float FacadeDiffuseMean(Texture texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new InvalidOperationException("Cannot measure facade diffuse source: " + path);
            if (facadeDiffuseMeans.TryGetValue(path, out float mean)) return mean;
            // Linear CPU texture retains the encoded bytes; convert each stored RGB sample
            // explicitly from sRGB. No source importer or GPU color readback is modified.
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(decoded, File.ReadAllBytes(path), false)) throw new InvalidOperationException("Cannot decode diffuse image: " + path);
                var pixels = decoded.GetPixels32(); double sum = 0;
                // A fixed 4-pixel stride is enough for a broad exposure statistic, not a texture bake.
                for (int i = 0; i < pixels.Length; i += 4)
                { Color linear = ((Color)pixels[i]).linear; sum += linear.r * .2126 + linear.g * .7152 + linear.b * .0722; }
                mean = Mathf.Max(.005f, (float)(sum / ((pixels.Length + 3) / 4))); facadeDiffuseMeans[path] = mean; return mean;
            }
            finally { Obj.DestroyImmediate(decoded); }
        }
        static Material FacadeMaterial(Material source, string sourcePath, Shader shader, int theme, string role, bool wallPhoto, string treatment)
        {
            string path = FacadeFolder + "/Materials/Photo_" + source.name + "_" + treatment + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(source); AssetDatabase.CreateAsset(result, path); }
            else { result.shader = source.shader; result.CopyPropertiesFromMaterial(source); }
            result.shader = shader; result.SetOverrideTag("FacadeSource", sourcePath); result.SetOverrideTag("FacadeTreatment", treatment);
            bool paint = role.StartsWith("paint_", StringComparison.Ordinal), concreteWall = role == "concrete";
            bool newPhoto = concreteWall || wallPhoto;
            string id = concreteWall ? "concrete_wall_007" : theme == 1 && role == "paint_1" ? "worn_corrugated_iron" : "corrugated_iron_02";
            if (newPhoto)
            {
                result.SetTexture("_MainTex", FacadeTexture(id, "diff", true)); result.SetTexture("_BumpMap", FacadeTexture(id, "nor_gl", true));
                result.SetTexture("_RoughnessMap", FacadeTexture(id, "rough", true)); result.SetTexture("_AOMap", FacadeTexture(id, "ao"));
                result.SetTexture("_MetallicMap", FacadeTexture(id, "metal"));
                result.SetFloat("_TileMetres", id == "concrete_wall_007" ? 2.16f : id == "corrugated_iron_02" ? 2.7f : 1.8f);
                result.SetVector("_TileAspect", Vector4.one);
            }
            var reflectance = concreteWall && !paint ? new Vector3(.32f, .31f, .28f) : FacadeReflectance[theme];
            result.SetVector("_Reflectance", new Vector4(reflectance.x, reflectance.y, reflectance.z, 1));
            result.SetFloat("_PhotoMean", FacadeDiffuseMean(result.mainTexture));
            result.SetFloat("_NormalizePhoto", concreteWall ? .35f : paint && theme != 5 ? 1 : role == "roof" ? .25f : 0);
            result.SetFloat("_PhotoColorRetention", wallPhoto && id == "worn_corrugated_iron" ? .7f : paint ? .16f : concreteWall ? .55f : .85f);
            result.SetColor("_Color", Color.white); result.SetFloat("_AlbedoGain", 1);
            result.SetFloat("_SurfaceCalm", 0); result.SetFloat("_Saturation", 1); result.SetFloat("_OcclusionStrength", .5f);
            result.SetFloat("_NormalStrength", concreteWall ? .65f : paint ? .55f : role == "rust" ? .55f : .35f);
            result.SetFloat("_RoughnessFloor", .18f); result.SetFloat("_RoughnessScale", paint && !concreteWall ? .65f : 1);
            result.SetFloat("_RoughnessBias", paint && !concreteWall ? .30f : 0);
            result.SetFloat("_MacroStrength", .06f); result.SetFloat("_GrimeStrength", concreteWall ? .20f : .15f);
            if (concreteWall || paint || role == "rust" || role == "roof") result.SetFloat("_Metallic", 0);
            if (role == "galvanized" || (theme == 5 && (paint || role == "roof"))) result.SetFloat("_Metallic", .8f);
            if (role == "roof") result.SetVector("_Reflectance", new Vector4(.12f, .135f, .14f, 1));
            bool rustChannels = result.GetTexture("_RustAlbedo") != null && result.GetTexture("_RustNormal") != null && result.GetTexture("_RustRoughness") != null;
            result.SetFloat("_WearAmount", theme == 1 && paint && rustChannels ? (role == "paint_1" ? .07f : .14f) : 0);
            result.mainTextureScale = Vector2.one; result.mainTextureOffset = Vector2.zero; result.enableInstancing = true;
            EditorUtility.SetDirty(result); return result;
        }

        static void FacadeAddInspectionCameras()
        {
            string[] names = Enumerable.Range(1, 9).Select(i => "FACADE B" + i.ToString("00")).ToArray();
            if (viewer.views.Length >= 59 && names.Select((n, i) => viewer.views[50 + i].name == n).All(x => x)) return;
            if (viewer.views.Length != 50) throw new InvalidOperationException("Expected the current 50 review views before appending facade cameras.");
            var positions = new[] { new Vector3(-78,19,-98),new Vector3(-142,12,11),new Vector3(62,7,22),new Vector3(26,16,100),
                new Vector3(-221,12,175),new Vector3(-195,11,306),new Vector3(10,36,475),new Vector3(11,30,632),new Vector3(-174,15,511) };
            var looks = new[] { new Vector3(-15,22,-25),new Vector3(-103,13,40),new Vector3(87,7,42),new Vector3(82,17,144),
                new Vector3(-161,12,228),new Vector3(-154,11,340),new Vector3(85,42,550),new Vector3(99,37,706),new Vector3(-109,15,550) };
            var views = viewer.views.ToList();
            for (int i = 0; i < 9; i++)
            {
                var c = Obj.Instantiate(viewer.views[37], cameras); c.name = names[i]; c.tag = "Untagged"; c.enabled = false;
                c.transform.position = positions[i]; c.transform.LookAt(looks[i]); c.fieldOfView = 60;
                c.clearFlags = CameraClearFlags.Skybox; c.backgroundColor = RenderSettings.fogColor;
                c.targetTexture = null; foreach (var listener in c.GetComponents<AudioListener>()) Obj.DestroyImmediate(listener);
                var layer = c.GetComponent<PostProcessLayer>(); if (layer != null) layer.volumeTrigger = c.transform;
                views.Add(c);
            }
            viewer.views = views.ToArray(); EditorUtility.SetDirty(viewer);
        }
        static void FacadeCaptureViews(string phase, bool preserve)
        {
            WarmSceneParticles(); var hidden = new List<(GameObject, bool)>();
            foreach (var go in new[] { viewer.scenario.gameObject, proxies.gameObject, viewer.labels, viewer.routes, viewer.frusta, viewer.reservations }.Distinct())
            { hidden.Add((go, go.activeSelf)); go.SetActive(false); }
            var records = new List<FacadeViewRecord>(); var camerasEnabled = viewer.views.Select(c => c.enabled).ToArray();
            try
            {
                foreach (int i in FacadeExistingViews.Concat(Enumerable.Range(50, 9)))
                {
                    var c = viewer.views[i]; string file = phase + "-" + i.ToString("00") + ".png";
                    if (!preserve || !File.Exists(FacadeOutput + "/" + file))
                    {
                        float aspect = c.aspect;
                        try { SaveWearCamera(c, FacadeOutput + "/" + file); }
                        finally { c.aspect = aspect; }
                    }
                    records.Add(new FacadeViewRecord { index = i, name = c.name, file = file, position = c.transform.position, forward = c.transform.forward, field_of_view = c.fieldOfView });
                }
                string csv = "index,name,file,x,y,z,forward_x,forward_y,forward_z,field_of_view\n" + string.Join("\n", records.Select(v =>
                    v.index + "," + FacadeCsv(v.name) + "," + v.file + "," + v.position.x.ToString("R", Inv) + "," + v.position.y.ToString("R", Inv) + "," + v.position.z.ToString("R", Inv)
                    + "," + v.forward.x.ToString("R", Inv) + "," + v.forward.y.ToString("R", Inv) + "," + v.forward.z.ToString("R", Inv) + "," + v.field_of_view.ToString("R", Inv))) + "\n";
                if (!preserve || !File.Exists(FacadeOutput + "/" + phase + "-cameras.csv")) File.WriteAllText(FacadeOutput + "/" + phase + "-cameras.csv", csv);
            }
            finally
            { for (int i = 0; i < viewer.views.Length; i++) viewer.views[i].enabled = camerasEnabled[i]; foreach (var pair in hidden) pair.Item1.SetActive(pair.Item2); }
            FacadeCheckedShader();
        }
        static string FacadeCameraSignature() => string.Join("\n", viewer.views.Take(50).Select((c, i) => i + "/" + c.name + "/" + c.transform.position.ToString("F5") + "/" + c.transform.rotation.ToString("F5") + "/" + c.fieldOfView.ToString("R", Inv)));
        static string FacadeGroundSignature()
        {
            return string.Join("\n", Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials.Select((m, i) => (r, m, i)))
                .Where(p => p.m != null && (p.m.shader.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0 || p.m.name.StartsWith("GroundEdge_", StringComparison.Ordinal)))
                .Select(p => PurposeGroundPath(p.r.transform) + "/" + p.i + "/" + AssetDatabase.GetAssetPath(p.m) + "/" + AuditHash(AssetDatabase.GetAssetPath(p.m))).OrderBy(s => s, StringComparer.Ordinal));
        }
        static string FacadeLookSignature()
        {
            var b = new StringBuilder(); b.Append(RenderSettings.ambientMode).Append(RenderSettings.ambientSkyColor).Append(RenderSettings.ambientEquatorColor).Append(RenderSettings.ambientGroundColor);
            b.Append(RenderSettings.ambientIntensity.ToString("R", Inv)).Append(RenderSettings.reflectionIntensity.ToString("R", Inv)).Append(RenderSettings.fog).Append(RenderSettings.fogColor)
                .Append(RenderSettings.fogMode).Append(RenderSettings.fogDensity.ToString("R", Inv)).Append(RenderSettings.fogStartDistance.ToString("R", Inv)).Append(RenderSettings.fogEndDistance.ToString("R", Inv));
            string sky = AssetDatabase.GetAssetPath(RenderSettings.skybox); b.Append(sky); if (File.Exists(sky)) b.Append(AuditHash(sky));
            foreach (var light in Obj.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(l => PurposeGroundPath(l.transform)))
                b.Append(PurposeGroundPath(light.transform)).Append(light.type).Append(light.color).Append(light.intensity.ToString("R", Inv)).Append(light.range.ToString("R", Inv)).Append(light.shadowStrength.ToString("R", Inv));
            foreach (var volume in Obj.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(v => v.name))
            { string p = AssetDatabase.GetAssetPath(volume.sharedProfile); b.Append(p); if (File.Exists(p)) b.Append(AuditHash(p)); }
            return b.ToString();
        }
        static FacadeIntegrity FacadeReadIntegrity() => new FacadeIntegrity { scene_sha256 = AuditHash(WideScene), colliders = GroundColliderSignature(), scenario_anchors = PhotoAnchorSignature(), ground_materials = FacadeGroundSignature(), global_look = FacadeLookSignature(), original_camera_poses = FacadeCameraSignature() };
        static bool FacadeGroundPreserved(string baseline, string current)
        {
            var oldRows = baseline.Split('\n'); var newRows = current.Split('\n');
            if (oldRows.Except(newRows).Any()) return false;
            string assetHash(string row) { int i = row.IndexOf("/Assets/", StringComparison.Ordinal); return i < 0 ? "" : row.Substring(i + 1); }
            var originals = new HashSet<string>(oldRows.Select(assetHash).Where(s => s.Length > 0));
            var added = newRows.Except(oldRows).ToArray();
            // The regrouped far LOD now mirrors B02's existing photographic floor.
            // Permit only additional LOD slots with an already-protected, identical
            // material path AND file hash. Existing assignments cannot disappear.
            if (added.Any(row => !row.Contains("/LOD1 combined structure/") || !originals.Contains(assetHash(row)))) return false;
            if (added.Length > 0) File.WriteAllText(FacadeOutput + "/ground-lod-additions.txt", string.Join("\n", added) + "\nExisting near-ground assignments and material hashes are unchanged; added far-LOD slots reuse those exact assets.\n");
            return true;
        }
        static void FacadeAssertIntegrity(FacadeIntegrity baseline, bool allowScenarioCameraReview)
        {
            var current = FacadeReadIntegrity();
            bool colliders = baseline.colliders == current.colliders, anchors = baseline.scenario_anchors == current.scenario_anchors,
                ground = FacadeGroundPreserved(baseline.ground_materials, current.ground_materials), look = baseline.global_look == current.global_look,
                cameraPoses = baseline.original_camera_poses == current.original_camera_poses;
            if (!colliders || !anchors || !ground || !look || (!allowScenarioCameraReview && !cameraPoses))
            {
                Directory.CreateDirectory(FacadeOutput);
                File.WriteAllText(FacadeOutput + "/current-integrity-failure.json", JsonUtility.ToJson(current, true));
                File.WriteAllText(FacadeOutput + "/baseline-integrity-failure.json", JsonUtility.ToJson(baseline, true));
                string status = "colliders_unchanged=" + colliders + " anchors_unchanged=" + anchors + " ground_materials_unchanged=" + ground
                    + " global_look_unchanged=" + look + " original_camera_poses_unchanged=" + cameraPoses + " allow_scenario_camera_review=" + allowScenarioCameraReview;
                var diff = new StringBuilder(status + "\n");
                foreach (var pair in new[] { ("colliders", baseline.colliders, current.colliders), ("scenario_anchors", baseline.scenario_anchors, current.scenario_anchors),
                    ("ground_materials", baseline.ground_materials, current.ground_materials), ("global_look", baseline.global_look, current.global_look),
                    ("original_camera_poses", baseline.original_camera_poses, current.original_camera_poses) })
                {
                    if (pair.Item2 == pair.Item3) continue;
                    var oldLines = pair.Item2.Split('\n'); var newLines = pair.Item3.Split('\n');
                    diff.AppendLine("[" + pair.Item1 + "] baseline_only:"); foreach (string line in oldLines.Except(newLines).Take(100)) diff.AppendLine(line);
                    diff.AppendLine("[" + pair.Item1 + "] current_only:"); foreach (string line in newLines.Except(oldLines).Take(100)) diff.AppendLine(line);
                }
                File.WriteAllText(FacadeOutput + "/integrity-failure-diff.txt", diff.ToString());
                throw new InvalidOperationException("Facade integrity failure: " + status + ". See current-integrity-failure.json and integrity-failure-diff.txt.");
            }
        }
        static void FacadeAssertSources(Dictionary<string, string> originals)
        { foreach (var pair in originals) if (AuditHash(pair.Key) != pair.Value) throw new InvalidOperationException("Original facade material modified: " + pair.Key); }

        [MenuItem("Anseong/Map35/Bake Build Photographic Facades")]
        public static void BakeBuildPhotographicFacades()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) throw new InvalidOperationException("Fresh Linear editor required.");
            EditorSceneManager.OpenScene(WideScene); ReconnectWideScene(); FacadeCheckedShader(); Directory.CreateDirectory(FacadeOutput);
            if (viewer.views.Length < 59) throw new InvalidOperationException("ApplyPhotographicFacades must run first.");
            var before = FacadeReadIntegrity();
            var lodBounds = new StringBuilder("building,old_reference,old_size,new_reference,new_size,lod0_transition,lod1_transition\n");
            RepairFacadeLampSupport();
            for (int i = 1; i <= 9; i++)
            {
                var root = Existing("B" + i.ToString("00")); var group = root.GetComponent<LODGroup>(); if (group == null) continue;
                Vector3 oldReference = group.localReferencePoint; float oldSize = group.size; var levels = group.GetLODs();
                group.RecalculateBounds(); EditorUtility.SetDirty(group);
                var updated = group.GetLODs();
                if (levels.Length != updated.Length || levels.Where((l, n) => l.screenRelativeTransitionHeight != updated[n].screenRelativeTransitionHeight || l.fadeTransitionWidth != updated[n].fadeTransitionWidth).Any())
                    throw new InvalidOperationException("Facade LOD bounds recalculation changed transition thresholds.");
                lodBounds.AppendLine(FacadeCsv(root.name) + "," + FacadeCsv(oldReference.ToString("F5")) + "," + oldSize.ToString("R", Inv) + ","
                    + FacadeCsv(group.localReferencePoint.ToString("F5")) + "," + group.size.ToString("R", Inv) + "," + levels[0].screenRelativeTransitionHeight.ToString("R", Inv) + "," + levels[1].screenRelativeTransitionHeight.ToString("R", Inv));
            }
            File.WriteAllText(FacadeOutput + "/lod-bounds-calibration.csv", lodBounds.ToString());
            PrepareZ1Lighting(); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), WideScene);
            Debug.Log("PHOTOGRAPHIC_FACADES_BAKE_START");
            if (!Lightmapping.Bake() || LightmapSettings.lightmaps.Length == 0) throw new InvalidOperationException("Facade GI bake failed.");
            WarmSceneParticles(); DynamicGI.UpdateEnvironment();
            // Existing helper rebuilds the same seven local probes. Preserve visibility toggles.
            var hiddenStates = new[] { viewer.labels, viewer.routes, proxies.gameObject, viewer.reservations }.Select(g => (g, g.activeSelf)).ToArray();
            try { BakeLocalReflections(); } finally { foreach (var pair in hiddenStates) pair.g.SetActive(pair.activeSelf); }
            AssetDatabase.SaveAssets(); FacadeAssertIntegrity(before, false);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), WideScene);
            FacadeCaptureViews("after", false); FacadeCheckedShader();
            ValidateScenarioSpatialChecks(viewer.scenario); ValidateMaintenanceAccess(); ValidateFiniteMapGeometry();
            viewer.scenario.SelectStep(0); viewer.scenario.SelectRole(0); FacadeAssertIntegrity(before, true);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), WideScene); FacadeCheckedShader();
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { WideScene }, locationPathName = "../Builds/MapWide/AnseongMapWide.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Photographic facade viewer build failed.");
            FacadeCheckedShader();
            var sourceRows = File.ReadAllLines(FacadeOutput + "/source-material-hashes.csv").Skip(1).Where(x => x.Length > 0);
            var sources = new Dictionary<string, string>();
            foreach (var row in sourceRows) { int split = row.LastIndexOf(','); sources.Add(row.Substring(0, split).Trim('"').Replace("\"\"", "\""), row.Substring(split + 1)); }
            FacadeAssertSources(sources);
            var stats = new FacadeBuildRecord {
                scope = "External photographic facade materials on B01-B09 and matching district/LOD slots; legacy far-LOD translation/scale misalignment corrected with canonical mesh triangle/bounds checks. Same Linear lighting/exposure; refreshed Z1 indirect GI and seven local reflections. 25 fixed before/after mono Editor cameras. Near geometry, ground, source prefab/mesh/material assets and scenario mechanics preserved; no HMD or frame-time validation.",
                generated_utc = DateTime.UtcNow.ToString("O"), scene_sha256 = AuditHash(WideScene), color_space = PlayerSettings.colorSpace.ToString(), build_result = result.summary.result.ToString(),
                materials = Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null && m.shader.name == FacadeShaderName).Select(m => AssetDatabase.GetAssetPath(m)).Distinct().Count(),
                source_materials = sources.Count, assigned_slots = File.ReadLines(FacadeOutput + "/material-assignments.csv").Count() - 1, inspection_cameras = 9,
                before_captures = Directory.GetFiles(FacadeOutput, "before-*.png").Length, after_captures = Directory.GetFiles(FacadeOutput, "after-*.png").Length,
                preview_captures = Directory.GetFiles(FacadeOutput, "preview-*.png").Length,
                lightmaps = LightmapSettings.lightmaps.Length, reflection_probes = Obj.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length,
                source_materials_unchanged = true, colliders_unchanged = true, anchors_unchanged = true, ground_materials_unchanged = true, global_look_unchanged = true };
            if (stats.before_captures != 25 || stats.after_captures != 25) throw new InvalidOperationException("Facade comparison capture set incomplete.");
            File.WriteAllText(FacadeOutput + "/build-validation.json", JsonUtility.ToJson(stats, true));
            File.WriteAllText(FacadeOutput + "/build-validation.txt", stats.scope + "\nScene SHA256=" + stats.scene_sha256 + "\nMaterials=" + stats.materials + "\nAssigned slots=" + stats.assigned_slots
                + "\nBefore/after captures=" + stats.before_captures + "/" + stats.after_captures + "\nLightmaps=" + stats.lightmaps + "\nLocal reflections=" + stats.reflection_probes + "\nAll protected integrity signatures and original material hashes unchanged.\n");
            Debug.Log("PHOTOGRAPHIC_FACADES_BUILD_OK materials=" + stats.materials + " captures=25+25");
        }
    }
}
