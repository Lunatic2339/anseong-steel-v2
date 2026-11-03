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
        const string DevMapName = "Dev";
        const string SwitchSeatActionName = "Switch Seat";
        const string SandboxFolderParent = "Assets/_Anseong/Scenes";
        const string SandboxFolderName = "Sandbox";
        const string SandboxSceneHint = "Assets/_Anseong/Scenes/Sandbox/Sandbox_EyeHeight.unity";
        const string LogPrefix = "[EyeHeightSandboxMenu]";

        const string HeadBone = RobotSandboxUtility.HeadBone;
        const string LeftPrefix = RobotSandboxUtility.LeftPrefix;
        const string RightPrefix = RobotSandboxUtility.RightPrefix;
        const string ArmSuffix = RobotSandboxUtility.ArmSuffix;
        const string ForeArmSuffix = RobotSandboxUtility.ForeArmSuffix;
        const string HandSuffix = RobotSandboxUtility.HandSuffix;

        const float BossDistance = 40f;          // 플레이어 로봇 루트 → 보스 루트 (m)

        // 가드 자세: 로봇 정면 기준 방향. X = 바깥쪽(+)/안쪽(−), Y = 위, Z = 앞.
        static readonly Vector3 GuardUpperArmDirection = new Vector3(0.3f, -0.6f, 0.75f);
        static readonly Vector3 GuardForearmDirection = new Vector3(-0.25f, 0.85f, 0.45f);

        const float GroundSize = 400f;
        const float UnityPlaneSize = 10f;
        const float CameraFarClip = 2000f;

        [MenuItem("Anseong/Sandbox/Build Eye Height Test")]
        static void BuildEyeHeightTest()
        {
            var art = RobotArtSettings.Load();
            var playerModel = art.PlayerModel;
            var bossModel = art.BossModel;
            if (playerModel == null || bossModel == null)
            {
                EditorUtility.DisplayDialog("Build Eye Height Test", $"모델을 찾지 못했습니다: {art.PlayerModelPath}, {art.BossModelPath}", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RemoveDefaultCameras(scene);

            // NewScene이 사용하지 않는 에셋을 내리므로, 액션 참조는 새 씬을 만든 뒤에 불러온다.
            var switchSeat = RobotSandboxUtility.FindAction(DevMapName, SwitchSeatActionName);
            if (switchSeat == null)
                Debug.LogWarning($"{LogPrefix} '{DevMapName}/{SwitchSeatActionName}' 액션을 찾지 못했습니다. 좌석 전환 없이 만듭니다: {RobotSandboxUtility.PilotInputPath}");

            var player = CreateRobot(playerModel, "Player Robot", Vector3.zero, 0f);
            var boss = CreateRobot(bossModel, "Boss Robot", Vector3.forward * BossDistance, 180f);
            if (player == null || boss == null) return;

            ApplyGuardPose(player);
            ApplyGuardPose(boss);
            RobotSandboxUtility.ApplyPlayerMaterials(player);
            RobotSandboxUtility.TintBoss(boss);

            CreateCockpit(player, switchSeat);
            CreateGround();
            EnsureSandboxFolder();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LogPrefix} 눈높이 테스트 씬 생성 완료. XR Origin (VR)을 추가한 뒤 'Attach XR Origin To Cockpit'을 실행하고 {SandboxSceneHint}로 저장하세요.");
        }

        [MenuItem("Anseong/Sandbox/Attach XR Origin To Cockpit")]
        static void AttachXROriginMenu() => AttachXROrigin(SceneManager.GetActiveScene());

        /// <summary>씬의 첫 XR Origin을 조종석(CockpitSeat)에 붙인다. 없으면 안내 창을 띄우고 false. Build M1 Main도 쓴다.</summary>
        internal static bool AttachXROrigin(Scene scene)
        {
            var seat = FindInScene<CockpitSeat>(scene).FirstOrDefault();
            if (seat == null)
            {
                EditorUtility.DisplayDialog("Attach XR Origin", "씬에 CockpitSeat가 없습니다. 먼저 'Build Eye Height Test'를 실행하세요.", "확인");
                return false;
            }
            var origins = FindInScene<XROrigin>(scene).ToArray();
            if (origins.Length == 0)
            {
                EditorUtility.DisplayDialog("Attach XR Origin",
                    $"'{scene.name}' 씬에 XR Origin이 없습니다.\nHierarchy 우클릭 → XR → XR Origin (VR)을 먼저 추가하세요.", "확인");
                return false;
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
            return true;
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
            RobotSandboxUtility.EnableUpdateWhenOffscreen(robotRoot);
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

        static void CreateCockpit(Transform player, InputActionReference switchSeat)
        {
            if (!RobotSandboxUtility.TryComputeSeatOffsets(player, out var headOffset, out var chestOffset, out var report))
            {
                Debug.LogError($"{LogPrefix} {report} 조종석을 만들지 않습니다.", player);
                return;
            }

            var rig = new GameObject("Cockpit");
            Undo.RegisterCreatedObjectUndo(rig, "Build Eye Height Test");
            var seat = rig.AddComponent<CockpitSeat>();
            var headRenderers = RobotSandboxUtility.FindHeadRenderers(player);
            RobotSandboxUtility.ConfigureSeat(seat, player, headOffset, chestOffset, headRenderers, switchSeat);
            seat.ApplySeatPose();

            RobotSandboxUtility.CreateCockpitProps(rig.transform);

            var leftHand = FindBone(player, LeftPrefix + HandSuffix);
            var rightHand = FindBone(player, RightPrefix + HandSuffix);
            var handY = leftHand != null && rightHand != null ? (leftHand.position.y + rightHand.position.y) * 0.5f : float.NaN;
            Debug.Log($"{LogPrefix} {report}. 가드 주먹 {handY:F1} m (앞으로 {(leftHand != null ? leftHand.position.z : float.NaN):F1} m). " +
                      $"보스 거리 {BossDistance} m. Head 좌석에서 숨길 머리 렌더러 {headRenderers.Length}개", seat);
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

        static Transform FindBone(Transform root, string boneName) => RobotSandboxUtility.FindBone(root, boneName);

        static System.Collections.Generic.IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }

        static void EnsureSandboxFolder()
        {
            var folder = $"{SandboxFolderParent}/{SandboxFolderName}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(SandboxFolderParent, SandboxFolderName);
        }
    }
}
