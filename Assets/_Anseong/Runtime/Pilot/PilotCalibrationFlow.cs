using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Anseong.Pilot
{
    /// <summary>보정 진행 단계.</summary>
    public enum CalibrationStage
    {
        NotStarted,
        /// <summary>똑바로 서서 팔을 아래로 편하게 내리고 그립.</summary>
        Shoulder,
        /// <summary>편한 가드 자세에서 그립.</summary>
        Guard,
        /// <summary>펀치 자세(손등 위, 엄지 안쪽)로 정면으로 편하게 끝까지 뻗고 그립. 이 손 회전이 로봇 주먹 회전 기준이 된다.</summary>
        Reach,
        Complete,
    }

    /// <summary>
    /// 기체 점검 최소판: 담당 팔 손마다 어깨 → 가드 → 도달을 저장한다. 각 단계는 그립을 쥐는 순간 저장하고,
    /// 다음 단계는 그립을 한 번 놓은 뒤에만 받는다. 1인 양팔 모드에서는 양손을 같은 단계에서 각각 저장하고,
    /// 두 손이 모두 저장되면 다음 단계로 간다. 값은 그 판 동안만 메모리에 둔다.
    /// <para>안내는 개발용 Console 로그와 선택 시 기즈모뿐이다. 관객용 안내 화면은 M4.</para>
    /// </summary>
    public class PilotCalibrationFlow : MonoBehaviour
    {
        [SerializeField, Tooltip("손 입력(같은 XR Origin의 PilotHandInput).")]
        PilotHandInput input;

        [SerializeField, Tooltip("머리(XR Origin의 Main Camera). 어깨 높이 추정에 쓴다.")]
        Transform head;

        [SerializeField, Tooltip("트래킹 공간(XR Origin의 Camera Offset). 손 위치와 같은 좌표계.")]
        Transform trackingSpace;

        [SerializeField, Tooltip("보정 판정 수치.")]
        ArmCalibrationSettings settings = new ArmCalibrationSettings();

        [SerializeField, Tooltip("켜질 때 바로 어깨 단계부터 시작한다.")]
        bool startOnEnable = true;

        // 직렬화 레이아웃을 빌드 종류와 무관하게 맞추려고 #if 밖에 둔다. 사용은 개발 빌드에서만.
#pragma warning disable 0414
        [SerializeField, Tooltip("보정을 처음부터 다시 하는 버튼 액션(에디터·개발 빌드 전용).")]
        InputActionReference recalibrateAction;
#pragma warning restore 0414

        ArmCalibration[] arms;
        readonly bool[] captured = new bool[2];
        readonly bool[] waitRelease = new bool[2];
        readonly float[] headYawAtReach = new float[2];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool enabledRecalibrate;
#endif

        public CalibrationStage Stage { get; private set; }

        public bool IsComplete => Stage == CalibrationStage.Complete;

        /// <summary>보정이 끝났을 때(다시 보정해도 매번).</summary>
        public event Action Completed;

        /// <summary>머리 회전(트래킹 공간, 손 위치와 같은 좌표계). 머리나 트래킹 공간이 없으면 false.</summary>
        public bool TryGetHeadRotation(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (head == null) return false;
            rotation = trackingSpace != null ? Quaternion.Inverse(trackingSpace.rotation) * head.rotation : head.rotation;
            return true;
        }

        /// <summary>머리 위치(트래킹 공간, 손 위치와 같은 좌표계). 머리가 없으면 false.</summary>
        public bool TryGetHeadPosition(out Vector3 position)
        {
            position = HeadPosition();
            return head != null;
        }

        public ArmCalibration Get(PilotSide hand)
        {
            EnsureArms();
            return arms[(int)hand];
        }

        void Awake()
        {
            EnsureArms();
        }

        void OnEnable()
        {
            if (startOnEnable) Restart();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (recalibrateAction != null && recalibrateAction.action != null)
            {
                recalibrateAction.action.performed += OnRecalibrate;
                if (!recalibrateAction.action.enabled)
                {
                    recalibrateAction.action.Enable();
                    enabledRecalibrate = true;
                }
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (recalibrateAction != null && recalibrateAction.action != null)
            {
                recalibrateAction.action.performed -= OnRecalibrate;
                if (enabledRecalibrate) recalibrateAction.action.Disable();
            }
            enabledRecalibrate = false;
#endif
        }

        /// <summary>보정값을 모두 지우고 어깨 단계부터 다시 한다.</summary>
        public void Restart()
        {
            EnsureArms();
            foreach (var arm in arms) arm.Reset();
            for (var h = 0; h < 2; h++)
            {
                captured[h] = false;
                // 이미 쥐고 있던 그립으로 바로 저장되지 않게 한 번 놓기를 요구한다.
                waitRelease[h] = true;
            }
            EnterStage(CalibrationStage.Shoulder);
        }

        void Update()
        {
            if (input == null || Stage == CalibrationStage.NotStarted || Stage == CalibrationStage.Complete) return;

            var allDone = true;
            for (var h = 0; h < 2; h++)
            {
                var hand = (PilotSide)h;
                if (!input.IsArmHand(hand)) continue;
                if (!captured[h]) TryCaptureOnGrip(hand);
                allDone &= captured[h];
            }
            if (allDone) EnterStage(Stage + 1);
        }

        void TryCaptureOnGrip(PilotSide hand)
        {
            var h = (int)hand;
            var sample = input.Get(hand);
            if (waitRelease[h])
            {
                if (!sample.GripHeld) waitRelease[h] = false;
                return;
            }
            if (!sample.GripHeld) return;

            // 쥔 순간 한 번만 시도한다. 실패해도 놓았다가 다시 쥐어야 한다.
            waitRelease[h] = true;
            if (!sample.Tracked)
            {
                Log($"{hand} 저장 실패: 손 추적이 끊겼습니다. 그립을 놓고 다시 쥐세요.");
                return;
            }

            var arm = arms[h];
            CalibrationError error;
            switch (Stage)
            {
                case CalibrationStage.Shoulder:
                    error = arm.CaptureShoulder(HeadPosition(), sample.Position);
                    break;
                case CalibrationStage.Guard:
                    error = arm.CaptureGuard(sample.Position, sample.Rotation);
                    break;
                case CalibrationStage.Reach:
                    error = arm.CaptureReach(sample.Position, sample.Rotation);
                    headYawAtReach[h] = HeadYaw();
                    break;
                default:
                    return;
            }

            if (error != CalibrationError.None)
            {
                Log($"{hand} {Stage} 저장 실패: {Explain(error)} 그립을 놓고 다시 쥐세요.");
                return;
            }
            captured[h] = true;
            Log($"{hand} {Stage} 저장: {Describe(arm, Stage, h)}");
        }

        void EnterStage(CalibrationStage stage)
        {
            Stage = stage;
            for (var h = 0; h < 2; h++) captured[h] = false;

            switch (stage)
            {
                case CalibrationStage.Shoulder:
                    Log($"보정 시작({HandsLabel()}). 1/3 어깨: 똑바로 서서 팔을 아래로 편하게 내린 채 그립을 쥐었다 놓으세요.");
                    break;
                case CalibrationStage.Guard:
                    Log("2/3 가드: 편한 가드 자세에서 그립을 쥐었다 놓으세요.");
                    break;
                case CalibrationStage.Reach:
                    Log("3/3 도달: 펀치 자세(손등 위, 엄지 안쪽)로 정면으로 편하게 끝까지 뻗고 그립을 쥐었다 놓으세요.");
                    break;
                case CalibrationStage.Complete:
                    AlignBodyYawForBothArms();
                    for (var h = 0; h < 2; h++)
                        if (input != null && input.IsArmHand((PilotSide)h)) input.Gate((PilotSide)h).RequireRelease();
                    LogSummary();
                    Completed?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// 1인 양팔 모드: 손마다 어깨 → 손으로 잰 정면은 어깨 추정이 좌우로 조금만 틀려도 서로 안쪽·바깥쪽으로 벌어진다
        /// (1차 헤드셋 확인: 왼손 +16°, 오른손 −16°, 그때 머리 −3°). 두 손을 잇는 선에 수직인 공통 정면으로 맞춘다.
        /// </summary>
        void AlignBodyYawForBothArms()
        {
            if (input == null || !input.ControlsBothArms || !arms[0].IsComplete || !arms[1].IsComplete) return;
            var yaw = ArmCalibration.BodyYawFromHands(arms[0].ReachPoint, arms[1].ReachPoint);
            if (float.IsNaN(yaw))
            {
                Log("두 손이 너무 가까워 공통 몸 정면을 정하지 못했습니다. 손마다 잰 정면을 그대로 씁니다.");
                return;
            }
            Log($"공통 몸 정면 {yaw:F0}° (손마다 잰 값: 왼손 {arms[0].BodyYawDegrees:F0}°, 오른손 {arms[1].BodyYawDegrees:F0}°)");
            arms[0].SetBodyYaw(yaw);
            arms[1].SetBodyYaw(yaw);
        }

        Vector3 HeadPosition()
        {
            if (head == null) return new Vector3(float.NaN, float.NaN, float.NaN);
            return trackingSpace != null ? trackingSpace.InverseTransformPoint(head.position) : head.localPosition;
        }

        float HeadYaw()
        {
            if (head == null) return float.NaN;
            var forward = trackingSpace != null ? trackingSpace.InverseTransformDirection(head.forward) : head.localRotation * Vector3.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        void EnsureArms()
        {
            if (arms != null) return;
            if (settings == null) settings = new ArmCalibrationSettings();
            arms = new[] { new ArmCalibration(settings), new ArmCalibration(settings) };
        }

        string HandsLabel()
        {
            if (input == null) return "입력 없음";
            return input.ControlsBothArms ? "1인 양팔, 양손 동시" : $"담당 팔 {input.ArmSide}";
        }

        string Describe(ArmCalibration arm, CalibrationStage stage, int h)
        {
            switch (stage)
            {
                case CalibrationStage.Shoulder:
                    return $"어깨 높이 {arm.Shoulder.y:F2} m, 팔 길이 {arm.ArmLength:F2} m";
                case CalibrationStage.Guard:
                    return $"가드 거리(어깨→손) {Vector3.Distance(arm.Guard, arm.Shoulder):F2} m";
                case CalibrationStage.Reach:
                    return $"도달 거리 {arm.EffectiveReach:F2} m, 가드/도달 {arm.GuardRatio:F2}, " +
                           $"몸 정면 yaw {arm.BodyYawDegrees:F0}° (그때 머리 yaw {headYawAtReach[h]:F0}°)";
                default:
                    return string.Empty;
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void LogSummary()
        {
            var text = "보정 완료.";
            for (var h = 0; h < 2; h++)
            {
                var hand = (PilotSide)h;
                if (input == null || !input.IsArmHand(hand)) continue;
                var arm = arms[h];
                text += $"\n  {hand}: 어깨 높이 {arm.Shoulder.y:F2} m, 팔 길이 {arm.ArmLength:F2} m, " +
                        $"가드 거리 {Vector3.Distance(arm.Guard, arm.Shoulder):F2} m, 도달 거리 {arm.EffectiveReach:F2} m, " +
                        $"몸 정면 yaw {arm.BodyYawDegrees:F0}°, 가드 위치(몸 기준) {arm.ToBody(arm.Guard)}";
            }
            if (input != null && input.ControlsBothArms)
            {
                text += $"\n  양손 공통 몸 정면 {arms[0].BodyYawDegrees:F0}° (그때 머리 yaw 왼손 {headYawAtReach[0]:F0}°, 오른손 {headYawAtReach[1]:F0}°)";
            }
            Log(text);
        }

        static string Explain(CalibrationError error)
        {
            switch (error)
            {
                case CalibrationError.InvalidPose: return "추적 값이 올바르지 않습니다.";
                case CalibrationError.ArmNotHanging: return "팔을 몸 옆으로 똑바로 내려 주세요.";
                case CalibrationError.ShoulderMissing: return "어깨를 먼저 저장해야 합니다.";
                case CalibrationError.GuardOutOfRange: return "편한 가드 자세(어깨 앞)로 손을 두세요.";
                case CalibrationError.GuardMissing: return "가드를 먼저 저장해야 합니다.";
                case CalibrationError.ReachTooShort: return "가드보다 더 멀리, 편하게 끝까지 뻗어 주세요.";
                case CalibrationError.ReachNotForward: return "위·아래가 아니라 정면으로 뻗어 주세요.";
                default: return error.ToString();
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void Log(string message)
        {
            Debug.Log($"[PilotCalibration] {message}", this);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnRecalibrate(InputAction.CallbackContext context)
        {
            Log($"다시 보정 ({context.control?.path})");
            Restart();
        }
#endif

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (arms == null) return;
            var space = trackingSpace != null ? trackingSpace : transform;
            foreach (var arm in arms)
            {
                if (arm.HasShoulder)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(space.TransformPoint(arm.Shoulder), 0.04f);
                }
                if (arm.HasGuard)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(space.TransformPoint(arm.Guard), 0.04f);
                }
                if (arm.IsComplete)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(space.TransformPoint(arm.ReachPoint), 0.04f);
                    var shoulder = space.TransformPoint(arm.Shoulder);
                    Gizmos.DrawLine(shoulder, shoulder + space.TransformDirection(arm.BodyRotation * Vector3.forward) * 0.5f);
                }
            }
        }
#endif
    }
}
