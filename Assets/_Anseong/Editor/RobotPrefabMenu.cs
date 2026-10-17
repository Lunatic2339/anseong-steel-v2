using Anseong.Pilot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 플레이어 로봇·보스 프리팹을 메뉴 한 번으로 다시 만든다(M1-01 조종석, M1-03 두 팔 드라이버). 열린 씬은 건드리지 않는다.
    /// </summary>
    public static class RobotPrefabMenu
    {
        const string PrefabFolderParent = "Assets/_Anseong";
        const string PrefabFolderName = "Prefabs";
        public const string PlayerPrefabPath = "Assets/_Anseong/Prefabs/Robot_Player.prefab";
        public const string BossPrefabPath = "Assets/_Anseong/Prefabs/Robot_Boss.prefab";
        const string IdleControllerPath = "Assets/_Anseong/Art/Robot/Animation/Robot_Idle.controller";
        const string DevMapName = "Dev";
        const string SwitchSeatActionName = "Switch Seat";
        const string LogPrefix = "[RobotPrefabMenu]";
        const float HeightTolerance = 0.5f;      // 맞춘 높이가 65 m에서 이만큼 벗어나면 경고 (m)

        [MenuItem("Anseong/Setup/Build Player Robot Prefab")]
        static void BuildPrefabs()
        {
            var art = RobotArtSettings.Load();
            var playerModel = art.PlayerModel;
            var bossModel = art.BossModel;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(IdleControllerPath);
            if (playerModel == null || bossModel == null || controller == null)
            {
                EditorUtility.DisplayDialog("Build Player Robot Prefab",
                    $"필요한 에셋을 찾지 못했습니다.\n플레이어 모델: {art.PlayerModelPath}\n보스 모델: {art.BossModelPath}\n대기 컨트롤러: {IdleControllerPath}\n" +
                    "모델은 'Anseong → Art → Robot Art Settings'에서 바꿉니다. " +
                    "컨트롤러가 없으면 'Anseong → Sandbox → Build Scale Test'를 먼저 실행하세요.", "확인");
                return;
            }
            var switchSeat = RobotSandboxUtility.FindAction(DevMapName, SwitchSeatActionName);
            if (switchSeat == null)
                Debug.LogWarning($"{LogPrefix} '{DevMapName}/{SwitchSeatActionName}' 액션을 찾지 못했습니다. 좌석 전환 없이 만듭니다.");

            if (!AssetDatabase.IsValidFolder($"{PrefabFolderParent}/{PrefabFolderName}"))
                AssetDatabase.CreateFolder(PrefabFolderParent, PrefabFolderName);

            // 미리보기 씬에서 조립하므로 지금 열린 씬(Main 포함)에 아무것도 남기지 않는다.
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var player = CreateRobot(playerModel, controller, RobotArtSettings.AvatarOf(playerModel), "Robot_Player", preview);
                if (player == null) return;
                RobotSandboxUtility.ApplyPlayerMaterials(player);
                if (!AddCockpit(player, IdleClip(controller), switchSeat)) return;
                if (!RobotArmSetup.AddArmDrivers(player, out var armReport))
                {
                    Debug.LogError($"{LogPrefix} {armReport} 플레이어 프리팹을 만들지 않습니다.");
                    return;
                }
                Debug.Log($"{LogPrefix} 팔 드라이버: {armReport}");
                Save(player, PlayerPrefabPath);

                var boss = CreateRobot(bossModel, controller, RobotArtSettings.AvatarOf(bossModel), "Robot_Boss", preview);
                if (boss == null) return;
                RobotSandboxUtility.TintBoss(boss);
                Save(boss, BossPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} 프리팹 생성 완료: {PlayerPrefabPath}, {BossPrefabPath}. " +
                      "'Anseong → Sandbox → Build Cockpit Root Test'로 샌드박스 씬에 배치할 수 있습니다.");
        }

        /// <summary>배율 1 루트(정면 +Z) 아래에 65 m로 맞춘 모델 + 대기 Animator. 루트가 조종석의 기준이 된다.</summary>
        static Transform CreateRobot(GameObject model, AnimatorController controller, Avatar avatar, string name, Scene scene)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            robot.transform.SetParent(root.transform, false);
            robot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (!RobotSandboxUtility.FitToHeight(robot, RobotSandboxUtility.RobotHeight, out var bounds, out var scale, out var offset))
            {
                Debug.LogError($"{LogPrefix} {model.name}: 렌더러가 없어 높이를 잴 수 없습니다. 프리팹을 만들지 않습니다.");
                return null;
            }
            if (RobotSandboxUtility.TryGetRendererBounds(robot, out var fitted) && Mathf.Abs(fitted.size.y - RobotSandboxUtility.RobotHeight) > HeightTolerance)
                Debug.LogWarning($"{LogPrefix} {name}: 맞춘 높이가 {fitted.size.y:F2} m입니다(목표 {RobotSandboxUtility.RobotHeight} m). 미리보기 씬에서 bounds가 다르게 잡혔을 수 있습니다.");

            var animator = robot.GetComponent<Animator>();
            if (animator == null) animator = robot.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            // 팔 IK(M1-03)가 LateUpdate에서 덮어쓰므로 화면 밖이어도 평가한다.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var skinned = RobotSandboxUtility.EnableUpdateWhenOffscreen(root.transform);
            Debug.Log($"{LogPrefix} {name}: 측정 높이 {bounds.size.y:F3} m → 배율 {scale:F3}, 바닥 보정 {offset:F3} m. " +
                      $"updateWhenOffscreen 켬: SkinnedMeshRenderer {skinned}개");
            return root.transform;
        }

        static bool AddCockpit(Transform player, AnimationClip idleClip, UnityEngine.InputSystem.InputActionReference switchSeat)
        {
            // 게임에서는 대기 클립이 계속 돌므로 기본 자세가 아니라 대기 클립 평균 자세에서 잰다.
            // (기본 자세 기준이면 대기 자세의 몸이 더 낮아 좌석이 머리 뒤·위로 밀린다. 1차 헤드셋 확인)
            if (idleClip == null)
                Debug.LogWarning($"{LogPrefix} 대기 컨트롤러에서 클립을 찾지 못해 기본 자세로 좌석을 잽니다.");
            if (!RobotSandboxUtility.TryComputeSeatOffsets(player, out var headOffset, out var chestOffset, out var headReference, out var report, idleClip))
            {
                Debug.LogError($"{LogPrefix} {report} 플레이어 프리팹을 만들지 않습니다.");
                return false;
            }

            var rig = new GameObject("Cockpit");
            rig.transform.SetParent(player, false);
            var seat = rig.AddComponent<CockpitSeat>();
            var headRenderers = RobotSandboxUtility.FindHeadRenderers(player);
            RobotSandboxUtility.ConfigureSeat(seat, player, headOffset, chestOffset, headRenderers, switchSeat);
            RobotSandboxUtility.ConfigureFollow(seat, RobotSandboxUtility.FindBone(player, RobotSandboxUtility.HeadBone), headReference);
            seat.ApplySeatPose();
            RobotSandboxUtility.CreateCockpitProps(rig.transform);

            Debug.Log($"{LogPrefix} {report}. Head 좌석에서 숨길 머리 렌더러 {headRenderers.Length}개");
            if (headRenderers.Length == 0)
                Debug.LogWarning($"{LogPrefix} 숨길 머리 렌더러를 찾지 못했습니다. Head 좌석에서 머리가 시야를 막을 수 있습니다.");
            return true;
        }

        static AnimationClip IdleClip(AnimatorController controller)
        {
            var state = controller.layers.Length > 0 ? controller.layers[0].stateMachine.defaultState : null;
            return state != null ? state.motion as AnimationClip : null;
        }

        static void Save(Transform root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path, out var success);
            if (success) Debug.Log($"{LogPrefix} 저장: {path}");
            else Debug.LogError($"{LogPrefix} 프리팹 저장 실패: {path}");
        }
    }
}
