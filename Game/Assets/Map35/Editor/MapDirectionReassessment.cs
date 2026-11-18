using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static string ReassessmentPhase { get { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-directionPhase"); return i >= 0 && i + 1 < args.Length ? args[i + 1] : ""; } }
        static string ReassessmentOutput => Path.GetFullPath("../docs/validation/map-direction-reassessment-2026-11-15" + (ReassessmentPhase.Length > 0 ? "/isolated" : ""));
        static readonly int[] ReassessmentViews = { 9, 12, 17, 37, 38, 49, 50, 52, 54 };
        // One condition per fresh Unity process. The original multi-condition run
        // failed baseline repeatability and must not be used for causal comparisons.
        public static void AuditMapDirection()
        {
            if (!new[] { "baseline", "normal-off", "concrete-macro-off", "exposure-plus1EV", "baseline-repeat" }.Contains(ReassessmentPhase))
                throw new ArgumentException("Use a fresh Unity process and -directionPhase baseline|normal-off|concrete-macro-off|exposure-plus1EV|baseline-repeat. In-process comparisons are rejected.");
            EditorSceneManager.OpenScene(WideScene); ReconnectWideScene(); Directory.CreateDirectory(ReassessmentOutput);
            string sceneHash = AuditHash(WideScene), buildHash = AuditHash("../Builds/MapWide/AnseongMapWide.exe");
            var renderers = Obj.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var originals = renderers.ToDictionary(r => r, r => r.sharedMaterials);
            var sourceMaterials = originals.Values.SelectMany(a => a).Where(m => m != null).Distinct().ToArray();
            var protectedFiles = sourceMaterials.Select(AssetDatabase.GetAssetPath).Where(File.Exists).Distinct().ToDictionary(p => p, AuditHash);
            var volumes = Obj.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var profiles = volumes.ToDictionary(v => v, v => v.sharedProfile);
            foreach (var profile in profiles.Values.Where(p => p != null).Distinct())
            { var path = AssetDatabase.GetAssetPath(profile); if (File.Exists(path)) protectedFiles[path] = AuditHash(path); }
            var clones = new Dictionary<Material, Material>(); var tempObjects = new List<Obj>();
            var hidden = new List<(GameObject, bool)>(); var enabled = viewer.views.Select(c => c.enabled).ToArray();
            try
            {
                WarmSceneParticles();
                foreach (var ps in Obj.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) ps.Pause(true);
                foreach (var go in new[] { viewer.scenario.gameObject, proxies.gameObject, viewer.labels, viewer.routes, viewer.frusta, viewer.reservations }.Distinct())
                { hidden.Add((go, go.activeSelf)); go.SetActive(false); }
                foreach (var original in sourceMaterials)
                { var copy = new Material(original) { hideFlags = HideFlags.HideAndDontSave }; clones.Add(original, copy); tempObjects.Add(copy); }
                foreach (var pair in originals) pair.Key.sharedMaterials = pair.Value.Select(m => m == null ? null : clones[m]).ToArray();
                var propertyRows = new StringBuilder("phase,material,property,original_value,diagnostic_value\n");
                CaptureReassessment("baseline");

                string[] normalTextures = { "_BumpMap", "_DetailNormalMap", "_RustNormal", "_BaseNormal", "_DamageNormal", "_MacroNormal", "_RockNormal", "_GravelNormal" };
                var flatNormal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
                flatNormal.SetPixels(Enumerable.Repeat(new Color(.5f, .5f, 1, 1), 4).ToArray()); flatNormal.Apply(); tempObjects.Add(flatNormal);
                if (ReassessmentPhase.Length == 0 || ReassessmentPhase == "normal-off") {
                foreach (var pair in clones)
                {
                    foreach (string property in normalTextures) if (pair.Value.HasProperty(property))
                    { pair.Value.SetTexture(property, flatNormal); propertyRows.AppendLine("normal-off," + FacadeCsv(pair.Key.name) + "," + property + ",source,flat tangent normal"); }
                    foreach (string property in new[] { "_NormalStrength", "_RustNormalStrength", "_BumpScale", "_DetailNormalMapScale" }) if (pair.Value.HasProperty(property))
                    { propertyRows.AppendLine("normal-off," + FacadeCsv(pair.Key.name) + "," + property + "," + pair.Value.GetFloat(property).ToString("R", Inv) + ",0"); pair.Value.SetFloat(property, 0); }
                }
                CaptureReassessment("normal-off");
                foreach (var pair in clones) pair.Value.CopyPropertiesFromMaterial(pair.Key);
                }

                if (ReassessmentPhase.Length == 0 || ReassessmentPhase == "concrete-macro-off") {
                foreach (var pair in clones.Where(p => p.Key.shader.name == "Anseong/PhotographicGround"))
                { propertyRows.AppendLine("concrete-macro-off," + FacadeCsv(pair.Key.name) + ",_MacroStrength," + pair.Value.GetFloat("_MacroStrength").ToString("R", Inv) + ",0"); pair.Value.SetFloat("_MacroStrength", 0); }
                CaptureReassessment("concrete-macro-off");
                foreach (var pair in clones) pair.Value.CopyPropertiesFromMaterial(pair.Key);
                }

                if (ReassessmentPhase.Length == 0 || ReassessmentPhase == "exposure-plus1EV") {
                foreach (var pair in profiles)
                {
                    if (pair.Value == null) continue;
                    var profile = Obj.Instantiate(pair.Value); profile.hideFlags = HideFlags.HideAndDontSave;
                    profile.settings = pair.Value.settings.Select(setting => { var copy = Obj.Instantiate(setting); copy.hideFlags = HideFlags.HideAndDontSave; tempObjects.Add(copy); return copy; }).ToList();
                    tempObjects.Add(profile); pair.Key.sharedProfile = profile;
                    if (profile.TryGetSettings<ColorGrading>(out var grade))
                    { float prior = grade.postExposure.value; grade.postExposure.Override(prior + 1); propertyRows.AppendLine("exposure-plus1EV," + FacadeCsv(pair.Value.name) + ",postExposure," + prior.ToString("R", Inv) + "," + (prior + 1).ToString("R", Inv)); }
                }
                CaptureReassessment("exposure-plus1EV");
                foreach (var pair in profiles) pair.Key.sharedProfile = pair.Value;
                }
                CaptureReassessment("baseline-repeat");
                File.WriteAllText(ReassessmentOutput + "/changed-controls" + (ReassessmentPhase.Length > 0 ? "-" + ReassessmentPhase : "") + ".csv", propertyRows.ToString());
                WriteReassessmentProjection();
            }
            finally
            {
                foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
                foreach (var pair in profiles) if (pair.Key != null) pair.Key.sharedProfile = pair.Value;
                for (int i = 0; i < viewer.views.Length; i++) viewer.views[i].enabled = enabled[i];
                foreach (var pair in hidden) if (pair.Item1 != null) pair.Item1.SetActive(pair.Item2);
                foreach (var item in tempObjects.AsEnumerable().Reverse()) if (item != null) Obj.DestroyImmediate(item);
                EditorSceneManager.OpenScene(WideScene); ReconnectWideScene();
            }
            bool unchanged = sceneHash == AuditHash(WideScene) && buildHash == AuditHash("../Builds/MapWide/AnseongMapWide.exe") && protectedFiles.All(p => p.Value == AuditHash(p.Key));
            if (!unchanged) throw new InvalidOperationException("Diagnostic unexpectedly changed a protected production file.");
            File.WriteAllText(ReassessmentOutput + "/integrity" + (ReassessmentPhase.Length > 0 ? "-" + ReassessmentPhase : "") + ".txt", "Scene SHA256=" + sceneHash + "\nBuild SHA256=" + buildHash + "\nProtected material/profile files=" + protectedFiles.Count
                + "\nScene/build/material/profile hashes unchanged=True\nPhase=" + ReassessmentPhase + "\nCaptures=" + (ReassessmentPhase.Length > 0 ? 9 : 45) + "\nTemporary material/profile clones only. No scene save, light bake, build, art production or HMD validation.\nNormal-off includes facade/ground/other material normal inputs; concrete-macro-off only PhotographicGround concrete brightness modulation (asphalt photograph retained). Exposure+1EV is a diagnostic, not an adopted look.\nBaseline repeat bounds render variation; image differences are not realism scores.\n");
            Debug.Log("MAP_DIRECTION_REASSESSMENT_OK phase=" + ReassessmentPhase + " unchanged=True");
        }
        static void CaptureReassessment(string phase)
        {
            if (ReassessmentPhase.Length > 0 && ReassessmentPhase != phase) return;
            var rows = new StringBuilder("index,name,file,x,y,z,forward_x,forward_y,forward_z,field_of_view\n");
            foreach (int index in ReassessmentViews)
            {
                var camera = viewer.views[index]; float aspect = camera.aspect;
                try { SaveWearCamera(camera, ReassessmentOutput + "/" + phase + "-" + index.ToString("00") + ".png"); }
                finally { camera.aspect = aspect; }
                var p = camera.transform.position; var f = camera.transform.forward;
                rows.AppendLine(index + "," + FacadeCsv(camera.name) + "," + phase + "-" + index.ToString("00") + ".png," + p.x.ToString("R", Inv) + "," + p.y.ToString("R", Inv) + "," + p.z.ToString("R", Inv) + "," + f.x.ToString("R", Inv) + "," + f.y.ToString("R", Inv) + "," + f.z.ToString("R", Inv) + "," + camera.fieldOfView.ToString("R", Inv));
            }
            File.WriteAllText(ReassessmentOutput + "/" + phase + "-cameras.csv", rows.ToString());
        }
        static void WriteReassessmentProjection()
        {
            var samples = new[] { ("B02_wall", new Vector3(-120, 9, 40.5f)), ("B02_yard", new Vector3(-78, .1f, 45)), ("B03_wall", new Vector3(83, 6, 35.25f)), ("Z1_yard", new Vector3(0, .1f, 70)) };
            var rows = new StringBuilder("camera,sample,view_depth_m,in_frame,feature_metres,screen_perpendicular_pixels\n");
            foreach (int index in ReassessmentViews) foreach (var sample in samples)
            {
                var camera = viewer.views[index]; float oldAspect = camera.aspect; camera.aspect = 1.6f; var p = camera.WorldToViewportPoint(sample.Item2); camera.aspect = oldAspect;
                if (p.z <= 0) continue;
                float focal = 1000f / (2 * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
                foreach (float size in new[] { .005f, .02f, .065f, .1f, 1f, 3f, 10f })
                    rows.AppendLine(index + "," + sample.Item1 + "," + p.z.ToString("R", Inv) + "," + (p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1) + "," + size.ToString("R", Inv) + "," + (focal * size / p.z).ToString("R", Inv));
            }
            File.WriteAllText(ReassessmentOutput + "/feature-projection.csv", rows.ToString());
        }
    }
}
