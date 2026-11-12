using System.IO;
using Anseong.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 전투 이펙트를 Unity 파티클로 만든다(선우 11/11 "이펙트는 AI로 만들지 않는다, Unity 파티클"). 텍스처(빛·고리·연기)도 코드로 그린다.
    /// 메뉴 'Anseong → Art → Build Combat VFX (파티클)': Art/VFX/Prefabs/VFX_*.prefab을 만들고 효과 표(Art/VFX/CombatEffects.asset)의
    /// 이름이 같은 줄 중 프리팹 칸이 비었거나 이 메뉴가 만든 프리팹이 든 줄에 넣는다(사람이 넣은 다른 프리팹은 건드리지 않음).
    /// 프리팹은 반지름 1 m쯤으로 만들고 효과 표의 크기 배수로 키운다(로봇 65 m 기준). 'Build Full Game'이 마지막에 부른다.
    /// </summary>
    public static class VfxBuilder
    {
        const string Root = "Assets/_Anseong/Art/VFX";
        const string EffectSetPath = Root + "/CombatEffects.asset";
        const string TextureFolder = Root + "/Textures";
        const string MaterialFolder = Root + "/Materials";
        const string PrefabFolder = Root + "/Prefabs";

        static Material glowAdd, ringAdd, smokeBlend, sparkAdd;

        [MenuItem("Anseong/Art/Build Combat VFX (파티클)")]
        static void BuildFromMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var set = AssetDatabase.LoadAssetAtPath<CombatEffectSet>(EffectSetPath);
            if (set == null) return "[VfxBuilder] 효과 표가 없습니다. 'Anseong → Setup → Build M1 Main'을 먼저 실행하세요.";
            EnsureFolder(Root, "Textures");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");

            var glow = Tex("VFX_Glow", (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 2.2f));
            var ring = Tex("VFX_Ring", (u, v) =>
            {
                var r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.14f) * Mathf.Clamp01((1f - r) * 12f);
            });
            var smoke = Tex("VFX_Smoke", (u, v) =>
            {
                var r = Mathf.Sqrt(u * u + v * v);
                var n = Mathf.PerlinNoise(u * 2.7f + 5.3f, v * 2.7f + 1.7f) * 0.6f + Mathf.PerlinNoise(u * 6.1f + 9.1f, v * 6.1f + 3.3f) * 0.4f;
                return Mathf.Clamp01(1f - r * 1.05f) * Mathf.Lerp(0.45f, 1.2f, n);
            });
            var spark = Tex("VFX_Spark", (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 3.2f), 1.5f) * Mathf.Clamp01(1f - Mathf.Abs(u)));

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return "[VfxBuilder] URP 파티클 셰이더를 찾지 못했습니다.";
            glowAdd = Mat("VFX_Glow_Add", shader, glow, true);
            ringAdd = Mat("VFX_Ring_Add", shader, ring, true);
            sparkAdd = Mat("VFX_Spark_Add", shader, spark, true);
            smokeBlend = Mat("VFX_Smoke_Blend", shader, smoke, false);

            var orange = new Color(1f, 0.62f, 0.22f);
            var yellow = new Color(1f, 0.9f, 0.55f);
            var blue = new Color(0.45f, 0.75f, 1f);
            var green = new Color(0.25f, 1f, 0.6f);
            var purple = new Color(0.8f, 0.3f, 1f);
            var steel = new Color(0.32f, 0.3f, 0.3f, 0.7f);
            var dust = new Color(0.55f, 0.5f, 0.44f, 0.55f);

            var made = 0;
            made += Save("VFX_PunchSpark", 1.2f, root =>
            {
                Flash(root, yellow, 2.2f, 0.12f, 0f);
                Sparks(root, 26, yellow, orange, 3f, 8f, 0.25f, 0.6f, 0.1f, 0.6f, 0f);
                Smoke(root, 4, steel, 0.6f, 1.6f, 0.8f, 1.1f, 0.6f, 0f);
            });
            made += Save("VFX_HookImpact", 1.6f, root =>
            {
                Flash(root, yellow, 3.4f, 0.16f, 0f);
                Ring(root, orange, 3.2f, 0.35f, 0f);
                Sparks(root, 48, yellow, orange, 4f, 11f, 0.3f, 0.8f, 0.12f, 0.8f, 0f);
                Fire(root, 8, orange, 0.9f, 2f, 0.35f, 0.6f, 0.5f, 0f);
                Smoke(root, 7, steel, 0.9f, 2.4f, 1f, 1.5f, 0.9f, 0f);
            });
            made += Save("VFX_BoostShock", 1.4f, root =>
            {
                Flash(root, Color.white, 3f, 0.14f, 0f);
                Ring(root, blue, 3.4f, 0.45f, 0f);
                Ring(root, Color.white, 2.2f, 0.3f, 0.06f);
                Sparks(root, 40, Color.white, blue, 5f, 12f, 0.3f, 0.7f, 0.1f, 0.2f, 0f);
            });
            made += Save("VFX_GuardSpark", 1.0f, root =>
            {
                Flash(root, blue, 2.4f, 0.12f, 0f);
                Ring(root, blue, 2.4f, 0.3f, 0f);
                Sparks(root, 30, Color.white, blue, 3f, 9f, 0.2f, 0.55f, 0.1f, 0.8f, 0f);
            });
            made += Save("VFX_WeaponBreak", 3.2f, root =>
            {
                Flash(root, yellow, 3.6f, 0.2f, 0f);
                Ring(root, orange, 3.6f, 0.5f, 0f);
                Fire(root, 18, orange, 1.2f, 2.4f, 0.5f, 1f, 0.9f, 0f);
                Fire(root, 10, yellow, 0.8f, 1.6f, 0.3f, 0.6f, 0.7f, 0.25f);
                Sparks(root, 70, yellow, orange, 3f, 10f, 0.6f, 1.4f, 0.1f, 1f, 0f);
                Smoke(root, 14, steel, 1.2f, 3.2f, 2f, 3f, 1f, 0.1f);
            });
            made += Save("VFX_DroneExplode", 2.2f, root =>
            {
                Flash(root, yellow, 3f, 0.15f, 0f);
                Fire(root, 12, orange, 1f, 2.2f, 0.4f, 0.8f, 0.6f, 0f);
                Sparks(root, 40, yellow, orange, 3f, 9f, 0.4f, 1f, 0.1f, 1f, 0f);
                Smoke(root, 8, steel, 1f, 2.6f, 1.4f, 2f, 0.8f, 0.05f);
            });
            made += Save("VFX_MuzzleFlash", 0.6f, root =>
            {
                Flash(root, yellow, 2.6f, 0.08f, 0f);
                Flash(root, Color.white, 1.4f, 0.06f, 0f);
                Sparks(root, 12, yellow, orange, 4f, 9f, 0.1f, 0.25f, 0.08f, 0f, 0f);
                Smoke(root, 3, steel, 0.5f, 1.2f, 0.4f, 0.55f, 0.5f, 0f);
            });
            made += Save("VFX_ScanPing", 1.2f, root =>
            {
                Ring(root, green, 3f, 0.6f, 0f);
                Ring(root, green, 3f, 0.6f, 0.25f);
                Flash(root, green, 1.2f, 0.3f, 0f);
            });
            made += Save("VFX_HackSparks", 1.6f, root =>
            {
                for (var i = 0; i < 4; i++)
                {
                    Flash(root, purple, 1.6f, 0.08f, i * 0.3f);
                    Sparks(root, 12, Color.white, purple, 2f, 6f, 0.15f, 0.4f, 0.08f, 0.5f, i * 0.3f);
                }
            });
            made += Save("VFX_MapDust", 4.5f, root =>
            {
                Smoke(root, 22, dust, 0.9f, 2.4f, 2.5f, 4.2f, 1.2f, 0f);
                Smoke(root, 10, dust, 0.6f, 1.8f, 2f, 3.5f, 0.8f, 0.4f);
                Sparks(root, 16, new Color(0.7f, 0.65f, 0.6f), dust, 1f, 4f, 0.5f, 1.2f, 0.08f, 1.5f, 0f);
            });
            made += Save("VFX_BossExplode", 5f, root =>
            {
                Flash(root, Color.white, 4.5f, 0.3f, 0f);
                Ring(root, orange, 4.5f, 0.8f, 0f);
                Ring(root, yellow, 3.5f, 0.6f, 0.9f);
                for (var i = 0; i < 4; i++)
                {
                    var delay = i * 0.45f;
                    Fire(root, 14, i % 2 == 0 ? orange : yellow, 1.1f, 2.6f, 0.5f, 1.1f, 1.1f, delay);
                    Sparks(root, 45, yellow, orange, 3f, 11f, 0.6f, 1.6f, 0.1f, 1f, delay);
                }
                Smoke(root, 26, steel, 1.4f, 3.6f, 2.5f, 4.2f, 1.3f, 0.2f);
            });

            // 효과 표에 넣기.
            var filled = 0;
            foreach (var entry in set.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.effectName)) continue;
                var path = $"{PrefabFolder}/{entry.effectName}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var current = entry.prefab != null ? AssetDatabase.GetAssetPath(entry.prefab) : null;
                if (current != null && !current.StartsWith(PrefabFolder)) continue; // 사람이 넣은 프리팹
                entry.prefab = prefab;
                entry.seconds = Mathf.Max(entry.seconds, Lifetime(entry.effectName));
                if (sizes.TryGetValue(entry.effectName, out var size)) entry.scale = size;
                filled++;
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return $"[VfxBuilder] 파티클 프리팹 {made}개 만들고 효과 표 {filled}줄에 넣음({PrefabFolder}).";
        }

        /// <summary>효과 크기(m, 프리팹 반지름 1에 곱함). 65 m 로봇 주먹이 5 m쯤이라 타격 불꽃도 그만큼 커야 보인다(11/13 자동 점검: 작아서 안 보임).</summary>
        static readonly System.Collections.Generic.Dictionary<string, float> sizes = new System.Collections.Generic.Dictionary<string, float>
        {
            { "VFX_PunchSpark", 8f }, { "VFX_HookImpact", 13f }, { "VFX_BoostShock", 18f }, { "VFX_GuardSpark", 10f },
            { "VFX_WeaponBreak", 25f }, { "VFX_DroneExplode", 10f }, { "VFX_MuzzleFlash", 6f }, { "VFX_ScanPing", 6f },
            { "VFX_HackSparks", 3f }, { "VFX_MapDust", 40f }, { "VFX_BossExplode", 45f },
        };

        static readonly System.Collections.Generic.Dictionary<string, float> lifetimes = new System.Collections.Generic.Dictionary<string, float>();
        static float Lifetime(string name) => lifetimes.TryGetValue(name, out var v) ? v : 1f;

        static int Save(string name, float seconds, System.Action<Transform> build)
        {
            lifetimes[name] = seconds;
            var root = new GameObject(name);
            try
            {
                build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return 1;
        }

        // ── 층(파티클 시스템 하나) ─────────────────────────────────────
        static ParticleSystem Layer(Transform root, string name, Material material, float delay, int count, float duration)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = Mathf.Max(0.1f, duration + delay);
            main.loop = false;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.Max(8, count * 2);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static ParticleSystem.MinMaxGradient FadeOut(Color from, Color to)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static void SizeCurve(ParticleSystem ps, params Keyframe[] keys)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        /// <summary>번쩍(한 장, 커졌다 사라짐).</summary>
        static void Flash(Transform root, Color color, float size, float seconds, float delay)
        {
            var ps = Layer(root, "Flash", glowAdd, delay, 1, seconds);
            var main = ps.main;
            main.startLifetime = seconds;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(Color.white, Color.white);
            SizeCurve(ps, new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.6f));
        }

        /// <summary>충격 고리(한 장, 퍼지며 사라짐).</summary>
        static void Ring(Transform root, Color color, float size, float seconds, float delay)
        {
            var ps = Layer(root, "Ring", ringAdd, delay, 1, seconds);
            var main = ps.main;
            main.startLifetime = seconds;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(Color.white, Color.white);
            SizeCurve(ps, new Keyframe(0f, 0.15f), new Keyframe(0.4f, 0.75f), new Keyframe(1f, 1f));
        }

        /// <summary>불꽃(길게 늘어난 점들이 사방으로 튐).</summary>
        static void Sparks(Transform root, int count, Color from, Color to, float speedMin, float speedMax,
            float lifeMin, float lifeMax, float size, float gravity, float delay)
        {
            var ps = Layer(root, "Sparks", sparkAdd, delay, count, lifeMax);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = gravity;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = 1.6f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(Color.white, new Color(1f, 0.5f, 0.3f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2.5f;
        }

        /// <summary>화염(밝은 덩어리가 부풀며 식음).</summary>
        static void Fire(Transform root, int count, Color color, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float spread, float delay)
        {
            var ps = Layer(root, "Fire", glowAdd, delay, count, lifeMax);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.gravityModifier = -0.05f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spread;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeOut(Color.white, new Color(0.8f, 0.2f, 0.05f));
            SizeCurve(ps, new Keyframe(0f, 0.35f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.25f));
        }

        /// <summary>연기·먼지(느리게 퍼지며 옅어짐).</summary>
        static void Smoke(Transform root, int count, Color color, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float spread, float delay)
        {
            var ps = Layer(root, "Smoke", smokeBlend, delay, count, lifeMax);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = -0.03f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spread;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            SizeCurve(ps, new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f));
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortingFudge = 2f;
        }

        // ── 텍스처·재질 ────────────────────────────────────────────────
        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>흰색 + 알파 텍스처를 그려 PNG로 저장한다. u·v는 -1~1.</summary>
        static Texture2D Tex(string name, System.Func<float, float, float> alpha)
        {
            var path = $"{TextureFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size * 2f - 1f;
                var v = (y + 0.5f) / size * 2f - 1f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v))));
            }
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name, Shader shader, Texture2D texture, bool additive)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
