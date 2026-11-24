using System.IO;
using System.Linq;
using Anseong.Net;
using Anseong.Skirmish;
using Anseong.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace Anseong.EditorTools
{
    /// <summary>
    /// Z2 교전 회색 상자 씬(공중포대 사수 + 메카 + 일반병 둘). 건물은 확대 맵 좌표의 상자 대역이다.
    /// 씬 저장은 사람이 한다. 권장 위치: Assets/_Anseong/Scenes/Sandbox/Sandbox_Z2Skirmish.unity
    /// </summary>
    public static class SkirmishSandboxMenu
    {
        const string LogPrefix = "[SkirmishSandboxMenu]";
        const string DebugFolder = "Assets/_Anseong/Art/Debug";
        const string GruntFolder = "Assets/_Anseong/Art/Enemies/G01";
        const string GruntModel = GruntFolder + "/G01_fire.fbx";
        public const string ScenePath = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Z2Skirmish.unity";
        public const string DuelScenePath = "Assets/_Anseong/Scenes/Sandbox/Sandbox_Scene1Duel.unity";
        /// <summary>성재 전달본(v066). 없으면 상자 로봇으로 만든다.</summary>
        const string RobotPrefab = "Assets/RobotDelivery_v066/Prefabs/Robot_v066_Shield_35m.prefab";
        const float RobotHeight = 35f;
        /// <summary>머리 뼈에서 조종사 눈까지(m, 35m 로봇 기준).</summary>
        static readonly Vector3 RobotEyeFromHead = new Vector3(0f, 1f, 1.5f);
        const float GruntPoseTime = 1.3f;   // 사격 클립에서 팔을 들고 조준하는 시점(초)

        [MenuItem("Anseong/Sandbox/Build Z2 Skirmish")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(out _, out _);
            Debug.Log($"{LogPrefix} Z2 교전 씬 생성 완료. {ScenePath}로 저장한 뒤 Play, 시작 화면에서 1(혼자) / 2(호스트 = 메카) / 3(클라이언트 = 공중포대)을 고르세요.");
        }

        static Material Mat(string name, string shaderName, Color color)
        {
            string path = $"{DebugFolder}/Skirmish_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                // 이미 만든 재질도 색은 코드의 값으로 맞춘다(색을 바꾸면 다시 만들지 않아도 반영).
                material.SetColor("_BaseColor", color);
                material.color = color;
                return material;
            }
            // 에디터에서만 셰이더를 찾는다(런타임 Shader.Find 금지).
            // 맵 프로젝트는 기본 렌더링(Built-in)이라 URP 셰이더가 없다. 같은 역할의 기본 셰이더로 바꾼다.
            if (BuiltIn) shaderName = shaderName.EndsWith("/Unlit") ? "Unlit/Color" : "Standard";
            material = new Material(Shader.Find(shaderName));
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>URP 없이 기본 렌더링으로 도는 프로젝트인가(맵 프로젝트).</summary>
        static bool BuiltIn => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null;

        const string BuiltInFolder = "Assets/_Anseong/Art/BuiltIn";

        /// <summary>
        /// URP 재질을 기본 렌더링용(Standard)으로 옮긴 사본을 만든다. 원본은 건드리지 않는다.
        /// URP 셰이더가 없는 프로젝트에서는 재질 값을 셰이더로 읽을 수 없어서 저장된 값을 직접 읽는다.
        /// </summary>
        static Material ToBuiltIn(Material source)
        {
            if (source == null) return null;
            if (!AssetDatabase.IsValidFolder(BuiltInFolder)) AssetDatabase.CreateFolder("Assets/_Anseong/Art", "BuiltIn");
            string path = $"{BuiltInFolder}/{source.name}_BuiltIn.mat";
            var made = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (made != null) return made;
            made = new Material(Shader.Find("Standard")) { name = source.name + "_BuiltIn" };
            var saved = new SerializedObject(source).FindProperty("m_SavedProperties");
            Texture Tex(string name)
            {
                var list = saved.FindPropertyRelative("m_TexEnvs");
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == name)
                        return list.GetArrayElementAtIndex(i).FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                return null;
            }
            float? Float(string name)
            {
                var list = saved.FindPropertyRelative("m_Floats");
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == name)
                        return list.GetArrayElementAtIndex(i).FindPropertyRelative("second").floatValue;
                return null;
            }
            Color? Col(string name)
            {
                var list = saved.FindPropertyRelative("m_Colors");
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == name)
                        return list.GetArrayElementAtIndex(i).FindPropertyRelative("second").colorValue;
                return null;
            }
            made.SetTexture("_MainTex", Tex("_BaseMap") ?? Tex("_MainTex"));
            made.color = Col("_BaseColor") ?? Col("_Color") ?? Color.white;
            var bump = Tex("_BumpMap");
            if (bump != null) { made.SetTexture("_BumpMap", bump); made.EnableKeyword("_NORMALMAP"); }
            var metal = Tex("_MetallicGlossMap");
            if (metal != null) { made.SetTexture("_MetallicGlossMap", metal); made.EnableKeyword("_METALLICGLOSSMAP"); }
            made.SetFloat("_Metallic", Float("_Metallic") ?? 0f);
            float smooth = Float("_Smoothness") ?? Float("_Glossiness") ?? 0.5f;
            made.SetFloat("_Glossiness", smooth);
            made.SetFloat("_GlossMapScale", smooth);
            var occlusion = Tex("_OcclusionMap");
            if (occlusion != null) made.SetTexture("_OcclusionMap", occlusion);
            var glow = Tex("_EmissionMap");
            var glowColor = Col("_EmissionColor") ?? Color.black;
            if (glow != null || glowColor.maxColorComponent > 0.01f)
            {
                made.SetTexture("_EmissionMap", glow);
                made.SetColor("_EmissionColor", glowColor);
                made.EnableKeyword("_EMISSION");
                made.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(made, path);
            return made;
        }

        /// <summary>기본 렌더링 프로젝트에서, 이 물건이 쓰는 URP 재질을 Standard 사본으로 바꿔 끼운다.</summary>
        static void UseBuiltInMaterials(GameObject root)
        {
            if (!BuiltIn) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && m.shader != null && m.shader.name == "Standard" ? m : ToBuiltIn(m)).ToArray();
        }

        static Material Lit(string name, Color color) => Mat(name, "Universal Render Pipeline/Lit", color);
        static Material Unlit(string name, Color color) => Mat(name, "Universal Render Pipeline/Unlit", color);

        static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static Transform Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material) =>
            Shape(PrimitiveType.Cube, name, parent, localPosition, size, material);

        static LineRenderer Tracer(string name, Transform parent, Material material, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        /// <summary>Meshy 일반병 G01을 24m로 맞춰 세운다. 모델이 없으면 상자 대역.</summary>
        static void Grunt(Transform root, float height, Material fallback)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GruntModel);
            if (model == null)
            {
                Box("Body proxy", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(10f, height, 7f), fallback);
                return;
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(GruntModel);
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.SaveAndReimport();
                model = AssetDatabase.LoadAssetAtPath<GameObject>(GruntModel);
            }
            string materialPath = GruntFolder + "/G01.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                string normalPath = GruntFolder + "/G01_normal.png";
                if (AssetImporter.GetAtPath(normalPath) is TextureImporter normal && normal.textureType != TextureImporterType.NormalMap)
                {
                    normal.textureType = TextureImporterType.NormalMap;
                    normal.SaveAndReimport();
                }
                // 기본 렌더링에서는 밝기를 올리고 테두리 빛을 내는 전용 셰이더를 쓴다(없으면 Standard).
                var rimLit = BuiltIn ? Shader.Find("Anseong/RimLit") : null;
                material = new Material(rimLit != null ? rimLit : Shader.Find(BuiltIn ? "Standard" : "Universal Render Pipeline/Lit"));
                material.SetTexture(BuiltIn ? "_MainTex" : "_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GruntFolder + "/G01_base_color.png"));
                var bump = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (bump != null) { material.SetTexture("_BumpMap", bump); material.EnableKeyword("_NORMALMAP"); }
                material.SetFloat(BuiltIn ? "_Glossiness" : "_Smoothness", 0.35f);
                // 기본 렌더링에서는 금속성이 높으면 흐린 하늘 아래에서 거의 검게 나온다.
                material.SetFloat("_Metallic", BuiltIn ? 0.15f : 0.6f);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root, false);
            var clip = AssetDatabase.LoadAllAssetsAtPath(GruntModel).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip != null) clip.SampleAnimation(instance, Mathf.Min(GruntPoseTime, clip.length));
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            if (RobotSandboxUtility.TryGetRendererBounds(instance, out var bounds) && bounds.size.y > 0.01f)
            {
                float scale = height / bounds.size.y;
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);
            }
        }

        /// <summary>움직인 정점으로 잰 실제 높이(SkinnedMeshRenderer.bounds는 넉넉하게 잡혀 있어 쓰지 않는다).</summary>
        static bool MeasureHeight(GameObject root, out float min, out float max)
        {
            min = float.MaxValue; max = float.MinValue;
            var baked = new Mesh();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.sharedMesh == null) continue;
                skin.BakeMesh(baked, true);
                // 이 모델은 구운 정점에 렌더러의 배율(0.51)을 한 번 더 곱해야 실제 자리가 된다(머리 뼈 높이와 맞춰 확인).
                var matrix = skin.transform.localToWorldMatrix;
                foreach (var v in baked.vertices)
                {
                    float y = matrix.MultiplyPoint3x4(v).y;
                    if (y < min) min = y;
                    if (y > max) max = y;
                }
            }
            Object.DestroyImmediate(baked);
            return max > min;
        }

        static bool ArmBones(Transform robot, bool isLeft, out SkirmishMechArms.Arm arm)
        {
            string prefix = isLeft ? RobotSandboxUtility.LeftPrefix : RobotSandboxUtility.RightPrefix;
            var all = robot.GetComponentsInChildren<Transform>(true);
            Transform Bone(string suffix) => all.FirstOrDefault(t => t.name == prefix + suffix);
            arm = new SkirmishMechArms.Arm
            {
                upper = Bone(RobotSandboxUtility.ArmSuffix), fore = Bone(RobotSandboxUtility.ForeArmSuffix), hand = Bone(RobotSandboxUtility.HandSuffix),
            };
            if (arm.upper == null || arm.fore == null || arm.hand == null) return false;
            if (RobotArmSetup.TryMeasureFist(arm.hand, prefix, out _, out _, out var forward, out var back, out _))
            {
                arm.fistForwardLocal = forward; arm.fistBackLocal = back;
            }
            return true;
        }

        /// <summary>
        /// 성재 전달본 로봇을 35m로 줄여 세우고 팔을 연결한다. 프리팹 이름은 35m지만 실제 높이는 50.7m라서 잰 값으로 줄인다.
        /// </summary>
        /// <param name="eye">발밑에서 조종사 눈까지.</param>
        static SkirmishMechArms Robot(Transform mech, out Vector3 eye)
        {
            eye = new Vector3(0f, 30f, 0f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefab);
            if (prefab == null) return null;
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            robot.transform.SetParent(mech, false);
            UseBuiltInMaterials(robot);
            if (!MeasureHeight(robot, out float min, out float max)) { Object.DestroyImmediate(robot); return null; }
            float scale = RobotHeight / (max - min);
            robot.transform.localScale = Vector3.one * scale;
            robot.transform.localPosition = new Vector3(0f, -min * scale, 0f);
            foreach (var skin in robot.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            if (!ArmBones(robot.transform, true, out var left) || !ArmBones(robot.transform, false, out var right))
            {
                Debug.LogWarning($"{LogPrefix} 로봇 팔 뼈를 찾지 못해 팔은 움직이지 않습니다.");
                return null;
            }
            var head = robot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == RobotSandboxUtility.HeadBone);
            if (head != null) eye = mech.InverseTransformPoint(head.position) + RobotEyeFromHead;
            var arms = robot.AddComponent<SkirmishMechArms>();
            arms.Bind(mech, left, right, robot.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c => c != null && c.GetType().Name == "ShieldController"));
            Debug.Log($"{LogPrefix} ROBOT_FIT 원래 높이 {max - min:F2} m → {RobotHeight} m (배율 {scale:F4}), 눈 {eye}, 차폐판 {(arms != null)}");
            return arms;
        }

        public static Vector3 LastMechEye { get; private set; } = new Vector3(0f, 30f, 0f);

        /// <summary>a에서 b까지 잇는 각진 막대.</summary>
        static void Bar(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var bar = Box(name, parent, (a + b) * 0.5f, new Vector3(thickness, thickness, Vector3.Distance(a, b)), material);
            bar.localRotation = Quaternion.LookRotation(b - a);
        }

        /// <summary>
        /// P1 조종석 임시 틀(사람 크기, 원점 = 조종사 발밑, 정면 = +Z). 헤드셋으로 기둥 자리와 계기판 높이를 정하는 용도다.
        /// 적과 자기 팔이 눈보다 아래에 있으므로 앞 아래쪽은 비운다: 앞 창턱은 0.5m, 계기판 윗변은 눈에서 약 45° 아래.
        /// </summary>
        /// <param name="hudMount">계기판 글자가 붙는 자리.</param>
        static Transform Cockpit(out Transform hudMount)
        {
            var frame = Lit("CockpitFrame", new Color(0.24f, 0.27f, 0.3f));
            var panel = Lit("CockpitPanel", new Color(0.1f, 0.15f, 0.18f));
            var accent = Unlit("CockpitAccent", new Color(0.15f, 0.85f, 0.9f));
            var root = new GameObject("Cockpit (P1, 임시 틀)").transform;
            const float half = 1.02f, back = -1.2f, front = 1.15f, top = 2.38f, sill = 0.5f, rearSill = 0.9f;

            Box("Rear footplate", root, new Vector3(0f, -0.03f, -0.45f), new Vector3(1.8f, 0.06f, 1.5f), frame);
            Box("Rear wall", root, new Vector3(0f, 1.2f, back), new Vector3(2.1f, 2.4f, 0.06f), frame);
            Box("Roof", root, new Vector3(0f, top + 0.04f, -0.15f), new Vector3(2.1f, 0.05f, 2.1f), frame);
            Bar("Top front bar", root, new Vector3(-half, top, front - 0.2f), new Vector3(half, top, front - 0.2f), 0.08f, frame);
            Bar("Front sill", root, new Vector3(-half, sill, front), new Vector3(half, sill, front), 0.06f, frame);
            foreach (float x in new[] { -half, half })
            {
                Bar("Pillar", root, new Vector3(x, sill, front), new Vector3(x, top, front - 0.2f), 0.07f, frame);
                Bar("Side sill", root, new Vector3(x, rearSill, back), new Vector3(x, sill, front), 0.06f, frame);
                Box("Side panel", root, new Vector3(x, 0.45f, -0.45f), new Vector3(0.04f, 0.9f, 1.5f), frame);
            }
            var dash = Box("Dashboard", root, new Vector3(0f, 0.7f, 0.62f), new Vector3(1.3f, 0.04f, 0.42f), panel);
            dash.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            Bar("Dashboard edge light", root, new Vector3(-0.65f, 0.84f, 0.78f), new Vector3(0.65f, 0.84f, 0.78f), 0.012f, accent);

            hudMount = new GameObject("HUD mount").transform;
            hudMount.SetParent(root, false);
            hudMount.localPosition = new Vector3(0f, 0.72f, 0.6f);
            hudMount.localRotation = Quaternion.Euler(50f, 0f, 0f);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        public static Transform LastCockpit { get; private set; }

        public static void Build(out SkirmishView view, out Camera camera) => Build(new SkirmishSettings(), out view, out camera);

        /// <summary>실제 맵(전시용 장면). 맵 프로젝트에만 있다.</summary>
        public const string MapScenePath = "Assets/MapShowcase/Scenes/Steelworks35m_Showcase.unity";
        public const string MapDuelScenePath = "Assets/_Anseong/Scenes/Sandbox/Scene1Duel_OnMap.unity";
        /// <summary>맵 장면에서 끌 검토용 물건(이름의 앞부분).</summary>
        static readonly string[] MapReviewRoots =
        {
            "Map35 review controls", "04 Scale proxies", "05 Routes", "06 Labels", "07 Reserved volumes",
            "08 Review cameras", "09 Control view frusta", "13 Scenario spatial rehearsal",
        };

        public static void Build(SkirmishSettings settings, out SkirmishView view, out Camera camera) => Build(settings, false, out view, out camera);

        /// <param name="settings">장면별 수치. 돌격형이 들어 있으면 돌격형 표시도 만든다.</param>
        /// <param name="onMap">회색 상자 배경 대신 실제 맵 장면을 겹쳐 쓴다(교전 장면에는 맵을 복사하지 않는다).</param>
        public static void Build(SkirmishSettings settings, bool onMap, out SkirmishView view, out Camera camera)
        {
            if (onMap && AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath) == null)
                throw new System.InvalidOperationException($"맵 장면이 없습니다: {MapScenePath} (맵 프로젝트에서만 됩니다)");
            if (!AssetDatabase.IsValidFolder(DebugFolder)) AssetDatabase.CreateFolder("Assets/_Anseong/Art", "Debug");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var concrete = Lit("Ground", new Color(0.23f, 0.24f, 0.25f));
            var yard = Lit("Yard", new Color(0.33f, 0.33f, 0.31f));
            var building = Lit("Building", new Color(0.36f, 0.27f, 0.22f));
            var steel = Lit("MechSteel", new Color(0.12f, 0.42f, 0.47f));
            var enemy = Lit("EnemyProxy", new Color(0.12f, 0.1f, 0.1f));
            var hot = Unlit("Hot", new Color(1f, 0.45f, 0.08f));
            var danger = Unlit("Danger", new Color(1f, 0.12f, 0.05f));
            var white = Unlit("Missile", new Color(0.95f, 0.95f, 0.9f));
            var tracer = Unlit("Tracer", new Color(1f, 0.9f, 0.4f));
            var lamp = Unlit("FurnaceLamp", new Color(0.2f, 0.9f, 0.3f));

            Transform furnaceLamp = null;
            if (onMap)
            {
                new GameObject("Map loader").AddComponent<SkirmishMapLoader>().Bind(MapScenePath, MapReviewRoots);
            }
            else
            {
            var sun = new GameObject("Overcast key light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);

            var world = new GameObject("Z2 graybox world (확대 맵 좌표 대역)").transform;
            Box("Ground", world, new Vector3(0f, -0.5f, 330f), new Vector3(1200f, 1f, 1200f), concrete);
            Box("Z2 yard X-110..110 Z180..360", world, new Vector3(0f, 0.02f, 270f), new Vector3(220f, 0.05f, 180f), yard);
            Box("Connector 1 X-16..16 Z100..180", world, new Vector3(0f, 0.02f, 140f), new Vector3(32f, 0.05f, 80f), yard);
            Box("B04 steelmaking hall 79.6x46.7x73", world, new Vector3(85f, 23.35f, 145f), new Vector3(79.6f, 46.7f, 73f), building);
            Box("B05 rolling mill 75x21.5x112", world, new Vector3(-155f, 10.75f, 235f), new Vector3(75f, 21.5f, 112.4f), building);
            Box("B06 silos 31x25.7x25", world, new Vector3(-155f, 12.85f, 340f), new Vector3(30.8f, 25.7f, 25.5f), building);
            Box("B07 crane beam 116 wide 68 high", world, new Vector3(90f, 64f, 555f), new Vector3(116f, 8f, 20f), Lit("Crane", new Color(0.8f, 0.6f, 0.1f)));
            foreach (float x in new[] { 34f, 146f })
                Box("B07 crane leg", world, new Vector3(x, 30f, 555f), new Vector3(6f, 60f, 6f), Lit("Crane", Color.yellow));
            Box("B08 blast furnace 103x90x104", world, new Vector3(100f, 45f, 715f), new Vector3(103f, 90f, 104f), building);
            furnaceLamp = Shape(PrimitiveType.Sphere, "B08 furnace status lamp", world, new Vector3(100f, 60f, 660f), Vector3.one * 16f, lamp);
            }

            var mech = new GameObject("Mech 35m").transform;
            var mechArms = Robot(mech, out var mechEye);
            LastMechEye = mechEye;
            Transform rightArm = null, leftArm = null;
            if (mechArms == null)
            {
            Box("Torso", mech, new Vector3(0f, 21f, 0f), new Vector3(11f, 12f, 5f), steel);
            foreach (float x in new[] { -3.2f, 3.2f }) Box("Leg", mech, new Vector3(x, 7.5f, 0f), new Vector3(3.6f, 15f, 4.5f), steel);
            rightArm = new GameObject("Right arm pivot (cannon)").transform;
            rightArm.SetParent(mech, false); rightArm.localPosition = new Vector3(7f, 26f, 0f);
            Box("Forearm cannon", rightArm, new Vector3(0f, 0f, 6.5f), new Vector3(2.4f, 2.4f, 13f), steel);
            leftArm = new GameObject("Left arm pivot (shield)").transform;
            leftArm.SetParent(mech, false); leftArm.localPosition = new Vector3(-7f, 26f, 0f);
            Box("Forearm", leftArm, new Vector3(0f, 0f, 5f), new Vector3(2.2f, 2.2f, 10f), steel);
            Box("Shield", leftArm, new Vector3(0f, 0f, 10.5f), new Vector3(8f, 10f, 0.8f), Lit("Shield", new Color(0.2f, 0.7f, 0.75f)));
            }

            var enemyRoots = new Transform[2];
            var glows = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                enemyRoots[i] = new GameObject(i == 0 ? "G3 grunt (정면)" : "G4 grunt (고로 포격)").transform;
                Grunt(enemyRoots[i], settings.enemyHeight, enemy);
                glows[i] = Shape(PrimitiveType.Sphere, i == 0 ? "G3 muzzle glow" : "G4 muzzle glow", null, Vector3.zero, Vector3.one * 2f,
                    i == 0 ? hot : danger).GetComponent<Renderer>();
                glows[i].enabled = false;
            }

            var viewRoot = new GameObject("Skirmish view");
            var bigShell = Shape(PrimitiveType.Sphere, "Big shell template", viewRoot.transform, Vector3.zero, Vector3.one * 6f, hot);
            var furnaceShell = Shape(PrimitiveType.Sphere, "Furnace shell template", viewRoot.transform, Vector3.zero, Vector3.one * 5f, danger);
            var missile = Box("Missile template", viewRoot.transform, Vector3.zero, new Vector3(1.4f, 1.4f, 7f), white);
            foreach (var template in new[] { bigShell, furnaceShell, missile }) template.gameObject.SetActive(false);

            // 공중포대 조종석. 사수 머리가 원점이고, 아래·앞 시야가 핵심이라 앞쪽 바닥은 비운다
            // (첫 캡처에서 바닥판이 메카와 G3를 가렸다). 바깥에서 보이는 큰 몸체는 뒤 아래에 둔다.
            var gunner = new GameObject("Gunship cockpit (P2)").transform;
            gunner.position = settings.gunnerPost;
            Box("Rear footplate", gunner, new Vector3(0f, -1.5f, -0.6f), new Vector3(1.6f, 0.08f, 1.4f), steel);
            foreach (float x in new[] { -0.9f, 0.9f })
                Box("Side frame", gunner, new Vector3(x, -0.75f, 0.5f), new Vector3(0.06f, 0.06f, 2.2f), steel);
            Box("Hull", gunner, new Vector3(0f, -2.8f, -4f), new Vector3(5f, 1.6f, 7f), steel);
            var barrel = new GameObject("Cannon pivot").transform;
            barrel.SetParent(gunner, false); barrel.localPosition = new Vector3(0.7f, -0.9f, 0.5f);
            Box("Cannon barrel", barrel, new Vector3(0f, 0f, 1.6f), new Vector3(0.22f, 0.22f, 3f), steel);

            view = viewRoot.AddComponent<SkirmishView>();
            view.Bind(mech, rightArm, leftArm, enemyRoots, glows, bigShell, furnaceShell, missile,
                Tracer("Cannon tracer", viewRoot.transform, tracer, 0.7f), Tracer("Mech gun tracer", viewRoot.transform, tracer, 1.2f),
                furnaceLamp != null ? furnaceLamp.GetComponent<Renderer>() : null, gunner, barrel);
            view.BindMechArms(mechArms);

            if (settings.grunts != null && settings.grunts.Length > 0)
            {
                var gruntRoot = new GameObject("Charger grunt (돌격형)").transform;
                Grunt(gruntRoot, settings.gruntHeight, enemy);
                var telegraph = Shape(PrimitiveType.Sphere, "Charger telegraph glow", null, Vector3.zero, Vector3.one * 3f, danger).GetComponent<Renderer>();
                var guardPlate = Box("Charger guard plate", null, Vector3.zero, new Vector3(13f, 12f, 1.2f), Lit("GuardPlate", new Color(0.05f, 0.05f, 0.06f))).GetComponent<Renderer>();
                telegraph.enabled = guardPlate.enabled = false;
                view.BindGrunt(gruntRoot, telegraph, guardPlate);
            }

            // XR 리그: 자리에 맞춰 SkirmishLocalInput이 옮긴다. 헤드셋이 없으면 같은 카메라를 마우스로 돌린다.
            var rig = new GameObject("XR Origin (Skirmish)");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(offset.transform, false);
            camera.gameObject.AddComponent<AudioListener>();
            camera.nearClipPlane = 0.1f; camera.farClipPlane = onMap ? 9000f : 1500f; camera.fieldOfView = 70f;
            var origin = rig.AddComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offset;
            origin.Camera = camera;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Head position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Head rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            rig.transform.position = settings.mechStart + mechEye;

            var handMaterial = Unlit("Hand", new Color(0.3f, 0.9f, 1f));
            var leftHand = Box("Left hand marker", rig.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.12f), handMaterial);
            var rightHand = Box("Right hand marker", rig.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.12f), handMaterial);
            var aimLine = Tracer("Right hand aim line", rig.transform, Unlit("AimLine", new Color(1f, 0.35f, 0.2f)), 0.02f);
            var pointerLine = Tracer("Left hand pointer line", rig.transform, Unlit("PointerLine", new Color(0.3f, 0.8f, 1f)), 0.02f);
            aimLine.endWidth = pointerLine.endWidth = 0.6f;

            var input = viewRoot.AddComponent<SkirmishLocalInput>();
            input.Bind(rig.transform, offset.transform, camera.transform, leftHand, rightHand, aimLine, pointerLine);
            input.BindMech(mechArms, mechEye);
            var cockpit = Cockpit(out var hudMount);
            LastCockpit = cockpit;
            var headMeshes = mech.GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("_Head")).ToArray();
            input.BindCockpit(cockpit, headMeshes);
            input.PlaceCockpit(settings.mechStart + mechEye, true);
            var hud = viewRoot.AddComponent<SkirmishHud>();
            hud.Bind(rig.transform, camera.transform);
            hud.BindCockpit(hudMount);
            var holo = viewRoot.AddComponent<MechHoloHud>();
            holo.Bind(cockpit, camera.transform, mechArms);
            holo.BindGlow(Shader.Find("Anseong/HoloUI"));
            // 테두리 빛이 되는 프로젝트(기본 렌더링)에서는 예고용 빨간 공을 뺀다: 몸의 붉은 빛과 표적 테두리로 충분하다.
            view.TelegraphGlow = !BuiltIn;
            hud.BindHolo(holo);

            // 접속: 기존 세션·운영자 시작 화면을 그대로 쓴다(1 = 혼자, 2 = 호스트·메카, 3 = 클라이언트·공중포대).
            var sessionObject = new GameObject("Network Session");
            var network = sessionObject.AddComponent<NetworkManager>();
            var transport = sessionObject.AddComponent<UnityTransport>();
            network.NetworkConfig ??= new NetworkConfig();
            network.NetworkConfig.NetworkTransport = transport;
            sessionObject.AddComponent<SessionBootstrap>();
            sessionObject.AddComponent<OperatorStartScreen>();

            var serverObject = new GameObject("Skirmish Server");
            serverObject.AddComponent<NetworkObject>();
            var server = serverObject.AddComponent<SkirmishServer>();
            server.Bind(view, input, hud);
            server.Configure(settings);

            view.Show(new SkirmishSession(settings), Vector3.forward, 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Anseong/Sandbox/Build Scene 1 Duel")]
        static void BuildDuelFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(SkirmishSettings.Scene1(), out _, out _);
            Debug.Log($"{LogPrefix} 장면 1(첫 상대) 씬 생성 완료. {DuelScenePath}로 저장한 뒤 Play, 시작 화면에서 1(혼자) / 2(호스트 = 메카) / 3(클라이언트 = 공중포대)을 고르세요.");
        }

        /// <summary>
        /// 배치 모드 확인용: 장면 1을 만들어 저장하고, 자동 조작으로 돌리며 몇 순간을 두 자리에서 찍어 Logs/DuelCapture에 저장한다.
        /// -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureDuelForReview
        /// </summary>
        public static void CaptureDuelForReview() => CaptureDuel(false);

        /// <summary>같은 확인을 실제 맵 위에서 한다. -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureDuelOnMap</summary>
        public static void CaptureDuelOnMap() => CaptureDuel(true);

        [MenuItem("Anseong/Sandbox/Build Scene 1 Duel On Map")]
        static void BuildDuelOnMapFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(SkirmishSettings.Scene1(), true, out _, out _);
            Directory.CreateDirectory(Path.GetDirectoryName(MapDuelScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MapDuelScenePath);
            Debug.Log($"{LogPrefix} 장면 1(맵 위) 저장: {MapDuelScenePath}. Play를 누르면 맵 장면을 겹쳐 불러옵니다.");
        }

        /// <summary>에디터에서 화면을 찍으려고 맵 장면을 겹쳐 연다. 맵 장면은 저장하지 않는다(끈 물건이 저장되면 안 된다).</summary>
        static void OpenMapForCapture(Camera camera)
        {
            var map = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Additive);
            // 후처리(색 보정 등)는 맵의 검토 카메라 것을 그대로 가져온다.
            var layerType = System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            if (layerType != null)
            {
                var from = map.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(layerType, true)).FirstOrDefault();
                if (from != null && UnityEditorInternal.ComponentUtility.CopyComponent(from)) UnityEditorInternal.ComponentUtility.PasteComponentAsNew(camera.gameObject);
            }
            SkirmishMapLoader.Prepare(map, MapReviewRoots);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(map);
        }

        static void CaptureDuel(bool onMap)
        {
            try
            {
                var settings = SkirmishSettings.Scene1();
                Build(settings, onMap, out var view, out var camera);
                string scenePath = onMap ? MapDuelScenePath : DuelScenePath;
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath)) throw new System.Exception("scene save failed");
                if (onMap) OpenMapForCapture(camera);
                var session = new SkirmishSession(settings);
                session.SetReady(true, true);
                string folder = onMap ? "Logs/DuelCaptureOnMap" : "Logs/DuelCapture";
                Directory.CreateDirectory(folder);
                var target = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
                var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                var aim = Vector3.forward;

                void Tick()
                {
                    var mech = SkirmishBots.Mech(session.State, settings);
                    if (SkirmishMath.Direction(ref mech.aim)) aim = mech.aim;
                    session.SetMech(mech);
                    session.SetGunner(SkirmishBots.Gunner(session.State, settings));
                    session.Tick(0.02f);
                    view.Show(session, aim, 0f);
                }
                var localInput = Object.FindAnyObjectByType<SkirmishLocalInput>();
                var holoHud = Object.FindAnyObjectByType<MechHoloHud>();
                void Pilot(bool on)
                {
                    localInput.PlaceCockpit(MechEye(), on);
                    camera.transform.position = MechEye();
                    holoHud.Show(session.State, settings, session.Events, on, 0f);
                }
                void Until(System.Func<SkirmishState, bool> reached, string what)
                {
                    for (int i = 0; i < 3000; i++) { Tick(); if (reached(session.State)) return; }
                    throw new System.Exception("도달하지 못함: " + what + " / 상태 " + session.State.gruntStep);
                }
                void Shot(string name, Vector3 eye, Vector3 lookAt)
                {
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                    image.Apply();
                    File.WriteAllBytes($"{folder}/{name}.png", image.EncodeToPNG());
                }
                Vector3 MechEye() => session.State.mech + LastMechEye;
                Vector3 Center() => SkirmishSession.GruntCenter(session.State, settings);
                var post = settings.gunnerPost;

                Until(st => st.gruntIndex == 0 && st.gruntStep == GruntStep.Hold, "첫 적의 예고");
                Pilot(true);
                Shot("1-p1-telegraph-slam", MechEye(), Center() + Vector3.up * 6f);
                Shot("1-p1-level", MechEye(), MechEye() + Vector3.forward);
                Shot("1-p1-look-down", MechEye(), MechEye() + new Vector3(0f, -1f, 0.75f));
                Shot("1-p1-look-left", MechEye(), MechEye() + new Vector3(-1f, -0.25f, 0.6f));
                Pilot(false);
                Shot("1-p2-telegraph-slam", post, Center());
                Shot("1-side-telegraph-slam", session.State.mech + new Vector3(-55f, 22f, 10f), session.State.mech + new Vector3(0f, 20f, 12f));
                Shot("1-front-robot", session.State.mech + new Vector3(8f, 24f, 42f), session.State.mech + Vector3.up * 19f);
                Until(st => st.gruntStep == GruntStep.Stagger, "튕겨남");
                Pilot(true);
                Shot("2-p1-stagger", MechEye(), Center());
                Pilot(false);
                Until(st => st.gruntIndex == 1 && st.gruntStep == GruntStep.Guard, "둘째 적의 가드");
                Pilot(true);
                Shot("3-p1-guard", MechEye(), Center());
                Pilot(false);
                Shot("3-p2-guard", post, Center());
                Until(st => st.phase == SkirmishPhase.Cleared, "끝");
                Shot("4-p2-cleared", post, Center());

                RenderTexture.active = null;
                camera.targetTexture = null;
                Debug.Log($"{LogPrefix} DUEL_CAPTURE_OK elapsed={session.State.elapsed:0.0} hull={session.State.hull:0}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 배치 모드 확인용: 씬을 만들고 정해진 시점 몇 개를 두 자리에서 찍어 Logs/SkirmishCapture에 저장한다.
        /// -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.CaptureForReview
        /// </summary>
        public static void CaptureForReview()
        {
            try
            {
                Build(out var view, out var camera);
                var session = new SkirmishSession();
                session.SetReady(true, true);
                string folder = "Logs/SkirmishCapture";
                Directory.CreateDirectory(folder);
                var target = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
                var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                camera.targetTexture = target;

                void Advance(float until)
                {
                    while (session.State.elapsed < until) { session.Tick(0.02f); view.Show(session, MechAim(), 0f); }
                }
                Vector3 MechAim() => (session.EnemyCenter(SkirmishSession.G3) - session.MechGun).normalized;
                void Shot(string name, Vector3 eye, Vector3 lookAt)
                {
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                    image.Apply();
                    File.WriteAllBytes($"{folder}/{name}.png", image.EncodeToPNG());
                }

                var post = session.Settings.gunnerPost;
                Vector3 MechEye() => session.State.mech + Vector3.up * 30f;

                Advance(7f);    // G3의 첫 큰 탄이 날아오는 중, G4는 걸어가는 중
                Shot("t07-gunner-forward", post, post + Quaternion.Euler(25f, 0f, 0f) * Vector3.forward * 100f);
                Shot("t07-gunner-at-g3", post, session.EnemyCenter(SkirmishSession.G3));
                Shot("t07-mech-at-g3", MechEye(), session.EnemyCenter(SkirmishSession.G3));
                Shot("t07-gunner-at-mech", post, session.MechChest);
                Advance(16.5f); // G4가 자리를 잡고 고로 포격을 충전하는 중
                Shot("t16-gunner-at-g4", post, session.EnemyCenter(SkirmishSession.G4));
                Shot("t16-gunner-forward", post, post + Quaternion.Euler(25f, 0f, 0f) * Vector3.forward * 100f);
                Shot("t16-mech-at-g4", MechEye(), session.EnemyCenter(SkirmishSession.G4));

                RenderTexture.active = null;
                camera.targetTexture = null;
                Debug.Log($"{LogPrefix} SKIRMISH_CAPTURE_OK");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 배치 모드 확인용: 씬을 만들어 저장하고 개발 빌드를 만든다(한 PC에서 두 개를 띄워 접속을 확인).
        /// -executeMethod Anseong.EditorTools.SkirmishSandboxMenu.BuildPlayerForNetworkTest
        /// </summary>
        public static void BuildPlayerForNetworkTest()
        {
            try
            {
                Build(out _, out _);
                var scene = EditorSceneManager.GetActiveScene();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new System.Exception("scene save failed");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/Z2Skirmish/AnseongZ2Skirmish.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new System.Exception("build failed: " + report.summary.result);
                Debug.Log($"{LogPrefix} SKIRMISH_BUILD_OK");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
