using Anseong.Pilot;
using UnityEngine;
using UnityEngine.XR;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace Anseong.Combat
{
    /// <summary>
    /// 필살기 준비 보고(M2-05): 이 PC가 맡은 팔마다 "정면으로 85% 이상 뻗음 + 그립·트리거·A/B(X/Y) 모두 누름 + 추적 유효"를 보고,
    /// 바뀔 때만 전투 서버에 보낸다. 판정(0.5초 안 시작, 1.5초 충전)은 서버.
    /// 펴짐은 보정한 도달 거리 기준 사람 손(<see cref="RobotArmDriver.TargetRatio"/>). A/B·X/Y는 XR 기기에서 바로 읽는다.
    /// 시험용(에디터·개발 빌드): 키보드 F를 누르고 있으면 두 팔 모두 준비.
    /// </summary>
    [DefaultExecutionOrder(CombatDirector.ExecutionOrder + 10)]
    public class FinisherReporter : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정(보고 통로·필살기 수치). 비어 있으면 같은 오브젝트에서 찾는다.")]
        CombatDirector director;
        [SerializeField, Tooltip("손 입력. 비어 있으면 시작할 때 한 번 찾는다.")]
        PilotHandInput input;
        [SerializeField, Tooltip("왼팔·오른팔 IK(펴짐 비율). 비어 있으면 같은 오브젝트에서 찾는다.")]
        RobotArmDriver[] arms = new RobotArmDriver[0];
        [SerializeField, Range(0.1f, 1f), Tooltip("트리거를 이만큼 당기면 누름.")]
        float triggerPress = 0.6f;

        readonly RobotArmDriver[] armBySide = new RobotArmDriver[2];
        readonly bool[] sent = new bool[2];
        ICombatLink sentTo;

        /// <summary>그 팔이 지금 준비 자세인지(안내·발광용).</summary>
        public bool Ready(PilotSide side) => sent[(int)side];

        void Start()
        {
            if (director == null) director = GetComponent<CombatDirector>();
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (arms == null || arms.Length == 0) arms = GetComponents<RobotArmDriver>();
            foreach (var arm in arms)
                if (arm != null) armBySide[(int)arm.Side] = arm;
            if (director == null || input == null) enabled = false;
        }

        void LateUpdate()
        {
            var link = director.Link;
            if (link == null)
            {
                sentTo = null;
                return;
            }
            var fresh = link != sentTo;
            sentTo = link;
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                if (!OwnsArm(side, out var hand)) continue;
                var ready = Evaluate(side, hand);
                if (!fresh && ready == sent[h]) continue;
                link.SubmitFinisherReady(side, ready);
                sent[h] = ready;
            }
        }

        bool OwnsArm(PilotSide arm, out PilotSide hand)
        {
            hand = arm;
            if (input.ControlsBothArms) return input.IsArmHand(arm);
            if (arm != input.ArmSide) return false;
            for (var h = 0; h < 2; h++)
            {
                if (!input.IsArmHand((PilotSide)h)) continue;
                hand = (PilotSide)h;
                return true;
            }
            return false;
        }

        bool Evaluate(PilotSide arm, PilotSide hand)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.isPressed) return true;
#endif
            var driver = armBySide[(int)arm];
            if (driver == null) return false;
            var sample = input.Get(hand);
            var buttons = sample.GripHeld && sample.Trigger >= triggerPress && FaceButtons(hand);
            return FinisherRules.IsArmReady(driver.TargetRatio, sample.Tracked, buttons, director.Finisher);
        }

        static bool FaceButtons(PilotSide hand)
        {
            var device = InputDevices.GetDeviceAtXRNode(hand == PilotSide.Left ? XRNode.LeftHand : XRNode.RightHand);
            if (!device.isValid) return false;
            return device.TryGetFeatureValue(CommonUsages.primaryButton, out var primary) && primary
                && device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary) && secondary;
        }
    }
}
