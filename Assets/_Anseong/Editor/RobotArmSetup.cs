using System.Collections.Generic;
using System.Linq;
using Anseong.Combat;
using Anseong.Pilot;
using UnityEditor;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 플레이어 로봇의 두 팔 드라이버(M1-03)와 펀치 분류(M1-04)를 붙이고 뼈 참조·주먹 포즈를 채운다. Mixamo 뼈 이름 탐색은 여기서만 한다.
    /// </summary>
    static class RobotArmSetup
    {
        const string LogPrefix = "[RobotArmSetup]";
        const string IndicatorMaterialPath = "Assets/_Anseong/Art/Debug/StrikeIndicator.mat";
        const string IndicatorFolderParent = "Assets/_Anseong/Art";
        const string IndicatorFolderName = "Debug";
        // 표시 구 지름 = 로봇 팔 길이 × 이 값.
        const float IndicatorSizeRatio = 0.12f;
        static readonly string[] Fingers = { "Index", "Middle", "Ring", "Pinky", "Thumb" };
        // 기존 레포 DefaultFistPose: 손가락 마디별 접는 각도(1·2·3마디).
        static readonly float[] CurlDegrees = { 55f, 45f, 25f };

        /// <summary>robotRoot 아래 "Arms" 오브젝트에 왼팔·오른팔 드라이버와 허리 회전을 만든다. 실패하면 false와 이유.</summary>
        public static bool AddArmDrivers(Transform robotRoot, out string report)
        {
            var arms = new GameObject("Arms");
            arms.transform.SetParent(robotRoot, false);
            var lines = new List<string>();
            foreach (var side in new[] { PilotSide.Left, PilotSide.Right })
            {
                var driver = arms.AddComponent<RobotArmDriver>();
                if (!Configure(driver, robotRoot, side, out var line))
                {
                    report = line;
                    return false;
                }
                lines.Add(line);
            }
            var spine = FindUnique(robotRoot, RobotSandboxUtility.SpineBone);
            if (spine == null)
            {
                report = $"허리 뼈({RobotSandboxUtility.SpineBone})를 찾지 못했습니다.";
                return false;
            }
            var torso = arms.AddComponent<TorsoTwistDriver>();
            var serialized = new SerializedObject(torso);
            serialized.FindProperty("robotRoot").objectReferenceValue = robotRoot;
            serialized.FindProperty("spine").objectReferenceValue = spine;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var driver in arms.GetComponents<RobotArmDriver>())
            {
                var arm = new SerializedObject(driver);
                arm.FindProperty("torso").objectReferenceValue = torso;
                arm.ApplyModifiedPropertiesWithoutUndo();
            }
            lines.Add("허리 회전: " + spine.name);
            lines.Add(AddPunchClassifier(arms));
            report = string.Join(" / ", lines);
            return true;
        }

        /// <summary>펀치 분류(M1-04)와 주먹 옆 개발용 표시 구를 붙인다.</summary>
        static string AddPunchClassifier(GameObject arms)
        {
            var classifier = arms.AddComponent<PunchClassifierDriver>();
            var material = GetOrCreateIndicatorMaterial();
            var serialized = new SerializedObject(classifier);
            var list = serialized.FindProperty("indicators");
            list.arraySize = 2;
            foreach (var driver in arms.GetComponents<RobotArmDriver>())
            {
                var arm = new SerializedObject(driver);
                var hand = (Transform)arm.FindProperty("hand").objectReferenceValue;
                var upper = (Transform)arm.FindProperty("upperArm").objectReferenceValue;
                var fore = (Transform)arm.FindProperty("foreArm").objectReferenceValue;
                if (hand == null || upper == null || fore == null) continue;
                var prefix = driver.Side == PilotSide.Left ? RobotSandboxUtility.LeftPrefix : RobotSandboxUtility.RightPrefix;
                var knuckle = hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == prefix + "HandMiddle1");
                var length = Vector3.Distance(upper.position, fore.position) + Vector3.Distance(fore.position, hand.position);

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"StrikeIndicator_{driver.Side}";
                // 명중 판정(M1-05)에 끼어들지 않게 충돌체는 뺀다.
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(hand, false);
                sphere.transform.position = knuckle != null ? knuckle.position : hand.position;
                var scale = Mathf.Max(1e-6f, hand.lossyScale.x);
                sphere.transform.localScale = Vector3.one * (length * IndicatorSizeRatio / scale);
                var renderer = sphere.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                list.GetArrayElementAtIndex((int)driver.Side).objectReferenceValue = renderer;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return "펀치 분류 + 주먹 표시";
        }

        static Material GetOrCreateIndicatorMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(IndicatorMaterialPath);
            if (material != null) return material;
            // 에디터에서만 셰이더를 찾는다(런타임 Shader.Find 금지). 색은 런타임에 MaterialPropertyBlock으로 바꾼다.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning($"{LogPrefix} URP Unlit 셰이더를 찾지 못해 표시 구에 기본 재질을 씁니다.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder($"{IndicatorFolderParent}/{IndicatorFolderName}"))
                AssetDatabase.CreateFolder(IndicatorFolderParent, IndicatorFolderName);
            material = new Material(shader) { color = Color.white };
            AssetDatabase.CreateAsset(material, IndicatorMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} 펀치 표시 재질 생성: {IndicatorMaterialPath}", material);
            return material;
        }

        static bool Configure(RobotArmDriver driver, Transform robotRoot, PilotSide side, out string report)
        {
            var prefix = side == PilotSide.Left ? RobotSandboxUtility.LeftPrefix : RobotSandboxUtility.RightPrefix;
            var upper = FindUnique(robotRoot, prefix + RobotSandboxUtility.ArmSuffix);
            var lower = FindUnique(robotRoot, prefix + RobotSandboxUtility.ForeArmSuffix);
            var hand = FindUnique(robotRoot, prefix + RobotSandboxUtility.HandSuffix);
            if (upper == null || lower == null || hand == null)
            {
                report = $"{side} 팔 뼈({prefix}{RobotSandboxUtility.ArmSuffix}/{RobotSandboxUtility.ForeArmSuffix}/{RobotSandboxUtility.HandSuffix})를 찾지 못했습니다.";
                return false;
            }
            if (lower.parent != upper || hand.parent != lower)
                Debug.LogWarning($"{LogPrefix} {side}: 위팔 → 아래팔 → 손이 바로 부모·자식이 아닙니다. 사이 뼈가 있으면 IK 결과가 어긋날 수 있습니다.");

            if (!TryMeasureFist(hand, prefix, out var joints, out var fistRotations, out var forward, out var back, out var palmCheck))
            {
                report = $"{side} 손가락 뼈({prefix}HandIndex1 등)를 찾지 못했습니다.";
                return false;
            }

            var serialized = new SerializedObject(driver);
            serialized.FindProperty("side").enumValueIndex = (int)side;
            serialized.FindProperty("robotRoot").objectReferenceValue = robotRoot;
            serialized.FindProperty("upperArm").objectReferenceValue = upper;
            serialized.FindProperty("foreArm").objectReferenceValue = lower;
            serialized.FindProperty("hand").objectReferenceValue = hand;
            serialized.FindProperty("fistForwardLocal").vector3Value = forward;
            serialized.FindProperty("fistBackLocal").vector3Value = back;
            var jointList = serialized.FindProperty("fingerJoints");
            var rotationList = serialized.FindProperty("fingerFistRotations");
            jointList.arraySize = joints.Length;
            rotationList.arraySize = joints.Length;
            for (var i = 0; i < joints.Length; i++)
            {
                jointList.GetArrayElementAtIndex(i).objectReferenceValue = joints[i];
                rotationList.GetArrayElementAtIndex(i).quaternionValue = fistRotations[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var upperLength = Vector3.Distance(upper.position, lower.position);
            var lowerLength = Vector3.Distance(lower.position, hand.position);
            report = $"{side} 팔: 위팔 {upperLength:F1} m, 아래팔 {lowerLength:F1} m, 손가락 {joints.Length}개, 손바닥 판별 {palmCheck:F2}";
            if (Mathf.Abs(palmCheck) < 0.2f)
                Debug.LogWarning($"{LogPrefix} {side}: 손가락이 거의 펴져 있어 손바닥 쪽 판별이 약합니다({palmCheck:F2}). 주먹이 손등 쪽으로 접히면 알려 주세요.");
            return true;
        }

        /// <summary>
        /// 기존 레포 DefaultFistPose.Configure를 옮겼다. 손가락 뿌리 위치로 주먹 정면·손등 축을 재고, 접은 회전을 계산한 뒤
        /// 손가락 뼈를 원래 회전으로 되돌린다(프리팹의 바인드 포즈를 바꾸지 않는다).
        /// palmCheck: 가운뎃손가락 굽은 방향과 손바닥 쪽의 내적(클수록 판별이 확실).
        /// </summary>
        static bool TryMeasureFist(Transform hand, string prefix, out Transform[] joints, out Quaternion[] fist,
            out Vector3 forwardLocal, out Vector3 backLocal, out float palmCheck)
        {
            joints = null;
            fist = null;
            forwardLocal = backLocal = Vector3.zero;
            palmCheck = 0f;
            var children = hand.GetComponentsInChildren<Transform>(true);
            Transform Bone(string finger, int joint) => children.FirstOrDefault(t => t.name == $"{prefix}Hand{finger}{joint}");

            var ordered = new List<Transform>();
            foreach (var finger in Fingers)
                for (var joint = 1; joint <= 3; joint++)
                {
                    var bone = Bone(finger, joint);
                    if (bone == null) return false;
                    ordered.Add(bone);
                }
            joints = ordered.ToArray();
            var rest = joints.Select(j => j.localRotation).ToArray();

            var index = Bone("Index", 1);
            var middle = Bone("Middle", 1);
            var pinky = Bone("Pinky", 1);
            var forward = ((index.position + middle.position + pinky.position) / 3f - hand.position).normalized;
            var across = (pinky.position - index.position).normalized;
            var inward = Vector3.Cross(forward, across).normalized;
            // 모델 손가락이 살짝 굽어 있으므로 그 굽은 쪽이 손바닥이다(좌우 반전 뼈의 오일러 부호를 가정하지 않는다).
            var curvature = Vector3.ProjectOnPlane(Bone("Middle", 3).position - middle.position, forward);
            palmCheck = curvature.sqrMagnitude > 1e-10f ? Vector3.Dot(curvature.normalized, inward) : 0f;
            if (palmCheck < 0f)
            {
                inward = -inward;
                palmCheck = -palmCheck;
            }
            forwardLocal = hand.InverseTransformDirection(forward);
            backLocal = hand.InverseTransformDirection(-inward);

            var axis = Vector3.Cross(forward, inward).normalized;
            var localAxes = joints.Select(j => j.InverseTransformDirection(axis)).ToArray();
            for (var i = 0; i < 12; i++)
                joints[i].localRotation = rest[i] * Quaternion.AngleAxis(CurlDegrees[i % 3], localAxes[i]);

            // 엄지는 접은 손가락 위 손바닥 쪽으로. 위치·길이는 그대로 두고 회전만 바꾼다.
            var palmLength = Vector3.Distance(hand.position, middle.position);
            var thumbTarget = (index.position + middle.position) * .5f + inward * palmLength * .55f;
            var thumb1 = Bone("Thumb", 1);
            var thumb2 = Bone("Thumb", 2);
            var thumb3 = Bone("Thumb", 3);
            Aim(thumb1, thumb2.position - thumb1.position, thumbTarget - thumb1.position);
            Aim(thumb2, thumb3.position - thumb2.position, thumbTarget - thumb2.position);
            Aim(thumb3, thumb3.position - thumb2.position, across + inward * .15f);
            fist = joints.Select(j => j.localRotation).ToArray();

            for (var i = 0; i < joints.Length; i++) joints[i].localRotation = rest[i];
            return true;
        }

        static void Aim(Transform joint, Vector3 from, Vector3 to)
        {
            if (from.sqrMagnitude > 1e-12f && to.sqrMagnitude > 1e-12f)
                joint.rotation = Quaternion.FromToRotation(from, to) * joint.rotation;
        }

        static Transform FindUnique(Transform root, string boneName)
        {
            var matches = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == boneName).ToArray();
            if (matches.Length > 1)
                Debug.LogWarning($"{LogPrefix} '{boneName}' 뼈가 {matches.Length}개입니다. 첫 번째({AnimationUtility.CalculateTransformPath(matches[0], root)})를 씁니다.");
            return matches.FirstOrDefault();
        }

        /// <summary>씬의 모든 팔 드라이버에 입력·보정을 연결한다. 연결한 개수.</summary>
        public static int ConnectDrivers(PilotHandInput input, PilotCalibrationFlow flow)
        {
            var drivers = Object.FindObjectsByType<RobotArmDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var driver in drivers) Connect(driver, input, flow);
            foreach (var torso in Object.FindObjectsByType<TorsoTwistDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Connect(torso, input, flow);
            foreach (var punch in Object.FindObjectsByType<PunchClassifierDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Connect(punch, input, flow);
            return drivers.Length;
        }

        static void Connect(Object component, PilotHandInput input, PilotCalibrationFlow flow)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty("input").objectReferenceValue = input;
            serialized.FindProperty("calibration").objectReferenceValue = flow;
            serialized.ApplyModifiedProperties();
        }
    }
}
