using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.Pilot
{
    /// <summary>
    /// 양손의 자세·추적·그립·트리거·스틱을 매 프레임 같은 장치에서 묶어 읽어 <see cref="PilotHandSample"/>로 내놓는다.
    /// <para>
    /// 손 추적과 컨트롤러가 함께 켜져 있으면 액션마다 다른 장치를 고를 수 있으므로, 위치 액션의 장치를 기준으로
    /// 회전·추적 상태·버튼을 같은 장치에서 읽는다(기존 ArmMotionInput.ReadTrackedPose 방식).
    /// 담당 팔과 이동 손은 <see cref="PilotHandRoles"/>로 정한다. 네트워크 모드에서도 손 회전·보정을 끄지 않는다(기존 버그 4).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class PilotHandInput : MonoBehaviour
    {
        /// <summary>IK·펀치 판정·보정보다 먼저 읽는다.</summary>
        public const int ExecutionOrder = -100;

        const int PoseTrackedFlags = 3; // InputTrackingState.Position | Rotation

        [Serializable]
        public class HandActions
        {
            [Tooltip("컨트롤러 위치(Vector3).")]
            public InputActionReference position;
            [Tooltip("컨트롤러 회전(Quaternion).")]
            public InputActionReference rotation;
            [Tooltip("컨트롤러 trackingState(Integer).")]
            public InputActionReference trackingState;
            [Tooltip("그립(Axis 0~1).")]
            public InputActionReference grip;
            [Tooltip("트리거(Axis 0~1).")]
            public InputActionReference trigger;
            [Tooltip("스틱(Vector2).")]
            public InputActionReference stick;
        }

        [SerializeField, Tooltip("왼손 액션(PilotInput의 Left Hand 맵).")]
        HandActions leftHand = new HandActions();

        [SerializeField, Tooltip("오른손 액션(PilotInput의 Right Hand 맵).")]
        HandActions rightHand = new HandActions();

        [SerializeField, Tooltip("이 PC 조종사가 맡은 로봇 팔. PC-A = 왼쪽, PC-B = 오른쪽. 1인 양팔 모드에서는 쓰지 않는다.")]
        PilotSide armSide = PilotSide.Left;

        [SerializeField, Tooltip("1인 양팔 모드: 양손이 모두 담당 팔이고 두 스틱 모두 이동 입력.")]
        bool controlsBothArms = true;

        [SerializeField, Range(0f, 1f), Tooltip("그립 값이 이 이상이면 쥔 것으로 본다.")]
        float gripPressThreshold = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("쥔 상태에서 그립 값이 이 이하로 내려가면 놓은 것으로 본다(누름 값보다 작게).")]
        float gripReleaseThreshold = 0.4f;

        readonly PilotHandSample[] samples = { DefaultSample(), DefaultSample() };
        readonly AttackIntentGate[] gates = { new AttackIntentGate(), new AttackIntentGate() };
        HandActions[] hands;
        bool[] enabledByMe;

        public PilotSide ArmSide
        {
            get => armSide;
            set => armSide = value;
        }

        public bool ControlsBothArms
        {
            get => controlsBothArms;
            set => controlsBothArms = value;
        }

        /// <summary>이번 프레임에 읽은 손 입력.</summary>
        public PilotHandSample Get(PilotSide hand) => samples[(int)hand];

        public AttackIntentGate Gate(PilotSide hand) => gates[(int)hand];

        public bool IsArmHand(PilotSide hand) => PilotHandRoles.IsArmHand(hand, armSide, controlsBothArms);

        public bool IsMoveHand(PilotSide hand) => PilotHandRoles.IsMoveHand(hand, armSide, controlsBothArms);

        void Awake()
        {
            hands = new[] { leftHand, rightHand };
        }

        void OnEnable()
        {
            if (hands == null) hands = new[] { leftHand, rightHand };
            enabledByMe = new bool[hands.Length * 6];
            for (var h = 0; h < hands.Length; h++)
            {
                var a = hands[h];
                var i = h * 6;
                enabledByMe[i] = EnableIfNeeded(a.position);
                enabledByMe[i + 1] = EnableIfNeeded(a.rotation);
                enabledByMe[i + 2] = EnableIfNeeded(a.trackingState);
                enabledByMe[i + 3] = EnableIfNeeded(a.grip);
                enabledByMe[i + 4] = EnableIfNeeded(a.trigger);
                enabledByMe[i + 5] = EnableIfNeeded(a.stick);
            }
        }

        void OnDisable()
        {
            if (enabledByMe == null) return;
            for (var h = 0; h < hands.Length; h++)
            {
                var a = hands[h];
                var i = h * 6;
                DisableIf(enabledByMe[i], a.position);
                DisableIf(enabledByMe[i + 1], a.rotation);
                DisableIf(enabledByMe[i + 2], a.trackingState);
                DisableIf(enabledByMe[i + 3], a.grip);
                DisableIf(enabledByMe[i + 4], a.trigger);
                DisableIf(enabledByMe[i + 5], a.stick);
            }
            enabledByMe = null;
            for (var h = 0; h < gates.Length; h++) gates[h].Reset();
        }

        void Update()
        {
            var now = Time.timeAsDouble;
            for (var h = 0; h < hands.Length; h++)
                samples[h] = Read((PilotSide)h, hands[h], samples[h], gates[h], now);
        }

        PilotHandSample Read(PilotSide side, HandActions actions, PilotHandSample previous, AttackIntentGate gate, double now)
        {
            var sample = previous;
            sample.Tracked = false;
            sample.IsArmHand = IsArmHand(side);
            sample.IsMoveHand = IsMoveHand(side);

            var device = FindDevice(actions, out var tracked, out var position, out var rotation);
            if (tracked)
            {
                sample.Tracked = true;
                sample.Position = position;
                sample.Rotation = rotation.normalized;
            }

            sample.Grip = ReadOn(actions.grip, device, 0f);
            sample.Trigger = ReadOn(actions.trigger, device, 0f);
            sample.Stick = ReadOn(actions.stick, device, Vector2.zero);
            sample.GripHeld = previous.GripHeld ? sample.Grip > gripReleaseThreshold : sample.Grip >= gripPressThreshold;
            sample.AttackIntent = gate.Update(sample.Tracked, sample.GripHeld, now);
            return sample;
        }

        /// <summary>
        /// 위치 액션에 묶인 장치 중 위치·회전이 둘 다 추적되는 첫 장치를 고른다.
        /// 추적되는 장치가 없으면 첫 장치(버튼만 읽음)를 돌려준다.
        /// </summary>
        static InputDevice FindDevice(HandActions actions, out bool tracked, out Vector3 position, out Quaternion rotation)
        {
            tracked = false;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (actions.position == null || actions.position.action == null) return null;

            InputDevice first = null;
            var controls = actions.position.action.controls;
            for (var i = 0; i < controls.Count; i++)
            {
                if (!(controls[i] is InputControl<Vector3> positionControl)) continue;
                var device = positionControl.device;
                if (!device.added || !device.enabled) continue;
                if (first == null) first = device;

                var rotationControl = FindOn<Quaternion>(actions.rotation, device);
                var stateControl = FindOn<int>(actions.trackingState, device);
                if (rotationControl == null) continue;
                // trackingState 액션이 없으면 항상 추적 중으로 본다(TrackedPoseProbe와 같은 규칙).
                if (stateControl != null && (stateControl.ReadValue() & PoseTrackedFlags) != PoseTrackedFlags) continue;

                var p = positionControl.ReadValue();
                var q = rotationControl.ReadValue();
                if (!ArmCalibration.Finite(p) || !ArmCalibration.ValidRotation(q)) continue;

                tracked = true;
                position = p;
                rotation = q;
                return device;
            }
            return first;
        }

        static InputControl<T> FindOn<T>(InputActionReference reference, InputDevice device) where T : struct
        {
            if (device == null || reference == null || reference.action == null) return null;
            var controls = reference.action.controls;
            for (var i = 0; i < controls.Count; i++)
                if (controls[i].device == device && controls[i] is InputControl<T> typed)
                    return typed;
            return null;
        }

        static T ReadOn<T>(InputActionReference reference, InputDevice device, T fallback) where T : struct
        {
            var control = FindOn<T>(reference, device);
            return control != null ? control.ReadValue() : fallback;
        }

        static PilotHandSample DefaultSample()
        {
            return new PilotHandSample { Rotation = Quaternion.identity };
        }

        static bool EnableIfNeeded(InputActionReference reference)
        {
            if (reference == null || reference.action == null || reference.action.enabled) return false;
            reference.action.Enable();
            return true;
        }

        static void DisableIf(bool enabledHere, InputActionReference reference)
        {
            if (enabledHere && reference != null && reference.action != null) reference.action.Disable();
        }
    }
}
