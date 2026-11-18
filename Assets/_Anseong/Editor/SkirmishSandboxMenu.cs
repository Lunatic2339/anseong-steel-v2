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
            if (material != null) return material;
            // 에디터에서만 셰이더를 찾는다(런타임 Shader.Find 금지).
            material = new Material(Shader.Find(shaderName));
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
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
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GruntFolder + "/G01_base_color.png"));
                var bump = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (bump != null) { material.SetTexture("_BumpMap", bump); material.EnableKeyword("_NORMALMAP"); }
                material.SetFloat("_Smoothness", 0.35f);
                material.SetFloat("_Metallic", 0.6f);
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

        public static void Build(out SkirmishView view, out Camera camera)
        {
            if (!AssetDatabase.IsValidFolder(DebugFolder)) AssetDatabase.CreateFolder("Assets/_Anseong/Art", "Debug");
            var settings = new SkirmishSettings();
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
            var furnaceLamp = Shape(PrimitiveType.Sphere, "B08 furnace status lamp", world, new Vector3(100f, 60f, 660f), Vector3.one * 16f, lamp);

            var mech = new GameObject("Mech 35m proxy").transform;
            Box("Torso", mech, new Vector3(0f, 21f, 0f), new Vector3(11f, 12f, 5f), steel);
            foreach (float x in new[] { -3.2f, 3.2f }) Box("Leg", mech, new Vector3(x, 7.5f, 0f), new Vector3(3.6f, 15f, 4.5f), steel);
            var rightArm = new GameObject("Right arm pivot (cannon)").transform;
            rightArm.SetParent(mech, false); rightArm.localPosition = new Vector3(7f, 26f, 0f);
            Box("Forearm cannon", rightArm, new Vector3(0f, 0f, 6.5f), new Vector3(2.4f, 2.4f, 13f), steel);
            var leftArm = new GameObject("Left arm pivot (shield)").transform;
            leftArm.SetParent(mech, false); leftArm.localPosition = new Vector3(-7f, 26f, 0f);
            Box("Forearm", leftArm, new Vector3(0f, 0f, 5f), new Vector3(2.2f, 2.2f, 10f), steel);
            Box("Shield", leftArm, new Vector3(0f, 0f, 10.5f), new Vector3(8f, 10f, 0.8f), Lit("Shield", new Color(0.2f, 0.7f, 0.75f)));

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
                furnaceLamp.GetComponent<Renderer>(), gunner, barrel);

            // XR 리그: 자리에 맞춰 SkirmishLocalInput이 옮긴다. 헤드셋이 없으면 같은 카메라를 마우스로 돌린다.
            var rig = new GameObject("XR Origin (Skirmish)");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(offset.transform, false);
            camera.gameObject.AddComponent<AudioListener>();
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 1500f; camera.fieldOfView = 70f;
            var origin = rig.AddComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offset;
            origin.Camera = camera;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Head position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Head rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            rig.transform.position = settings.mechStart + Vector3.up * 30f;

            var handMaterial = Unlit("Hand", new Color(0.3f, 0.9f, 1f));
            var leftHand = Box("Left hand marker", rig.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.12f), handMaterial);
            var rightHand = Box("Right hand marker", rig.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.12f), handMaterial);
            var aimLine = Tracer("Right hand aim line", rig.transform, Unlit("AimLine", new Color(1f, 0.35f, 0.2f)), 0.02f);
            var pointerLine = Tracer("Left hand pointer line", rig.transform, Unlit("PointerLine", new Color(0.3f, 0.8f, 1f)), 0.02f);
            aimLine.endWidth = pointerLine.endWidth = 0.6f;

            var input = viewRoot.AddComponent<SkirmishLocalInput>();
            input.Bind(rig.transform, offset.transform, camera.transform, leftHand, rightHand, aimLine, pointerLine);
            var hud = viewRoot.AddComponent<SkirmishHud>();
            hud.Bind(rig.transform, camera.transform);

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
            serverObject.AddComponent<SkirmishServer>().Bind(view, input, hud);

            view.Show(new SkirmishSession(settings), Vector3.forward, 0f);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
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
