using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 지금 씬의 플레이어 로봇에 비교용 팔 모델(Robot Art Settings의 Comparison Arms, 기본 성재 임시 팔)을 붙인다(선우 10/28).
    /// 로봇 팔 크기에 맞춰 키우고 허리 뼈 아래에 두며, 같은 팔 IK(RobotArmDriver)를 하나씩 더 붙여 같은 손 입력으로 움직인다.
    /// 어느 팔을 보일지는 ArmModelSwap(Inspector 또는 인게임 조절 메뉴 "팔 모델")에서 바꾼다. 씬 저장은 사람이 한다.
    /// </summary>
    public static class ComparisonArmsMenu
    {
        const string LogPrefix = "[ComparisonArmsMenu]";
        const string MenuTitle = "Add Comparison Arms";
        const string InstanceName = "ComparisonArms";
        // 비교용 팔 뼈 이름(Blender 식: UpperArm.L). 손가락 Pinky는 Little로 부른다.
        const string UpperArmBone = "UpperArm";
        const string LowerArmBone = "LowerArm";
        const string HandBone = "Hand";
        const string PinkyFinger = "Pinky";
        const string LittleFinger = "Little";

        [MenuItem("Anseong/Sandbox/Add Comparison Arms")]
        static void AddComparisonArms()
        {
            if (Object.FindAnyObjectByType<ArmModelSwap>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog(MenuTitle, $"이미 비교용 팔이 있습니다. 다시 붙이려면 Hierarchy의 '{InstanceName}'을 지우고 실행하세요.", "확인");
                return;
            }
            var robotDrivers = Object.FindObjectsByType<RobotArmDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var robotLeft = robotDrivers.FirstOrDefault(d => d.Side == PilotSide.Left);
            var robotRight = robotDrivers.FirstOrDefault(d => d.Side == PilotSide.Right);
            if (robotLeft == null || robotRight == null)
            {
                EditorUtility.DisplayDialog(MenuTitle,
                    "씬에 플레이어 로봇 팔 드라이버가 없습니다. Sandbox_Pilot처럼 Robot_Player가 있는 씬을 열고 실행하세요.", "확인");
                return;
            }
            var settings = RobotArtSettings.Load();
            var model = settings.ComparisonArms;
            if (model == null)
            {
                EditorUtility.DisplayDialog(MenuTitle, $"비교용 팔 모델이 없습니다: {settings.ComparisonArmsPath}", "확인");
                return;
            }

            var robotRoot = Ref(robotLeft, "robotRoot");
            var robotUpperL = Ref(robotLeft, "upperArm");
            var robotUpperR = Ref(robotRight, "upperArm");
            var torso = robotLeft.GetComponent<TorsoTwistDriver>();
            var spine = torso != null ? (Transform)new SerializedObject(torso).FindProperty("spine").objectReferenceValue : null;
            if (spine == null) spine = robotUpperL.parent;
            var robotLength = (ArmLength(robotLeft) + ArmLength(robotRight)) * .5f;

            var scene = robotLeft.gameObject.scene;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            Undo.RegisterCreatedObjectUndo(instance, MenuTitle);
            instance.name = InstanceName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var bones = new Dictionary<PilotSide, Transform[]>();
            foreach (var side in new[] { PilotSide.Left, PilotSide.Right })
            {
                var suffix = Suffix(side);
                var chain = new[] { UpperArmBone, LowerArmBone, HandBone }.Select(b => FindBone(instance.transform, b + suffix)).ToArray();
                if (chain.Any(t => t == null))
                {
                    Object.DestroyImmediate(instance);
                    EditorUtility.DisplayDialog(MenuTitle, $"비교용 팔에서 {UpperArmBone}{suffix}/{LowerArmBone}{suffix}/{HandBone}{suffix} 뼈를 찾지 못했습니다.", "확인");
                    return;
                }
                bones[side] = chain;
            }

            // 크기: 팔 길이(위팔 + 아래팔)를 로봇 팔에 맞춘다. 방향: 왼쪽 → 오른쪽 어깨 방향을 로봇과 맞춘다(yaw만). 위치: 두 어깨 가운데를 로봇과 맞춘다.
            float Length(Transform[] c) => Vector3.Distance(c[0].position, c[1].position) + Vector3.Distance(c[1].position, c[2].position);
            var modelLength = (Length(bones[PilotSide.Left]) + Length(bones[PilotSide.Right])) * .5f;
            if (modelLength < 1e-5f)
            {
                Object.DestroyImmediate(instance);
                EditorUtility.DisplayDialog(MenuTitle, "비교용 팔 길이가 0입니다.", "확인");
                return;
            }
            var scale = robotLength / modelLength;
            instance.transform.localScale *= scale;
            var up = robotRoot.up;
            var modelAcross = Vector3.ProjectOnPlane(bones[PilotSide.Right][0].position - bones[PilotSide.Left][0].position, up);
            var robotAcross = Vector3.ProjectOnPlane(robotUpperR.position - robotUpperL.position, up);
            instance.transform.rotation = Quaternion.FromToRotation(modelAcross, robotAcross) * instance.transform.rotation;
            var modelMid = (bones[PilotSide.Left][0].position + bones[PilotSide.Right][0].position) * .5f;
            var robotMid = (robotUpperL.position + robotUpperR.position) * .5f;
            instance.transform.position += robotMid - modelMid;
            // 허리 회전·대기 동작을 같이 따라가게 허리 뼈 아래에 둔다.
            instance.transform.SetParent(spine, true);

            var lines = new List<string> { $"배율 {scale:F1}배(팔 {modelLength:F2} m → {robotLength:F1} m)" };
            foreach (var robotDriver in new[] { robotLeft, robotRight })
            {
                var side = robotDriver.Side;
                var driver = instance.AddComponent<RobotArmDriver>();
                // 조절값·입력·허리 회전 참조를 로봇 팔과 같게 하고 뼈만 바꾼다.
                EditorUtility.CopySerialized(robotDriver, driver);
                var chain = bones[side];
                if (!RobotArmSetup.ConfigureBones(driver, robotRoot, side, chain[0], chain[1], chain[2],
                        (finger, joint) => $"{(finger == PinkyFinger ? LittleFinger : finger)}.0{joint}{Suffix(side)}", out var line))
                {
                    Object.DestroyImmediate(instance);
                    EditorUtility.DisplayDialog(MenuTitle, line, "확인");
                    return;
                }
                lines.Add(line);
            }

            var robotRenderers = RobotArmRenderers(robotRoot, robotUpperL, robotUpperR);
            var swap = instance.AddComponent<ArmModelSwap>();
            var serialized = new SerializedObject(swap);
            Fill(serialized.FindProperty("robotArmRenderers"), robotRenderers);
            Fill(serialized.FindProperty("comparisonRenderers"), instance.GetComponentsInChildren<Renderer>(true));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            lines.Add($"숨길 로봇 팔 렌더러 {robotRenderers.Length}개");
            if (robotRenderers.Length == 0)
                Debug.LogWarning($"{LogPrefix} 위팔 아래 뼈에만 묶인 로봇 렌더러를 찾지 못했습니다. 비교용 팔로 바꿔도 로봇 팔이 같이 보입니다.");

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = instance;
            Debug.Log($"{LogPrefix} 비교용 팔({settings.ComparisonArmsPath}) 추가: {string.Join(" / ", lines)}. " +
                      "'ComparisonArms'의 ArmModelSwap에서 Show Comparison Arms를 켜거나, 플레이 중 조절 메뉴(오른손 B) '팔 모델'에서 바꾸세요. 씬은 직접 저장하세요.", instance);
        }

        static string Suffix(PilotSide side) => side == PilotSide.Left ? ".L" : ".R";

        static Transform Ref(Object component, string property) => (Transform)new SerializedObject(component).FindProperty(property).objectReferenceValue;

        static float ArmLength(RobotArmDriver driver)
        {
            var upper = Ref(driver, "upperArm");
            var fore = Ref(driver, "foreArm");
            var hand = Ref(driver, "hand");
            return Vector3.Distance(upper.position, fore.position) + Vector3.Distance(fore.position, hand.position);
        }

        static Transform FindBone(Transform root, string boneName)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == boneName);
        }

        /// <summary>로봇 메시 중 위팔(또는 그 아래) 뼈에만 묶인 것. 몸통과 같이 묶인 메시는 숨기지 않는다.</summary>
        static Renderer[] RobotArmRenderers(Transform robotRoot, Transform upperLeft, Transform upperRight)
        {
            bool UnderArm(Transform t) => t != null && (t.IsChildOf(upperLeft) || t.IsChildOf(upperRight));
            return robotRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.bones != null && r.bones.Length > 0 ? r.bones.All(UnderArm) : UnderArm(r.rootBone))
                .Cast<Renderer>()
                .ToArray();
        }

        static void Fill(SerializedProperty list, Renderer[] renderers)
        {
            list.arraySize = renderers.Length;
            for (var i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }
    }
}
