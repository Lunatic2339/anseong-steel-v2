using System.Linq;
using Anseong.Pilot;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 조종석 눈높이 비교(M0-04) 샌드박스 씬 메뉴. 씬 저장은 사람이 한다.
    /// </summary>
    public static class EyeHeightSandboxMenu
    {
        const string PilotInputPath = "Assets/_Anseong/Runtime/Pilot/PilotInput.inputactions";
        const string DevMapName = "Dev";
        const string SwitchSeatActionName = "Switch Seat";
        const string BossFolderParent = "Assets/_Anseong/Art";
        const string BossFolderName = "Boss";
        const string BossTintPath = "Assets/_Anseong/Art/Boss/Boss_Tint.mat";
        const string SandboxFolderParent = "Assets/_Anseong/Scenes";
        const string SandboxFolderName = "Sandbox";
        const string SandboxSceneHint = "Assets/_Anseong/Scenes/Sandbox/Sandbox_EyeHeight.unity";
        const string LogPrefix = "[EyeHeightSandboxMenu]";

        // Mixamo 본 이름 (BossUpdatedCompatible.fbx). 런타임은 이 이름을 쓰지 않고 Inspector 참조만 쓴다.
        const string HeadBone = "mixamorig:Head";
        const string HeadEndBone = "mixamorig:Head_end";
        const string SpineBone = "mixamorig:Spine";
        const string LeftPrefix = "mixamorig:Left";
        const string RightPrefix = "mixamorig:Right";
        const string ArmSuffix = "Arm";
        const string ForeArmSuffix = "ForeArm";
        const string HandSuffix = "Hand";

        const float BossDistance = 40f;          // 플레이어 로봇 루트 → 보스 루트 (m)
        static readonly Color BossColor = new Color(0.45f, 0.06f, 0.05f);

        // 좌석 초기값. 바닥 = 눈 높이 − PilotEyeHeight. 이후 CockpitSeat Inspector에서 조정한다.
        const float PilotEyeHeight = 1.6f;       // 서 있는 조종사의 눈 높이 가정 (m)
        const float HeadEyeFraction = 0.4f;      // Head → Head_end 사이 눈 위치 비율
        const float HeadSeatForward = 0f;        // 머리 좌석 앞뒤 보정 (m)
        const float ChestSeatForward = 0f;       // 가슴 좌석 앞뒤 보정 (m). 눈 높이 = 어깨(Arm 본) 높이

        // 가드 자세: 로봇 정면 기준 방향. X = 바깥쪽(+)/안쪽(−), Y = 위, Z = 앞.
        static readonly Vector3 GuardUpperArmDirection = new Vector3(0.3f, -0.6f, 0.75f);
        static readonly Vector3 GuardForearmDirection = new Vector3(-0.25f, 0.85f, 0.45f);

        // 조종석 박스 (조종석 로컬, 바닥 중심 = 원점, +Z = 앞)
        const float SeatSpacing = 2.1f;
        static readonly Vector3 FloorSize = new Vector3(3.6f, 0.05f, 2f);
        static readonly Vector3 SeatMarkSize = new Vector3(0.6f, 0.005f, 0.6f);
        static readonly Vector3 ConsoleSize = new Vector3(3.6f, 0.9f, 0.3f);
        const float ConsoleZ = 1.15f;
        const float WindowZ = 1.25f;
        const float WindowTop = 2.4f;
        const float WindowHalfWidth = 1.85f;
        const float FrameThickness = 0.12f;

        const float GroundSize = 400f;
        const float UnityPlaneSize = 10f;
        const float CameraFarClip = 2000f;

        [MenuItem("Anseong/Sandbox/Build Eye Height Test")]
        static void BuildEyeHeightTest()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RobotSandboxUtility.RobotModelPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Build Eye Height Test", $"모델을 찾지 못했습니다: {RobotSandboxUtility.RobotModelPath}", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RemoveDefaultCameras(scene);

            // NewScene이 사용하지 않는 에셋을 내리므로, 액션 참조는 새 씬을 만든 뒤에 불러온다.
            var switchSeat = FindAction(DevMapName, SwitchSeatActionName);
            if (switchSeat == null)
                Debug.LogWarning($"{LogPrefix} '{DevMapName}/{SwitchSeatActionName}' 액션을 찾지 못했습니다. 좌석 전환 없이 만듭니다: {PilotInputPath}");

            var player = CreateRobot(model, "Player Robot", Vector3.zero, 0f);
            var boss = CreateRobot(model, "Boss Robot", Vector3.forward * BossDistance, 180f);
            if (player == null || boss == null) return;

            ApplyGuardPose(player);
            ApplyGuardPose(boss);
            TintBoss(boss);

            CreateCockpit(player, switchSeat);
            CreateGround();
            EnsureSandboxFolder();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} 눈높이 테스트 씬 생성 완료. XR Origin (VR)을 추가한 뒤 'Attach XR Origin To Cockpit'을 실행하고 {SandboxSceneHint}로 저장하세요.");
        }

        [MenuItem("Anseong/Sandbox/Attach XR Origin To Cockpit")]
        static void AttachXROrigin()
        {
            var scene = SceneManager.GetActiveScene();
            var seat = FindInScene<CockpitSeat>(scene).FirstOrDefault();
            if (seat == null)
            {
                EditorUtility.DisplayDialog("Attach XR Origin", "씬에 CockpitSeat가 없습니다. 먼저 'Build Eye Height Test'를 실행하세요.", "확인");
                return;
            }
            var origins = FindInScene<XROrigin>(scene).ToArray();
            if (origins.Length == 0)
            {
                EditorUtility.DisplayDialog("Attach XR Origin",
                    $"'{scene.name}' 씬에 XR Origin이 없습니다.\nHierarchy 우클릭 → XR → XR Origin (VR)을 먼저 추가하세요.", "확인");
                return;
            }
            if (origins.Length > 1)
                Debug.LogWarning($"{LogPrefix} XR Origin이 {origins.Length}개입니다. 첫 번째({origins[0].name})만 붙입니다.", origins[0]);

            var origin = origins[0];
            Undo.SetTransformParent(origin.transform, seat.transform, "Attach XR Origin To Cockpit");

            var serialized = new SerializedObject(seat);
            serialized.FindProperty("xrOrigin").objectReferenceValue = origin;
            serialized.ApplyModifiedProperties();

            Undo.RecordObject(origin.transform, "Attach XR Origin To Cockpit");
            seat.PlaceOrigin();

            // 조종석 바닥 = 트래킹 바닥. 눈 높이는 조종사의 실제 키를 따른다.
            Undo.RecordObject(origin, "Attach XR Origin To Cockpit");
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            if (origin.Camera != null)
            {
                Undo.RecordObject(origin.Camera, "Attach XR Origin To Cockpit");
                origin.Camera.farClipPlane = Mathf.Max(origin.Camera.farClipPlane, CameraFarClip);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} XR Origin을 조종석에 붙였습니다(Floor 모드, far {CameraFarClip}m). 손 큐브가 필요하면 'Anseong → Setup → Add Tracking Probes'도 실행하세요.", origin);
        }

        [MenuItem("Anseong/Sandbox/Apply Guard Pose To Selection")]
        static void ApplyGuardPoseToSelection()
        {
            var root = Selection.activeTransform;
            if (root == null || FindBone(root, HeadBone) == null)
            {
                EditorUtility.DisplayDialog("Apply Guard Pose", "로봇 루트(Player Robot / Boss Robot)를 선택하세요.", "확인");
                return;
            }
            ApplyGuardPose(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        /// <summary>배율 1 루트(정면 +Z) 아래에 65m 모델을 둔다. 루트가 CockpitSeat의 정면 기준이 된다.</summary>
        static Transform CreateRobot(GameObject model, string name, Vector3 position, float yaw)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Undo.RegisterCreatedObjectUndo(root, "Build Eye Height Test");

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            robot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (!RobotSandboxUtility.FitToHeight(robot, RobotSandboxUtility.RobotHeight, out var bounds, out var scale, out var offset))
            {
                Debug.LogError($"{LogPrefix} {model.name}: 렌더러가 없어 높이를 잴 수 없습니다.", robot);
                return null;
            }

            // 정지 자세를 유지한다(이 작업에는 애니메이션이 필요 없다).
            var animator = robot.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            Debug.Log($"{LogPrefix} {name}: 측정 높이 {bounds.size.y:F3} m → 배율 {scale:F3}, 바닥 보정 {offset:F3} m", root);
            return root.transform;
        }

        /// <summary>
        /// 팔을 고정 가드 자세로 세운다. 위팔·아래팔을 로봇 정면 기준 방향으로 조준할 뿐 IK는 아니다.
        /// 적용 후 Scene 뷰에서 본을 직접 돌려 다듬고 씬을 저장하면 된다.
        /// </summary>
        static void ApplyGuardPose(Transform robotRoot)
        {
            foreach (var prefix in new[] { LeftPrefix, RightPrefix })
            {
                var arm = FindBone(robotRoot, prefix + ArmSuffix);
                var foreArm = FindBone(robotRoot, prefix + ForeArmSuffix);
                var hand = FindBone(robotRoot, prefix + HandSuffix);
                if (arm == null || foreArm == null || hand == null)
                {
                    Debug.LogError($"{LogPrefix} {robotRoot.name}: '{prefix}' 팔 본을 찾지 못해 가드 자세를 건너뜁니다.", robotRoot);
                    continue;
                }

                // 어깨가 루트의 어느 쪽에 있는지로 바깥 방향을 정한다(모델 좌우 규칙에 의존하지 않음).
                var outward = Mathf.Sign(robotRoot.InverseTransformDirection(arm.position - robotRoot.position).x);
                Undo.RecordObjects(new Object[] { arm, foreArm }, "Apply Guard Pose");
                Aim(arm, foreArm.position, robotRoot.TransformDirection(Mirror(GuardUpperArmDirection, outward)));
                Aim(foreArm, hand.position, robotRoot.TransformDirection(Mirror(GuardForearmDirection, outward)));
            }

            // 스킨 메시의 컬링 bounds는 T자세 기준으로 저장되어 있어, 팔을 옮기면 손가락 조각이
            // bounds 밖으로 나가 시선에 따라 깜빡인다. 매 프레임 실제 자세로 bounds를 다시 잡게 한다.
            foreach (var skinned in robotRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Undo.RecordObject(skinned, "Apply Guard Pose");
                skinned.updateWhenOffscreen = true;
            }
        }

        static Vector3 Mirror(Vector3 direction, float outward)
        {
            return new Vector3(direction.x * outward, direction.y, direction.z).normalized;
        }

        static void Aim(Transform bone, Vector3 childPosition, Vector3 targetDirection)
        {
            var current = childPosition - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current, targetDirection) * bone.rotation;
        }

        static void TintBoss(Transform boss)
        {
            var renderers = boss.GetComponentsInChildren<Renderer>(true);
            var tint = GetOrCreateBossTint(renderers);
            if (tint == null) return;
            foreach (var r in renderers)
                r.sharedMaterials = Enumerable.Repeat(tint, r.sharedMaterials.Length).ToArray();
        }

        static Material GetOrCreateBossTint(Renderer[] renderers)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BossTintPath);
            if (material != null) return material;

            // 로봇 재질과 같은 셰이더(URP Lit)를 쓴다. 텍스처 없이 색만.
            var source = renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null);
            if (source == null)
            {
                Debug.LogWarning($"{LogPrefix} 로봇 재질이 없어 보스 색을 바꾸지 않습니다.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder($"{BossFolderParent}/{BossFolderName}"))
                AssetDatabase.CreateFolder(BossFolderParent, BossFolderName);

            material = new Material(source.shader) { color = BossColor };
            AssetDatabase.CreateAsset(material, BossTintPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} 보스 색 재질 생성: {BossTintPath}", material);
            return material;
        }

        static void CreateCockpit(Transform player, InputActionReference switchSeat)
        {
            var head = FindBone(player, HeadBone);
            var headEnd = FindBone(player, HeadEndBone);
            var spine = FindBone(player, SpineBone);
            var leftArm = FindBone(player, LeftPrefix + ArmSuffix);
            var rightArm = FindBone(player, RightPrefix + ArmSuffix);
            var leftHand = FindBone(player, LeftPrefix + HandSuffix);
            var rightHand = FindBone(player, RightPrefix + HandSuffix);
            if (head == null || headEnd == null || spine == null || leftArm == null || rightArm == null)
            {
                Debug.LogError($"{LogPrefix} 좌석 기준 본을 찾지 못했습니다. 조종석을 만들지 않습니다.", player);
                return;
            }

            var toRobot = Quaternion.Inverse(CockpitSeatMath.YawOnly(player.rotation));

            var headEye = Vector3.Lerp(head.position, headEnd.position, HeadEyeFraction);
            var headFloor = headEye + Vector3.down * PilotEyeHeight;
            var headOffset = toRobot * (headFloor - head.position) + Vector3.forward * HeadSeatForward;

            var shoulderY = (leftArm.position.y + rightArm.position.y) * 0.5f;
            var chestFloor = new Vector3(spine.position.x, shoulderY - PilotEyeHeight, spine.position.z);
            var chestOffset = toRobot * (chestFloor - spine.position) + Vector3.forward * ChestSeatForward;

            var rig = new GameObject("Cockpit");
            Undo.RegisterCreatedObjectUndo(rig, "Build Eye Height Test");
            var seat = rig.AddComponent<CockpitSeat>();

            var serialized = new SerializedObject(seat);
            serialized.FindProperty("robotRoot").objectReferenceValue = player;
            var seats = serialized.FindProperty("seats");
            seats.arraySize = 2;
            SetSeat(seats.GetArrayElementAtIndex(0), "Head", head, headOffset);
            SetSeat(seats.GetArrayElementAtIndex(1), "ChestTop", spine, chestOffset);
            serialized.FindProperty("activeSeat").intValue = 0;
            serialized.FindProperty("seatSpacing").floatValue = SeatSpacing;
            serialized.FindProperty("switchSeatAction").objectReferenceValue = switchSeat;
            var headRenderers = FindRenderersMostlyOn(player, head, headEnd);
            var hidden = seats.GetArrayElementAtIndex(0).FindPropertyRelative("hideWhileSeated");
            hidden.arraySize = headRenderers.Length;
            for (var i = 0; i < headRenderers.Length; i++)
                hidden.GetArrayElementAtIndex(i).objectReferenceValue = headRenderers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            seat.ApplySeatPose();

            CreateCockpitProps(rig.transform);

            var handY = leftHand != null && rightHand != null ? (leftHand.position.y + rightHand.position.y) * 0.5f : float.NaN;
            Debug.Log($"{LogPrefix} 본 높이: Head {head.position.y:F1} m, Head_end {headEnd.position.y:F1} m, Spine {spine.position.y:F1} m, " +
                      $"어깨 {shoulderY:F1} m, 가드 주먹 {handY:F1} m (앞으로 {(leftHand != null ? leftHand.position.z : float.NaN):F1} m)", player);
            Debug.Log($"{LogPrefix} 좌석 눈 높이(키 {PilotEyeHeight} m 가정): Head {headEye.y:F1} m, ChestTop {shoulderY:F1} m. " +
                      $"오프셋 Head {headOffset}, ChestTop {chestOffset}. 보스 거리 {BossDistance} m. " +
                      $"Head 좌석에서 숨길 머리 렌더러 {headRenderers.Length}개", seat);
        }

        /// <summary>
        /// 정점의 절반 이상이 주어진 본에 가장 크게 묶인 스킨 메시 렌더러(예: 머리 장갑판).
        /// </summary>
        static Renderer[] FindRenderersMostlyOn(Transform robotRoot, params Transform[] bones)
        {
            var result = new System.Collections.Generic.List<Renderer>();
            foreach (var skinned in robotRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinned.sharedMesh;
                var rendererBones = skinned.bones;
                if (mesh == null || rendererBones == null || mesh.vertexCount == 0) continue;

                var weights = mesh.boneWeights;
                if (weights.Length == 0)
                {
                    // 가중치가 없으면 rootBone 하나에 붙은 메시다.
                    if (bones.Contains(skinned.rootBone)) result.Add(skinned);
                    continue;
                }
                var onBones = 0;
                foreach (var w in weights)
                {
                    var index = w.boneIndex0;
                    if (index >= 0 && index < rendererBones.Length && bones.Contains(rendererBones[index]))
                        onBones++;
                }
                if (onBones * 2 >= weights.Length)
                    result.Add(skinned);
            }
            return result.ToArray();
        }

        static void SetSeat(SerializedProperty element, string label, Transform bone, Vector3 offset)
        {
            element.FindPropertyRelative("label").stringValue = label;
            element.FindPropertyRelative("bone").objectReferenceValue = bone;
            element.FindPropertyRelative("offsetMeters").vector3Value = offset;
        }

        static void CreateCockpitProps(Transform rig)
        {
            var root = new GameObject("Cockpit Props");
            root.transform.SetParent(rig, false);

            CreateBox(root.transform, "Floor", FloorSize, new Vector3(0f, -FloorSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Seat Mark L", SeatMarkSize, new Vector3(-SeatSpacing * 0.5f, SeatMarkSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Seat Mark R", SeatMarkSize, new Vector3(SeatSpacing * 0.5f, SeatMarkSize.y * 0.5f, 0f));
            CreateBox(root.transform, "Console", ConsoleSize, new Vector3(0f, ConsoleSize.y * 0.5f, ConsoleZ));

            var postSize = new Vector3(FrameThickness, WindowTop, FrameThickness);
            CreateBox(root.transform, "Window Post L", postSize, new Vector3(-WindowHalfWidth, WindowTop * 0.5f, WindowZ));
            CreateBox(root.transform, "Window Post R", postSize, new Vector3(WindowHalfWidth, WindowTop * 0.5f, WindowZ));
            CreateBox(root.transform, "Window Top", new Vector3(WindowHalfWidth * 2f + FrameThickness, FrameThickness, FrameThickness),
                new Vector3(0f, WindowTop, WindowZ));
        }

        static void CreateBox(Transform parent, string name, Vector3 size, Vector3 localPosition)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localScale = size;
            box.transform.localPosition = localPosition;
        }

        static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * (GroundSize / UnityPlaneSize);
            Undo.RegisterCreatedObjectUndo(ground, "Build Eye Height Test");
        }

        static void RemoveDefaultCameras(Scene scene)
        {
            foreach (var camera in FindInScene<Camera>(scene).ToArray())
                Object.DestroyImmediate(camera.gameObject);
        }

        static Transform FindBone(Transform root, string boneName)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == boneName);
        }

        static System.Collections.Generic.IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }

        static InputActionReference FindAction(string mapName, string actionName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(PilotInputPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap != null &&
                                     r.action.actionMap.name == mapName && r.action.name == actionName);
        }

        static void EnsureSandboxFolder()
        {
            var folder = $"{SandboxFolderParent}/{SandboxFolderName}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(SandboxFolderParent, SandboxFolderName);
        }
    }
}
