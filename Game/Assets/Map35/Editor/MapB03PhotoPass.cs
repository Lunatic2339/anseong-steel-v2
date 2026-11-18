using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static bool b03PhotoPassStarted;
        public static void ReviewB03ProjectedUnlit() => B03RunPhotoPass("projected-unlit", true, false, false);
        public static void ReviewB03ProjectedLit() => B03RunPhotoPass("projected-lit", false, false, false);
        public static void ReviewB03ProjectedLitAdjusted() => B03RunPhotoPass("projected-lit-adjusted", false, true, false);
        public static void ReviewB03RectifiedLitAdjusted() => B03RunPhotoPass("rectified-lit-adjusted", false, true, true);

        // Three independent fresh-process conditions on one saved Meshy Retexture scene.
        // No assets/scenes/profiles are saved by this diagnostic. The ambient condition
        // builds on probe-off; compare ambient vs off to isolate ambient illumination.
        public static void ReviewB03MeshyLightProbeDiagnostic()
        {
            if (b03PhotoPassStarted) throw new InvalidOperationException("Run one lighting condition per fresh Unity process.");
            b03PhotoPassStarted = true;
            string mode = RepArg("-probeMode", "original");
            if (!new[] { "original", "off", "ambient" }.Contains(mode)) throw new ArgumentException("-probeMode must be original|off|ambient");
            string scene = ExtB03 + "/Scenes/B03_MeshyRetexture.unity";
            if (!File.Exists(scene)) throw new FileNotFoundException("Build the original-look Retexture candidate before this diagnostic", scene);
            EditorSceneManager.OpenScene(scene); ReconnectWideScene();
            var root = GameObject.Find("B03 Meshy Retexture candidate");
            if (root == null) throw new InvalidOperationException("Meshy Retexture candidate root missing.");
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Retexture renderers missing.");
            string output = Path.GetFullPath("../docs/validation/b03-meshy-review-2026-11-15/probe-diagnostic/" + mode);
            Directory.CreateDirectory(output);
            var protectedFiles = B03PhotoProtectedFiles(root); protectedFiles[scene] = AuditHash(scene);
            foreach (string path in AssetDatabase.GetDependencies(scene, true))
                if (File.Exists(path) && (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(PostProcessProfile))) protectedFiles[path] = AuditHash(path);
            string look = FacadeLookSignature(), poses = ExtB03CameraSignature(), anchors = PhotoAnchorSignature();
            var ambientBefore = RenderSettings.ambientProbe;
            var settings = new StringBuilder("renderer,center_x,center_y,center_z,receive_gi,light_probe_usage_before,light_probe_usage_after,probe_anchor,lightmap_index,reflection_probe_usage\n");
            var directions = new StringBuilder("sample,direction,r_linear,g_linear,b_linear,luminance_linear\n");
            var coefficients = new StringBuilder("sample,channel,coefficient,value\n");
            var report = new StringBuilder();
            report.AppendLine("Mode=" + mode + "; fresh process=" + System.Diagnostics.Process.GetCurrentProcess().Id + "; scene=" + scene);
            report.AppendLine("Controlled source=original-look saved Retexture scene. Same geometry/PBR/camera52/postprocessing/original lights/reflection probes; no exposure or extra light adjustment.");
            report.AppendLine("Ambient before: mode=" + RenderSettings.ambientMode + "; intensity=" + RenderSettings.ambientIntensity.ToString("R", Inv) + "; ambientLight=" + RenderSettings.ambientLight + "; sky=" + RenderSettings.ambientSkyColor + "; equator=" + RenderSettings.ambientEquatorColor + "; ground=" + RenderSettings.ambientGroundColor);
            report.AppendLine("Light probes count=" + (LightmapSettings.lightProbes == null ? 0 : LightmapSettings.lightProbes.count) + "; baked lightmaps=" + LightmapSettings.lightmaps.Length + "; active color space=" + QualitySettings.activeColorSpace);
            B03LogProbeSH("ambient-before", ambientBefore, directions, coefficients);
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
                SphericalHarmonicsL2 probe; LightProbes.GetInterpolatedProbe(r.bounds.center, r, out probe);
                B03LogProbeSH("renderer-center:" + r.name, probe, directions, coefficients);
                var prior = r.lightProbeUsage;
                if (mode != "original") r.lightProbeUsage = LightProbeUsage.Off;
                settings.AppendLine(FacadeCsv(r.name) + "," + RepVec(r.bounds.center) + "," + r.receiveGI + "," + prior + "," + r.lightProbeUsage + "," + FacadeCsv(r.probeAnchor == null ? "bounds center" : r.probeAnchor.name) + "," + r.lightmapIndex + "," + r.reflectionProbeUsage);
                if (r.probeAnchor != null)
                {
                    LightProbes.GetInterpolatedProbe(r.probeAnchor.position, r, out probe);
                    B03LogProbeSH("actual-anchor:" + r.name, probe, directions, coefficients);
                }
            }
            var samplePoints = new[] { bounds.center, new Vector3(bounds.min.x - 1, bounds.center.y, bounds.center.z), new Vector3(bounds.max.x + 1, bounds.center.y, bounds.center.z), new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 1), new Vector3(bounds.center.x, bounds.max.y + 1, bounds.center.z) };
            var sampleNames = new[] { "building-center", "west-outside-1m", "east-outside-1m", "south-outside-1m", "above-roof-1m" };
            for (int i = 0; i < samplePoints.Length; i++)
            {
                SphericalHarmonicsL2 sample; LightProbes.GetInterpolatedProbe(samplePoints[i], null, out sample);
                B03LogProbeSH(sampleNames[i] + ":" + RepVec(samplePoints[i]), sample, directions, coefficients);
            }
            if (mode == "ambient")
            {
                Color flatSRGB = new Color(.8f, .8f, .8f, 1);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = flatSRGB;
                RenderSettings.ambientIntensity = 1;
                // An explicitly supplied constant SH avoids async sky-capture timing.
                // The 0.8 inspector-style colour is converted once for linear SH input.
                var flatProbe = new SphericalHarmonicsL2(); flatProbe.AddAmbientLight(flatSRGB.linear);
                RenderSettings.ambientProbe = flatProbe;
                report.AppendLine("Ambient intervention: candidate renderers LightProbeUsage.Off + ambientMode Flat + ambientLight sRGB=(0.8,0.8,0.8) + ambientIntensity=1; explicit constant ambient SH from linear=" + flatSRGB.linear + ". No DynamicGI async update or rebake.");
                report.AppendLine("Interpret ambient against off, not original alone: this condition includes both probe-off and the deliberate brighter ambient intervention.");
            }
            else report.AppendLine(mode == "off" ? "Only candidate renderer LightProbeUsage changed to Off; all global lighting remains original." : "No probe or lighting properties changed.");
            B03LogProbeSH("ambient-at-capture", RenderSettings.ambientProbe, directions, coefficients);
            RepHideOverlays(); var camera = viewer.views[52]; float oldAspect = camera.aspect;
            report.AppendLine("Camera52=" + camera.name + "; position=" + RepVec(camera.transform.position) + "; rotation=" + RepVec(camera.transform.eulerAngles) + "; verticalFOV=" + camera.fieldOfView.ToString("R", Inv) + "; capture=1600x1000");
            try { SaveWearCamera(camera, output + "/camera52.png"); }
            finally { camera.aspect = oldAspect; }
            B03LogProbeSH("ambient-after-capture", RenderSettings.ambientProbe, directions, coefficients);
            if (mode != "ambient" && look != FacadeLookSignature()) throw new InvalidOperationException("Probe-only diagnostic changed the global look.");
            if (poses != ExtB03CameraSignature() || anchors != PhotoAnchorSignature()) throw new InvalidOperationException("Probe diagnostic changed camera or scenario anchors.");
            var hashes = new StringBuilder("path,sha256_before,sha256_after,unchanged\n");
            foreach (var p in protectedFiles)
            {
                string after = AuditHash(p.Key); hashes.AppendLine(FacadeCsv(p.Key) + "," + p.Value + "," + after + "," + (after == p.Value));
                if (after != p.Value) throw new InvalidOperationException("Probe diagnostic changed protected file: " + p.Key);
            }
            report.AppendLine("Protected files unchanged=" + protectedFiles.Count + "; no scene/asset/profile save. Original textures, shadow settings and geometry unchanged.");
            report.AppendLine("SH coefficients and six directional evaluations are evidence only. Dark center SH does not prove the former building caused it; compare original/off images and outside samples before attributing causality. Reflections, direct light/shadows, exposure and albedo remain possible contributors.");
            File.WriteAllText(output + "/report.txt", report.ToString());
            File.WriteAllText(output + "/renderer-settings.csv", settings.ToString());
            File.WriteAllText(output + "/probe-directional.csv", directions.ToString());
            File.WriteAllText(output + "/probe-coefficients.csv", coefficients.ToString());
            File.WriteAllText(output + "/protected-files.csv", hashes.ToString());
            Debug.Log("B03_MESHY_PROBE_DIAGNOSTIC_OK " + mode + " output=" + output);
        }

        static void B03LogProbeSH(string name, SphericalHarmonicsL2 sh, StringBuilder directions, StringBuilder coefficients)
        {
            var vectors = new[] { Vector3.left, Vector3.right, Vector3.down, Vector3.up, Vector3.back, Vector3.forward };
            var names = new[] { "west-X", "east+X", "down-Y", "up+Y", "south-Z", "north+Z" };
            var colours = new Color[vectors.Length]; sh.Evaluate(vectors, colours);
            for (int i = 0; i < vectors.Length; i++)
            {
                var c = colours[i]; float luminance = .2126f * c.r + .7152f * c.g + .0722f * c.b;
                directions.AppendLine(FacadeCsv(name) + "," + names[i] + "," + c.r.ToString("R", Inv) + "," + c.g.ToString("R", Inv) + "," + c.b.ToString("R", Inv) + "," + luminance.ToString("R", Inv));
            }
            for (int channel = 0; channel < 3; channel++) for (int coefficient = 0; coefficient < 9; coefficient++)
                coefficients.AppendLine(FacadeCsv(name) + "," + channel + "," + coefficient + "," + sh[channel, coefficient].ToString("R", Inv));
        }

        // Invoke exactly one entry point in each fresh Unity process. No production
        // scene, source material, shared grading profile, or original light is saved.
        static void B03RunPhotoPass(string phase, bool diagnostic, bool adjusted, bool rectified)
        {
            if (b03PhotoPassStarted) throw new InvalidOperationException("One B03 photo condition per fresh Unity process is required.");
            b03PhotoPassStarted = true;
            string assets = ExtB03 + "/PhotoPasses/" + phase;
            string output = ExtB03Output + "/photo-passes/" + phase;
            PurposeFolder(assets + "/Materials"); PurposeFolder(assets + "/Profiles");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(ExtB03Scene); ReconnectWideScene();
            var building = GameObject.Find(ExtB03Root);
            if (building == null || building.transform.Find("Annex forward low roof") == null)
                throw new InvalidOperationException("The saved stage02 B03 morphology is required; do not rebuild from production here.");
            if (viewer.views.Length <= 52 || viewer.views[52] == null) throw new InvalidOperationException("Reference camera52 missing.");
            var camera = viewer.views[52];
            var protectedFiles = B03PhotoProtectedFiles(building);
            string originalLook = FacadeLookSignature(), cameraSignature = ExtB03CameraSignature(), anchors = PhotoAnchorSignature();
            var originalLights = Obj.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToDictionary(l => l, l => EditorJsonUtility.ToJson(l));
            var report = new StringBuilder();
            report.AppendLine("Phase=" + phase + "\nFresh process id=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            report.AppendLine("Input=" + ExtB03Scene + "\nProjection reference=camera52,1586x992; UV4=(viewport.u*depth,viewport.v*depth,depth)");
            report.AppendLine("Capture=1600x1000 mono Editor; same camera52 position/rotation/FOV. No Unity/HMD/performance acceptance.");
            report.AppendLine("Camera52=" + camera.name + "; position=" + RepVec(camera.transform.position) + "; rotation=" + RepVec(camera.transform.eulerAngles) + "; verticalFOV=" + camera.fieldOfView.ToString("R", Inv));
            report.AppendLine("DiagnosticUnlit=" + diagnostic + "; generated images retain baked glints/shading and are NOT verified PBR albedo.");
            report.AppendLine("Original lights and ambient are retained. No GI or reflection rebake is performed in these material comparisons.");
            report.AppendLine("Photo materials use geometric normals only; unrelated source brick/metal normals and surface maps are disabled.");
            var reference = B03PhotoImport("reference_b03_full.png", report);
            var brick = B03PhotoImport("b03_brick_wall_albedo.png", report);
            var annex = B03PhotoImport("b03_annex_wall_albedo.png", report);
            var shader = Shader.Find("Anseong/B03PhotoSurface");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("B03PhotoSurface shader missing or unsupported.");
            var assignments = new StringBuilder("renderer,slot,original_material,result_material,operation,photo\n");
            var created = new Dictionary<string, Material>();
            int changedSlots = 0, glassSlots = 0, changedRenderers = 0;
            foreach (var r in building.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = r.name;
                // The low entry strip has its own facade proportions and a personnel
                // door. The six-window rectified west image must never be copied there.
                bool west = name.StartsWith("West ", StringComparison.Ordinal) || (!rectified && name.StartsWith("Low west brick", StringComparison.Ordinal));
                bool southBrick = name.StartsWith("South brick", StringComparison.Ordinal);
                bool front = name.StartsWith("Annex south", StringComparison.Ordinal);
                if (name.IndexOf("inside", StringComparison.OrdinalIgnoreCase) >= 0 || !(west || front || (!rectified && southBrick))) continue;
                B03ValidatePhotoUV(r, !rectified);
                Texture2D image = rectified ? (front ? annex : brick) : reference;
                var materials = r.sharedMaterials;
                bool changed = false;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var source = materials[slot]; if (source == null) continue;
                    string identity = source.name + " " + source.GetTag("ExternalOriginalMaterial", false, "");
                    bool glass = identity.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 || source.renderQueue >= 3000;
                    if (glass && !diagnostic)
                    {
                        glassSlots++;
                        assignments.AppendLine(FacadeCsv(name) + "," + slot + "," + FacadeCsv(source.name) + "," + FacadeCsv(source.name) + ",retained authored glass,");
                        continue;
                    }
                    string key = (front ? "metal-front" : "brick") + (rectified ? "-rectified" : "-projected");
                    if (!created.TryGetValue(key, out var replacement))
                    {
                        var fresh = new Material(shader) { name = "B03 " + phase + " " + key };
                        fresh.SetTexture("_MainTex", image); fresh.SetTextureScale("_MainTex", Vector2.one); fresh.SetTextureOffset("_MainTex", Vector2.zero);
                        fresh.SetColor("_BaseColor", Color.white); fresh.SetFloat("_AlbedoGain", 1);
                        fresh.SetFloat("_UsePhotoUV3", 1); fresh.SetFloat("_UseCameraUV4", rectified ? 0 : 1);
                        fresh.SetFloat("_UseReferenceVP", 0); fresh.SetVector("_ReferenceUVScaleOffset", new Vector4(1, 1, 0, 0));
                        fresh.SetFloat("_DiagnosticUnlit", diagnostic ? 1 : 0);
                        fresh.SetFloat("_Roughness", front ? .68f : .82f); fresh.SetFloat("_Metallic", 0);
                        fresh.SetFloat("_UseDetailNormal", 0); fresh.SetFloat("_UseRoughnessMap", 0); fresh.SetFloat("_UseMetallicMap", 0);
                        fresh.SetOverrideTag("B03PhotoCondition", phase);
                        string path = assets + "/Materials/" + key + ".mat";
                        replacement = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (replacement == null) { AssetDatabase.CreateAsset(fresh, path); replacement = fresh; }
                        else { EditorUtility.CopySerialized(fresh, replacement); Obj.DestroyImmediate(fresh); EditorUtility.SetDirty(replacement); }
                        created.Add(key, replacement);
                    }
                    materials[slot] = replacement; changed = true; changedSlots++;
                    assignments.AppendLine(FacadeCsv(name) + "," + slot + "," + FacadeCsv(source.name) + "," + FacadeCsv(replacement.name) + ",photo on actual geometry," + FacadeCsv(AssetDatabase.GetAssetPath(image)));
                }
                if (changed) { r.sharedMaterials = materials; changedRenderers++; }
            }
            if (changedSlots == 0) throw new InvalidOperationException("No real facade material slots were assigned.");
            if (!diagnostic && created.Values.Any(m => m.GetFloat("_DiagnosticUnlit") != 0)) throw new InvalidOperationException("Unlit material leaked into a lit test.");
            if (adjusted) B03PhotoAdjustCandidate(assets, camera, report);
            else if (originalLook != FacadeLookSignature()) throw new InvalidOperationException("Unadjusted phase changed global look.");
            report.AppendLine("Changed renderers=" + changedRenderers + "; photo slots=" + changedSlots + "; separate authored glass slots retained=" + glassSlots);
            report.AppendLine("Unselected faces, interior backs, roofs, authored ducts/stairs/doors and original source materials remain unchanged.");
            RepHideOverlays();
            string variant = assets + "/B03_" + phase + ".unity";
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), variant);
            report.AppendLine("Variant scene=" + variant);
            float oldAspect = camera.aspect; Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
            try
            {
                SaveWearCamera(camera, output + "/camera52-center.png");
                if (adjusted && !diagnostic)
                {
                    Vector3 right = camera.transform.right;
                    camera.transform.position = oldPosition - 2 * right;
                    SaveWearCamera(camera, output + "/camera52-left2m.png");
                    camera.transform.position = oldPosition + 2 * right;
                    SaveWearCamera(camera, output + "/camera52-right2m.png");
                    report.AppendLine("Crosschecks=translation +/-2m along original camera right; rotation/FOV fixed, projected photo remains anchored to original camera52.");
                }
            }
            finally { camera.aspect = oldAspect; camera.transform.SetPositionAndRotation(oldPosition, oldRotation); }
            if (cameraSignature != ExtB03CameraSignature() || anchors != PhotoAnchorSignature()) throw new InvalidOperationException("Original cameras or scenario anchors changed.");
            foreach (var p in originalLights) if (p.Key == null || EditorJsonUtility.ToJson(p.Key) != p.Value) throw new InvalidOperationException("An original light was changed.");
            var hashes = new StringBuilder("path,sha256_before,sha256_after,unchanged\n");
            foreach (var p in protectedFiles)
            {
                string after = AuditHash(p.Key); bool unchanged = after == p.Value;
                hashes.AppendLine(FacadeCsv(p.Key) + "," + p.Value + "," + after + "," + unchanged);
                if (!unchanged) throw new InvalidOperationException("Photo experiment changed protected file: " + p.Key);
            }
            report.AppendLine("Protected files unchanged=" + protectedFiles.Count + "; production scene/source candidate scene/materials/profiles/source meshes/photo image bytes preserved.");
            report.AppendLine("Unlit output is a diagnostic geometry projection, never a final photoreal result. Camera grading/fog still apply.");
            report.AppendLine("A convincing center frame alone does not establish angle consistency, physically correct albedo, production suitability, or VR readiness.");
            File.WriteAllText(output + "/assignments.csv", assignments.ToString());
            File.WriteAllText(output + "/protected-files.csv", hashes.ToString());
            File.WriteAllText(output + "/report.txt", report.ToString());
            Debug.Log("B03_PHOTO_PASS_OK " + phase + " assigned=" + changedSlots + " output=" + output);
        }

        static Dictionary<string, string> B03PhotoProtectedFiles(GameObject building)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { WideScene, ExtB03Scene };
            foreach (string scene in new[] { WideScene, ExtB03Scene })
                foreach (string path in AssetDatabase.GetDependencies(scene, true))
                    if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(PostProcessProfile)) paths.Add(path);
            foreach (var f in building.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) paths.Add(AssetDatabase.GetAssetPath(f.sharedMesh));
            foreach (string name in new[] { "reference_b03_full.png", "b03_brick_wall_albedo.png", "b03_annex_wall_albedo.png" }) paths.Add(ExtB03 + "/Textures/" + name);
            return paths.Where(File.Exists).OrderBy(p => p).ToDictionary(p => p, AuditHash);
        }

        static Texture2D B03PhotoImport(string name, StringBuilder report)
        {
            string path = ExtB03 + "/Textures/" + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Required native wall/reference image missing", path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.ClearPlatformTextureSettings("Standalone"); importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var png = File.ReadAllBytes(path);
            int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            if (texture.width != width || texture.height != height)
                throw new InvalidOperationException("Native photo dimensions were resampled: " + path + " source=" + width + "x" + height + " imported=" + texture.width + "x" + texture.height);
            report.AppendLine("Photo=" + path + "; imported native dimensions=" + texture.width + "x" + texture.height + "; sRGB/uncompressed/mipmaps/NPOT-none/clamp; original bytes unchanged.");
            return texture;
        }

        static void B03ValidatePhotoUV(MeshRenderer r, bool projected)
        {
            var filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Actual wall mesh missing: " + r.name);
            var mesh = filter.sharedMesh; var values = new List<Vector3>(); mesh.GetUVs(projected ? 3 : 2, values);
            if (values.Count != mesh.vertexCount) throw new InvalidOperationException("Prepared wall UV channel missing: " + r.name);
            if (values.Any(v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || (projected && (v.z <= 0 || float.IsNaN(v.z) || float.IsInfinity(v.z)))))
                throw new InvalidOperationException("Invalid fixed-camera UV4 depth or wall UV3: " + r.name);
        }

        static void B03PhotoAdjustCandidate(string assets, Camera camera, StringBuilder report)
        {
            // Deep-copy settings as subassets, not just the profile's list of references.
            // Adjust only the highest-priority active global grading volume so exposure
            // does not accumulate accidentally across multiple blended volumes.
            var volume = Obj.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(v => v.enabled && v.isGlobal && v.weight > 0 && v.sharedProfile != null)
                .OrderByDescending(v => v.priority).FirstOrDefault();
            if (volume == null) throw new InvalidOperationException("Active global postprocessing volume required for the documented +0.7EV test.");
            var source = volume.sharedProfile; string path = assets + "/Profiles/CandidateOnlyGrading.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(profile, path); }
            else foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o != profile)) Obj.DestroyImmediate(sub, true);
            profile.name = "B03 candidate +0.7EV";
            profile.settings = new List<PostProcessEffectSettings>();
            foreach (var setting in source.settings)
            {
                var copy = Obj.Instantiate(setting); copy.name = setting.name; copy.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                AssetDatabase.AddObjectToAsset(copy, profile); profile.settings.Add(copy);
            }
            if (!profile.TryGetSettings<ColorGrading>(out var grade))
            {
                grade = ScriptableObject.CreateInstance<ColorGrading>(); grade.name = "ColorGrading";
                AssetDatabase.AddObjectToAsset(grade, profile); profile.settings.Add(grade);
            }
            float prior = grade.postExposure.value;
            grade.enabled.Override(true); grade.gradingMode.Override(GradingMode.HighDefinitionRange); grade.postExposure.Override(prior + .7f);
            EditorUtility.SetDirty(grade); EditorUtility.SetDirty(profile); volume.sharedProfile = profile;
            var fill = new GameObject("B03 PHOTO TEST ONLY soft camera fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .5f; fill.color = new Color(.88f, .93f, 1);
            fill.shadows = LightShadows.Soft; fill.shadowStrength = .35f; fill.shadowBias = .05f;
            fill.transform.rotation = Quaternion.LookRotation((new Vector3(86.5f, 6.5f, 44) - camera.transform.position).normalized, Vector3.up);
            report.AppendLine("Adjusted cloned profile=" + path + "; original=" + AssetDatabase.GetAssetPath(source) + "; volume=" + volume.name + "; weight=" + volume.weight.ToString("R", Inv));
            report.AppendLine("ColorGrading postExposure=" + prior.ToString("R", Inv) + " -> " + (prior + .7f).ToString("R", Inv) + " EV; existing tonemapper/contrast/saturation retained.");
            report.AppendLine("Candidate scene added directional fill: intensity=0.5, RGB=(0.88,0.93,1), soft shadows, shadowStrength=0.35, shadowBias=0.05, rotation=" + RepVec(fill.transform.eulerAngles));
            report.AppendLine("Fill affects the candidate SCENE through its default culling mask, not only the building. Production scene and original lights unchanged; ambient unchanged=" + RenderSettings.ambientMode + ", intensity=" + RenderSettings.ambientIntensity.ToString("R", Inv));
        }
    }
}
