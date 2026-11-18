using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string B02SurfaceShaderName = "Anseong/B02PanelSurface";
        const string B02SurfaceFolder = "Assets/Map35/B02Architecture/Materials";
        const string B02SurfaceMeshFolder = "Assets/Map35/B02Architecture/MaterialMeshes";
        static readonly Dictionary<string, Material> b02SurfaceCache = new Dictionary<string, Material>();

        // Small shared material palette. Geometry/panel seeds, rather than a material for
        // every sheet, make the distribution of oxidation independent of repeated photos.
        public static Material B02Material(string role, int variant = 0)
        {
            role = (role ?? "").ToLowerInvariant();
            if (!new[] { "wall", "ribbedwall", "roof", "steel", "flashing", "liner", "concrete", "glass", "galvanized", "rust" }.Contains(role))
                throw new ArgumentException("Unknown B02 material role: " + role);
            variant = ((variant % 3) + 3) % 3;
            string key = role + "_" + variant;
            if (b02SurfaceCache.TryGetValue(key, out var cached) && cached != null) return cached;
            B02EnsureAssetFolder(B02SurfaceFolder);
            string path = B02SurfaceFolder + "/B02_" + key + ".mat";
            Shader shader = Shader.Find(role == "glass" ? "Standard" : B02SurfaceShaderName);
            if (shader == null) throw new InvalidOperationException("Missing B02 shader: " + role);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            material.SetOverrideTag("B02SurfaceRole", role);
            material.SetOverrideTag("B02SurfaceVersion", "1");
            material.enableInstancing = true;
            if (role == "glass")
            {
                // Thin, clear inspection glazing; no emission or global lighting change.
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Mode", 3);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetColor("_Color", new Color(.52f, .60f, .62f, .18f));
                material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", .76f);
                material.renderQueue = (int)RenderQueue.Transparent;
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                bool corrugatedPhoto = role == "wall" || role == "roof";
                bool sheet = corrugatedPhoto || role == "ribbedwall";
                bool bare = role == "galvanized";
                bool concrete = role == "concrete", rust = role == "rust";
                string baseFolder, basePrefix, diffuseSuffix, normalSuffix, roughSuffix, aoSuffix;
                if (corrugatedPhoto)
                {
                    baseFolder = "Assets/ThirdParty/FreePBR/PhotorealFacades/corrugated_iron_02/";
                    basePrefix = "corrugated_iron_02"; diffuseSuffix = "_diff_2k.jpg";
                    normalSuffix = "_nor_gl_2k.png"; roughSuffix = "_rough_2k.png"; aoSuffix = "_ao_2k.png";
                }
                else if (concrete)
                {
                    baseFolder = "Assets/ThirdParty/FreePBR/PhotorealFacades/concrete_wall_007/";
                    basePrefix = "concrete_wall_007"; diffuseSuffix = "_diff_2k.jpg";
                    normalSuffix = "_nor_gl_2k.png"; roughSuffix = "_rough_2k.png"; aoSuffix = "_ao_2k.png";
                }
                else
                {
                    baseFolder = rust ? "Assets/ThirdParty/SurfaceLibrary/rust_coarse_01/"
                        : bare ? "Assets/ThirdParty/QuietSurfaces/Metal049A/" : "Assets/ThirdParty/SurfaceLibrary/Metal027/";
                    basePrefix = (rust ? "rust_coarse_01" : bare ? "Metal049A" : "Metal027") + "_2K-PNG";
                    diffuseSuffix = "_Color.png"; normalSuffix = "_NormalGL.png";
                    roughSuffix = "_Roughness.png"; aoSuffix = "_AmbientOcclusion.png";
                }
                var diffuse = B02SurfaceTexture(baseFolder + basePrefix + diffuseSuffix, true, false);
                material.SetTexture("_MainTex", diffuse);
                material.SetTexture("_BumpMap", B02SurfaceTexture(baseFolder + basePrefix + normalSuffix, false, true));
                material.SetTexture("_RoughnessMap", B02SurfaceTexture(baseFolder + basePrefix + roughSuffix, false, false));
                string aoPath = baseFolder + basePrefix + aoSuffix;
                material.SetTexture("_AOMap", File.Exists(aoPath) ? B02SurfaceTexture(aoPath, false, false) : Texture2D.whiteTexture);
                string metalPath = "Assets/ThirdParty/QuietSurfaces/Metal049A/Metal049A_2K-PNG";
                string rustPath = "Assets/ThirdParty/SurfaceLibrary/rust_coarse_01/rust_coarse_01_2K-PNG";
                material.SetTexture("_MetalAlbedo", B02SurfaceTexture(metalPath + "_Color.png", true, false));
                material.SetTexture("_MetalNormal", B02SurfaceTexture(metalPath + "_NormalGL.png", false, true));
                material.SetTexture("_MetalRoughness", B02SurfaceTexture(metalPath + "_Roughness.png", false, false));
                material.SetTexture("_RustAlbedo", B02SurfaceTexture(rustPath + "_Color.png", true, false));
                material.SetTexture("_RustNormal", B02SurfaceTexture(rustPath + "_NormalGL.png", false, true));
                material.SetTexture("_RustRoughness", B02SurfaceTexture(rustPath + "_Roughness.png", false, false));
                Vector3 reflectance = role == "wall" || role == "ribbedwall" ? new Vector3(.34f, .38f, .39f)
                    : role == "roof" ? new Vector3(.27f, .30f, .31f)
                    : role == "liner" ? new Vector3(.37f, .37f, .34f)
                    : role == "flashing" ? new Vector3(.27f, .30f, .31f)
                    : concrete ? new Vector3(.35f, .34f, .31f) : new Vector3(.10f, .12f, .13f);
                float tone = variant == 1 ? .94f : variant == 2 ? 1.06f : 1;
                reflectance *= tone;
                // Vector stores already-linear reflectance: never multiply two dark
                // sRGB material colours or perform a second Color.linear conversion.
                material.SetVector("_Reflectance", new Vector4(reflectance.x, reflectance.y, reflectance.z, 1));
                material.SetFloat("_PhotoMean", FacadeDiffuseMean(diffuse));
                material.SetFloat("_NormalizePhoto", concrete ? .25f : bare || rust ? 0 : 1);
                material.SetFloat("_PhotoContrast", sheet ? .72f : .6f);
                material.SetFloat("_TileMetres", corrugatedPhoto ? 2.7f : concrete ? 2.16f : 1.8f);
                material.SetFloat("_NormalStrength", corrugatedPhoto ? .67f : concrete ? .65f : .35f);
                material.SetFloat("_RoughnessScale", sheet ? .62f : 1);
                material.SetFloat("_RoughnessBias", sheet ? .22f : 0);
                material.SetFloat("_BaseMetallic", bare ? .92f : 0);
                material.SetFloat("_OcclusionStrength", concrete ? .45f : .28f);
                material.SetFloat("_MetalTileMetres", 1.6f); material.SetFloat("_RustTileMetres", 1.3f);
                material.SetFloat("_RustGain", 1.12f);
                material.SetFloat("_WeatherStrength", 1);
                material.SetFloat("_WearAmount", concrete || rust ? 0 : sheet ? .76f : bare ? .24f : .44f);
                material.SetFloat("_Panelize", sheet ? 1 : 0);
                material.SetFloat("_PanelWidth", role == "roof" ? 1.2f : 1.15f);
                // Do not invent horizontal seams in an actual uninterrupted 8m sheet.
                material.SetFloat("_PanelHeight", 100);
                material.SetFloat("_ReplacementRate", sheet ? .16f : .08f);
                material.SetFloat("_PanelSeed", 19 + variant * 17 + (role == "roof" ? 101 : 0));
                material.SetFloat("_PanelVariation", sheet ? .20f : .08f);
                material.SetFloat("_EdgeWidth", sheet ? .075f : .035f);
                material.SetFloat("_FastenerWeather", sheet ? 1 : 0);
                material.SetFloat("_RunoffStrength", sheet ? .32f : .12f);
                material.mainTextureScale = Vector2.one; material.mainTextureOffset = Vector2.zero;
                material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = -1;
            }
            EditorUtility.SetDirty(material); b02SurfaceCache[key] = material;
            return material;
        }

        static void B02EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            B02EnsureAssetFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        static Texture2D B02SurfaceTexture(string sourcePath, bool srgb, bool normal)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (texture == null) throw new InvalidOperationException("B02 photographic channel missing: " + sourcePath);
            var sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (sourceImporter == null) throw new InvalidOperationException("B02 texture importer missing: " + sourcePath);
            bool correct = sourceImporter.sRGBTexture == srgb
                && sourceImporter.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)
                && sourceImporter.mipmapEnabled && sourceImporter.wrapMode == TextureWrapMode.Repeat;
            if (correct) return texture;
            // Correct a derived copy only; other materials may depend on the old importer.
            string folder = "Assets/Map35/B02Architecture/Textures"; B02EnsureAssetFolder(folder);
            string target = folder + "/" + Path.GetFileName(sourcePath);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(target) == null && !AssetDatabase.CopyAsset(sourcePath, target))
                throw new InvalidOperationException("Cannot copy B02 photographic channel: " + sourcePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.convertToNormalmap = false;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8; importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        static uint B02StableHash(string value)
        {
            unchecked { uint hash = 2166136261; foreach (char c in value) { hash ^= c; hash *= 16777619; } return hash; }
        }

        static string B02ChooseRole(MeshRenderer renderer, Material material)
        {
            string n = renderer.name.ToLowerInvariant();
            string assigned = material.GetTag("B02SurfaceRole", false, "");
            if (!string.IsNullOrEmpty(assigned)) return assigned;
            if (n.StartsWith("b02 glazing ")) return "glass";
            // These sheets have real .18m-pitch corrugation. Do not superimpose the
            // unrelated corrugation frequency baked into the flat-sheet photograph.
            if (n.StartsWith("b02 facade panel ")) return "ribbedwall";
            if (n.StartsWith("b02 inner liner ")) return "liner";
            if (n.StartsWith("b02 corner flashing ") || n.StartsWith("b02 roof edge ")) return "flashing";
            if (n.StartsWith("b02 window frame ") || n.StartsWith("b02 reveal ") || n.StartsWith("b02 roof support ")) return "steel";
            if (n.StartsWith("b02 panel fixing ")) return "galvanized";
            // Floor, warning markings, glass placeholders and shared effects are protected.
            if (n.Contains("floor") || n.Contains("foundation") || n.Contains("footing") || n.Contains("pavement")
                || n.Contains("stencil") || n.Contains("warning") || n.Contains("runoff")) return null;
            string original = material.GetTag("FacadeSource", false, "");
            string sourceName = string.IsNullOrEmpty(original) ? material.name : Path.GetFileNameWithoutExtension(original);
            if (sourceName.Contains("glass") || sourceName.Contains("warning") || sourceName.Contains("ground")) return null;
            if (n.StartsWith("roofpanel_")) return "roof";
            if (n.StartsWith("sidewall_") || n.StartsWith("endwall_")) return "wall";
            if (!sourceName.StartsWith("Wide_Palette_B02_", StringComparison.Ordinal)) return null;
            if (sourceName.EndsWith("_concrete", StringComparison.Ordinal)) return FacadeAboveGroundConcrete(renderer) ? "concrete" : null;
            if (sourceName.EndsWith("_rust", StringComparison.Ordinal)) return "rust";
            if (sourceName.EndsWith("_galvanized", StringComparison.Ordinal) || sourceName.EndsWith("_metal", StringComparison.Ordinal)) return "galvanized";
            if (sourceName.Contains("_paint_") || sourceName.EndsWith("_roof", StringComparison.Ordinal)) return "steel";
            return null;
        }

        // Only the loaded scene is changed; the caller owns scene save, bake, LOD rebuild,
        // capture and production promotion. No source prefab, material or mesh is mutated.
        public static int ApplyB02PanelMaterials()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) throw new InvalidOperationException("B02 material calibration requires Linear colour space.");
            b02SurfaceCache.Clear(); B02EnsureAssetFolder(B02SurfaceMeshFolder);
            var root = Existing("B02"); if (root == null) throw new InvalidOperationException("B02 building is missing.");
            int changed = 0, sheets = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || FacadeIsFarLod(renderer.transform)) continue;
                if (FacadeProtectedRenderer(renderer)) continue;
                var slots = renderer.sharedMaterials; bool any = false;
                // Many old fasteners have identical sibling names. Include their stable
                // placement/scale so those copies cannot overwrite one another's mesh.
                string identity = PurposeGroundPath(renderer.transform) + "|" + renderer.transform.position.ToString("R")
                    + "|" + renderer.transform.rotation.ToString("R") + "|" + renderer.transform.lossyScale.ToString("R");
                uint seed = B02StableHash(identity);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    string role = B02ChooseRole(renderer, slots[i]); if (role == null) continue;
                    slots[i] = B02Material(role, (int)(seed % 3)); any = true; changed++;
                    if (role == "wall" || role == "ribbedwall" || role == "roof") sheets++;
                }
                if (!any) continue;
                renderer.sharedMaterials = slots;
                if (slots.Any(m => m != null && m.shader != null && m.shader.name == B02SurfaceShaderName))
                    B02PreparePanelMesh(renderer, seed);
                EditorUtility.SetDirty(renderer);
            }
            if (sheets == 0) throw new InvalidOperationException("No B02 wall or roof received photographic panel materials.");
            AssetDatabase.SaveAssets();
            Debug.Log("B02_PANEL_MATERIALS_APPLIED slots=" + changed + " wall_roof_slots=" + sheets
                + " source_assets_preserved=true far_LOD_requires_caller_rebuild=true");
            return changed;
        }

        static void B02PreparePanelMesh(MeshRenderer renderer, uint seed)
        {
            var filter = renderer.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) return;
            var source = filter.sharedMesh;
            var panel = new List<Vector4>(); source.GetUVs(2, panel);
            bool hasPanel = panel.Count == source.vertexCount && panel.Any(p => p.z > .005f && p.w > .005f);
            var oldColors = source.colors;
            bool hasSeed = oldColors.Length == source.vertexCount && oldColors.Any(c => c.a > .01f && c.r + c.g < 1.998f);
            bool hasTangents = source.tangents.Length == source.vertexCount;
            if (hasPanel && hasSeed && hasTangents) return;
            var mesh = UnityEngine.Object.Instantiate(source); mesh.name = source.name + " B02 panel metadata";
            if (!hasPanel)
            {
                // Bounding-face fallback is for old simple building boxes. It preserves
                // all positions/triangles/colliders; new architecture supplies authored UVs.
                var vertices = mesh.vertices; var normals = mesh.normals; var metres = new Vector2[vertices.Length];
                var bounds = mesh.bounds; var extent = bounds.size;
                Vector3 scale = renderer.transform.lossyScale;
                Vector3 size = Vector3.Scale(extent, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                panel.Clear();
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 n = normals.Length == vertices.Length ? normals[i] : Vector3.forward;
                    int axis = Mathf.Abs(n.x) > Mathf.Abs(n.y) ? 0 : 1; if (Mathf.Abs(n.z) > Mathf.Abs(n[axis])) axis = 2;
                    Vector3 p = vertices[i] - bounds.min;
                    Vector3 f = new Vector3(p.x / Mathf.Max(.001f, extent.x), p.y / Mathf.Max(.001f, extent.y), p.z / Mathf.Max(.001f, extent.z));
                    Vector4 data = axis == 0 ? new Vector4(f.z, f.y, size.z, size.y)
                        : axis == 1 ? new Vector4(f.x, f.z, size.x, size.z) : new Vector4(f.x, f.y, size.x, size.y);
                    panel.Add(data); metres[i] = new Vector2(data.x * data.z, data.y * data.w);
                }
                mesh.SetUVs(2, panel); mesh.uv = metres;
            }
            if (!hasSeed)
            {
                float r = .02f + ((seed & 65535) / 65535f) * .96f;
                float g = .02f + (((seed >> 16) & 65535) / 65535f) * .96f;
                float exposure = renderer.bounds.center.y < 7 ? .88f : .65f;
                var colour = new Color(r, g, exposure, 1);
                mesh.colors = Enumerable.Repeat(colour, mesh.vertexCount).ToArray();
            }
            if (!hasTangents || !hasPanel) mesh.RecalculateTangents();
            string path = B02SurfaceMeshFolder + "/panel_" + seed.ToString("x8") + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            filter.sharedMesh = saved; EditorUtility.SetDirty(filter);
        }

        // Comparison helper: callers capture each condition from a fresh process/scene.
        // This changes only new derived materials, never the photographic source assets.
        public static void B02SetWeatherStrength(float strength)
        {
            var root = Existing("B02"); if (root == null) throw new InvalidOperationException("B02 building is missing.");
            foreach (var material in root.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
                if (material != null && material.shader != null && material.shader.name == B02SurfaceShaderName)
                { material.SetFloat("_WeatherStrength", Mathf.Clamp01(strength)); EditorUtility.SetDirty(material); }
        }
    }
}
