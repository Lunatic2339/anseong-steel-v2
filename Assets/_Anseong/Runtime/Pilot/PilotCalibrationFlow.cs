using System;
using System.Diagnostics;
using Anseong.Core;
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

    /// <summary>보정 순서.</summary>
    public enum CalibrationSteps
    {
        /// <summary>정면 뻗기 → 가드(기본, 선우 10/28). 몸 정면 = 헤드셋, 어깨 = 뻗은 손 바로 뒤, 모든 값은 목 기준. docs/design/09_calibration.md</summary>
        TwoStep,
        /// <summary>어깨(팔 내리기) → 가드 → 도달(10/28 이전). 아래 3단계 전용 설정을 쓴다.</summary>
        ThreeStep,
    }

    /// <summary>보정 때 몸 정면(yaw)을 무엇으로 정하나(3단계 전용).</summary>
    public enum BodyYawSource
    {
        /// <summary>도달 단계 때 머리(헤드셋) 정면. 손·어깨 추정 오차에 영향받지 않는다(기본, 선우 10/28).</summary>
        Head,
        /// <summary>손으로 잰 정면(1인 양팔: 두 도달 손을 잇는 선에 수직, 한 팔: 어깨 → 손). 10/28 이전 방식.</summary>
        Hands,
    }

    /// <summary>보정이 끝날 때 사람 어깨를 어디에 두나(3단계 전용).</summary>
    public enum ShoulderPlacement
    {
        /// <summary>도달 손 바로 뒤(좌우 = 도달 손, 앞뒤 = 목). 보정 때처럼 정면으로 뻗으면 로봇도 정면(기본, 선우 10/28).</summary>
        BehindReach,
        /// <summary>1인 양팔: 머리 좌우 대칭, 간격은 내린 손으로 잰 값(10/28 오전 방식).</summary>
        MirrorHead,
        /// <summary>내린 손 위치 그대로(처음 방식).</summary>
        Hanging,
    }

    /// <summary>
    /// 기체 점검 최소판: 담당 팔 손마다 정면 뻗기 → 가드(기본, 2단계) 또는 어깨 → 가드 → 도달(3단계)을 저장한다. 각 단계는 그립을 쥐는 순간 저장하고,
    /// 다음 단계는 그립을 한 번 놓은 뒤에만 받는다. 1인 양팔 모드에서는 양손을 같은 단계에서 각각 저장하고,
    /// 두 손이 모두 저장되면 다음 단계로 간다. 값은 그 판 동안만 메모리에 둔다.
    /// <para>안내는 개발용 Console 로그, 헤드셋 안 테스트 안내판(TestGuidePanel), 선택 시 기즈모다. 관객용 안내 화면은 M4.</para>
    /// </summary>
    public class PilotCalibrationFlow : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("손 입력(같은 XR Origin의 PilotHandInput).")]
        PilotHandInput input;

        [SerializeField, Tooltip("머리(XR Origin의 Main Camera). 어깨 높이 추정에 쓴다.")]
        Transform head;

        [SerializeField, Tooltip("트래킹 공간(XR Origin의 Camera Offset). 손 위치와 같은 좌표계.")]
        Transform trackingSpace;

        [SerializeField, Tooltip("보정 판정 수치.")]
        ArmCalibrationSettings settings = new ArmCalibrationSettings();

        [SerializeField, Tooltip("보정 뒤 몸이 방 안에서 옮겨 가면 보정한 어깨·가드도 따라가게 한다(팔 IK·허리·펀치 분류가 같이 쓴다).")]
        BodyFollowSettings bodyFollow = new BodyFollowSettings();

        [SerializeField, Tooltip("보정 순서. TwoStep = 정면 뻗기 → 가드(기본, 선우 10/28: 어떤 기기·체격이든 정면 = 정면). " +
            "ThreeStep = 어깨 → 가드 → 도달(이전 방식, 아래 3단계 설정을 씀). 바꾸면 다시 보정해야 적용된다.")]
        CalibrationSteps steps = CalibrationSteps.TwoStep;

        [SerializeField, Tooltip("헤드셋(눈) → 목 오프셋(m, 머리 로컬: X 오른쪽, Y 위, Z 앞). 고개 회전 중심. 2단계는 모든 값을 목 기준으로 잰다.")]
        Vector3 eyeToNeck = new Vector3(0f, -0.1f, -0.09f);

        [SerializeField, Tooltip("보정이 끝난 뒤 처음 정면 잽 몇 번의 방향을 보고 어깨 좌우를 조금 옮겨 잽이 곧게 나가게 한다(기본, 선우 10/28). 끄면 보정값 그대로.")]
        bool jabCheck = true;

        [SerializeField, Tooltip("정면 잽 확인: 몇 번을 모아 평균하나.")]
        int jabCheckCount = 3;

        [SerializeField, Tooltip("정면 잽 확인: 평균 방향이 이 각도(°) 이내일 때만 자동으로 맞춘다. 넘으면 다시 보정하라고 안내만 한다.")]
        float jabCheckMaxDegrees = 8f;

        [SerializeField, Tooltip("정면 잽 확인: 어깨를 옮기는 최대 거리(m).")]
        float jabCheckMaxShift = 0.06f;

        [Header("3단계 전용(이전 방식)")]
        [SerializeField, Tooltip("3단계: 몸 정면 기준. Hands = 손으로 잰 정면(10/28 이전), Head = 도달 단계 때 헤드셋 정면.")]
        BodyYawSource bodyYawSource = BodyYawSource.Hands;

        [SerializeField, Tooltip("3단계: 보정이 끝날 때 사람 어깨 위치. MirrorHead = 머리 좌우 대칭(10/28 오전), BehindReach = 도달 손 바로 뒤, Hanging = 내린 손 그대로.")]
        ShoulderPlacement shoulderPlacement = ShoulderPlacement.MirrorHead;

        [SerializeField, Tooltip("3단계 BehindReach: 목에서 어깨까지 좌우 거리 한계(m).")]
        Vector2 shoulderHalfWidthRange = new Vector2(0.1f, 0.3f);

        [SerializeField, Tooltip("3단계: 몸 이동 보정이 헤드셋(눈) 대신 목을 따라간다. 끄면 눈 위치(10/28 이전). 2단계는 늘 목.")]
        bool followNeck;

        Vector3 neckAtShoulder;
        bool hasHeadAtShoulder;
        // 2단계 정면 뻗기: 손마다 저장한 순간의 원래 값. 두 손이 다 저장되면 공통 목·정면으로 다시 계산한다.
        readonly Vector3[] forwardHand = new Vector3[2];
        readonly Quaternion[] forwardRotation = new Quaternion[2];
        readonly Vector3[] forwardNeck = new Vector3[2];
        readonly float[] forwardYaw = new float[2];
        // 정면 잽 확인
        readonly int[] jabCount = new int[2];
        readonly Vector3[] jabSum = new Vector3[2];
        readonly bool[] jabChecked = new bool[2];

        [SerializeField, Tooltip("켜질 때 바로 어깨 단계부터 시작한다.")]
        bool startOnEnable = true;

        // 직렬화 레이아웃을 빌드 종류와 무관하게 맞추려고 #if 밖에 둔다. 사용은 개발 빌드에서만.
#pragma warning disable 0414
        [SerializeField, Tooltip("보정을 처음부터 다시 하는 버튼 액션(에디터·개발 빌드 전용).")]
        InputActionReference recalibrateAction;
#pragma warning restore 0414

        ArmCalibration[] arms;
        readonly BodyFollow follow = new BodyFollow();
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

        /// <summary>보정이 끝난 뒤 어깨 위치를 조금 고쳤을 때(정면 잽 확인). 어깨 기준 값을 쓰는 곳은 다시 읽는다.</summary>
        public event Action Adjusted;

        public bool TwoStep => steps == CalibrationSteps.TwoStep;

        /// <summary>지금 단계 안내 문장.</summary>
        public string CurrentInstruction => Instruction(Stage, TwoStep);

        /// <summary>안내·저장 결과 한 줄(개발용 로그와 같은 내용). 헤드셋 안 안내판이 받는다. 에디터·개발 빌드에서만 온다.</summary>
        public event Action<string> Notice;

        /// <summary>이번 단계에서 이 손을 저장했다.</summary>
        public bool IsCaptured(PilotSide hand) => captured[(int)hand];

        /// <summary>이 손이 보정 대상(담당 팔)이다.</summary>
        public bool UsesHand(PilotSide hand) => input != null && input.IsArmHand(hand);

        /// <summary>단계별 안내 문장(사람이 읽는 것).</summary>
        public static string Instruction(CalibrationStage stage, bool twoStep)
        {
            if (!twoStep) return Instruction(stage);
            switch (stage)
            {
                case CalibrationStage.Reach:
                    return "1/2 정면 뻗기: 똑바로 서서 정면을 보고, 펀치 자세(손등 위, 엄지 안쪽)로 정면 끝까지 편하게 뻗은 채 그립을 쥐었다 놓으세요.";
                case CalibrationStage.Guard:
                    return "2/2 가드: 편한 가드 자세에서 그립을 쥐었다 놓으세요.";
                default:
                    return Instruction(stage);
            }
        }

        /// <summary>3단계 안내 문장.</summary>
        public static string Instruction(CalibrationStage stage)
        {
            switch (stage)
            {
                case CalibrationStage.Shoulder:
                    return "1/3 어깨: 똑바로 서서 팔을 아래로 편하게 내린 채 그립을 쥐었다 놓으세요.";
                case CalibrationStage.Guard:
                    return "2/3 가드: 편한 가드 자세에서 그립을 쥐었다 놓으세요.";
                case CalibrationStage.Reach:
                    return "3/3 도달: 펀치 자세(손등 위, 엄지 안쪽)로 정면으로 편하게 끝까지 뻗고 그립을 쥐었다 놓으세요.";
                case CalibrationStage.Complete:
                    return "보정 완료.";
                default:
                    return "보정 대기 중.";
            }
        }

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

        /// <summary>보정 뒤 몸이 옮겨 간 양(트래킹 공간, m). 끄거나 보정 전이면 0.</summary>
        public Vector3 BodyOffset => follow.Offset;

        /// <summary>몸 이동 보정이 지금 켜져 있고 기준이 잡혀 있다.</summary>
        public bool BodyFollowActive => follow.HasReference && bodyFollow != null && bodyFollow.enabled;

        /// <summary>
        /// 손 위치(트래킹 공간)를 "보정한 자리에 서 있는 것처럼" 바꾼다. 보정값(어깨·가드·도달)과 비교하는 모든 곳은 이 값을 쓴다.
        /// 플레이어가 걸어 옮겨도 가드 자세면 팔이 움직이지 않는다.
        /// </summary>
        public Vector3 CompensateHand(Vector3 trackingHand) => trackingHand - follow.Offset;

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

        /// <summary>보정값을 모두 지우고 첫 단계부터 다시 한다.</summary>
        public void Restart()
        {
            EnsureArms();
            follow.Clear();
            foreach (var arm in arms) arm.Reset();
            for (var h = 0; h < 2; h++)
            {
                captured[h] = false;
                // 이미 쥐고 있던 그립으로 바로 저장되지 않게 한 번 놓기를 요구한다.
                waitRelease[h] = true;
            }
            EnterStage(TwoStep ? CalibrationStage.Reach : CalibrationStage.Shoulder);
        }

        /// <summary>다음 단계. 2단계: 정면 뻗기 → 가드 → 끝. 3단계: 어깨 → 가드 → 도달 → 끝.</summary>
        CalibrationStage NextStage(CalibrationStage stage)
        {
            if (!TwoStep) return stage + 1;
            return stage == CalibrationStage.Reach ? CalibrationStage.Guard : CalibrationStage.Complete;
        }

        void Update()
        {
            if (follow.HasReference) follow.Update(FollowPoint(), Time.deltaTime, bodyFollow);
            if (input == null || Stage == CalibrationStage.NotStarted || Stage == CalibrationStage.Complete) return;

            var allDone = true;
            for (var h = 0; h < 2; h++)
            {
                var hand = (PilotSide)h;
                if (!input.IsArmHand(hand)) continue;
                if (!captured[h]) TryCaptureOnGrip(hand);
                allDone &= captured[h];
            }
            if (!allDone) return;
            if (TwoStep && Stage == CalibrationStage.Reach) FinishForward();
            EnterStage(NextStage(Stage));
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
            if (TwoStep)
            {
                error = Stage == CalibrationStage.Reach ? CaptureForward(hand, sample.Position, sample.Rotation)
                    : Stage == CalibrationStage.Guard ? arm.CaptureGuardAfterReach(CompensateHand(sample.Position), sample.Rotation)
                    : CalibrationError.InvalidPose;
            }
            else switch (Stage)
            {
                case CalibrationStage.Shoulder:
                    error = arm.CaptureShoulder(HeadPosition(), sample.Position);
                    if (error == CalibrationError.None && head != null)
                    {
                        neckAtShoulder = followNeck ? NeckPosition() : HeadPosition();
                        hasHeadAtShoulder = true;
                    }
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

            var first = stage == (TwoStep ? CalibrationStage.Reach : CalibrationStage.Shoulder);
            switch (stage)
            {
                case CalibrationStage.Shoulder:
                case CalibrationStage.Guard:
                case CalibrationStage.Reach:
                    Log(first ? $"보정 시작({HandsLabel()}, {(TwoStep ? "2단계" : "3단계")}). {CurrentInstruction}" : CurrentInstruction);
                    break;
                case CalibrationStage.Complete:
                    for (var h = 0; h < 2; h++)
                    {
                        jabCount[h] = 0;
                        jabSum[h] = Vector3.zero;
                        jabChecked[h] = false;
                    }
                    if (!TwoStep)
                    {
                        AlignBodyYaw();
                        PlaceShoulders();
                        if (head != null) follow.SetReference(FollowPoint());
                    }
                    Log(head == null ? "몸 이동 보정 꺼짐: 머리(head)가 비어 있습니다. Add Pilot Input을 다시 실행하세요."
                        : $"몸 이동 보정 {(bodyFollow.enabled ? "켜짐" : "꺼짐")} (Follow Seconds {bodyFollow.followSeconds:F2}, Follow Amount {bodyFollow.followAmount:F2})");
                    for (var h = 0; h < 2; h++)
                        if (input != null && input.IsArmHand((PilotSide)h)) input.Gate((PilotSide)h).RequireRelease();
                    LogSummary();
                    Completed?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// 몸 정면을 정한다. Head: 도달 단계 때 머리 정면(1인 양팔이면 두 손 저장 때의 평균).
        /// Hands: 1인 양팔은 두 도달 손을 잇는 선에 수직(1차 헤드셋 확인: 손마다 잰 값이 왼손 +16°, 오른손 −16°로 벌어짐), 한 팔은 손마다 잰 값.
        /// </summary>
        void AlignBodyYaw()
        {
            if (input == null) return;
            var both = input.ControlsBothArms && arms[0].IsComplete && arms[1].IsComplete;
            var handLine = both ? ArmCalibration.BodyYawFromHands(arms[0].ReachPoint, arms[1].ReachPoint) : float.NaN;
            if (bodyYawSource == BodyYawSource.Head && head != null)
            {
                var yaw = both ? ArmCalibration.MeanYaw(headYawAtReach[0], headYawAtReach[1]) : float.NaN;
                for (var h = 0; h < 2; h++)
                {
                    if (!arms[h].IsComplete || !input.IsArmHand((PilotSide)h)) continue;
                    var armYaw = both ? yaw : headYawAtReach[h];
                    Log($"{(PilotSide)h} 몸 정면 {armYaw:F0}° = 머리 정면 (손으로 잰 값 {arms[h].BodyYawDegrees:F0}°" +
                        (both && !float.IsNaN(handLine) ? $", 두 손 선 {handLine:F0}°)" : ")"));
                    arms[h].SetBodyYaw(armYaw);
                }
                return;
            }
            if (!both) return;
            if (float.IsNaN(handLine))
            {
                Log("두 손이 너무 가까워 공통 몸 정면을 정하지 못했습니다. 손마다 잰 정면을 그대로 씁니다.");
                return;
            }
            Log($"공통 몸 정면 {handLine:F0}° (손마다 잰 값: 왼손 {arms[0].BodyYawDegrees:F0}°, 오른손 {arms[1].BodyYawDegrees:F0}°)");
            arms[0].SetBodyYaw(handLine);
            arms[1].SetBodyYaw(handLine);
        }

        /// <summary>몸 정면이 정해진 뒤 어깨를 옮긴다(<see cref="ShoulderPlacement"/>).</summary>
        void PlaceShoulders()
        {
            if (input == null || !hasHeadAtShoulder || shoulderPlacement == ShoulderPlacement.Hanging) return;
            if (shoulderPlacement == ShoulderPlacement.MirrorHead)
            {
                if (!input.ControlsBothArms || !arms[0].IsComplete || !arms[1].IsComplete) return;
                var (left, right) = ArmCalibration.SymmetricShoulders(arms[0].Shoulder, arms[1].Shoulder, neckAtShoulder, arms[0].BodyYawDegrees);
                LogShoulderMove(PilotSide.Left, left);
                LogShoulderMove(PilotSide.Right, right);
                arms[0].MoveShoulder(left);
                arms[1].MoveShoulder(right);
                return;
            }
            for (var h = 0; h < 2; h++)
            {
                var arm = arms[h];
                if (!arm.IsComplete || !input.IsArmHand((PilotSide)h)) continue;
                var shoulder = ArmCalibration.ShoulderFromReach(neckAtShoulder, arm.Shoulder.y, arm.ReachPoint, arm.BodyYawDegrees,
                    h == (int)PilotSide.Right, shoulderHalfWidthRange.x, shoulderHalfWidthRange.y);
                LogShoulderMove((PilotSide)h, shoulder);
                arm.MoveShoulder(shoulder);
            }
        }

        void LogShoulderMove(PilotSide hand, Vector3 shoulder)
        {
            var arm = arms[(int)hand];
            var lateral = Vector3.Dot(shoulder - neckAtShoulder, arm.BodyRotation * Vector3.right);
            Log($"{hand} 어깨 옮김 {(shoulder - arm.Shoulder).magnitude * 100f:F0}cm ({shoulderPlacement}): 목에서 좌우 {lateral * 100f:F0}cm");
        }

        /// <summary>몸 이동 보정이 따라가는 점: 목(기본) 또는 헤드셋(눈).</summary>
        Vector3 FollowPoint() => TwoStep || followNeck ? NeckPosition() : HeadPosition();

        /// <summary>
        /// 2단계 1/2 정면 뻗기, 손 하나. 고개를 숙이거나 들었으면 거절한다. 그 순간의 목·헤드셋 정면으로 먼저 저장하고(잘못된 자세는 여기서 거절),
        /// 두 손이 다 저장되면 <see cref="FinishForward"/>가 공통 목·정면으로 다시 계산한다.
        /// </summary>
        CalibrationError CaptureForward(PilotSide hand, Vector3 position, Quaternion rotation)
        {
            if (head == null || !TryGetHeadRotation(out var headRotation)) return CalibrationError.InvalidPose;
            if (Mathf.Abs(ArmCalibration.HeadPitchDegrees(headRotation * Vector3.forward)) > settings.maxHeadPitch) return CalibrationError.LookForward;
            var h = (int)hand;
            var neck = NeckPosition();
            var yaw = HeadYaw();
            var error = arms[h].CaptureForward(neck, yaw, position, rotation, hand == PilotSide.Right);
            if (error != CalibrationError.None) return error;
            forwardHand[h] = position;
            forwardRotation[h] = rotation;
            forwardNeck[h] = neck;
            forwardYaw[h] = yaw;
            headYawAtReach[h] = yaw;
            return CalibrationError.None;
        }

        /// <summary>
        /// 정면 뻗기가 다 저장된 뒤: 1인 양팔이면 두 손을 공통 목·정면으로 다시 계산하고, 그 목을 몸 이동 보정 기준으로 잡는다.
        /// 이후 가드와 플레이 중 손은 모두 이 목 기준으로 잰다(걸어 옮기거나 고개를 돌려도 같은 값).
        /// </summary>
        void FinishForward()
        {
            var both = input.ControlsBothArms && arms[0].HasReach && arms[1].HasReach;
            var reference = forwardNeck[input.IsArmHand(PilotSide.Left) ? 0 : 1];
            if (both)
            {
                var (neck, yaw) = ArmCalibration.CommonReference(forwardNeck[0], forwardYaw[0], forwardNeck[1], forwardYaw[1]);
                reference = neck;
                for (var h = 0; h < 2; h++)
                {
                    var hand = forwardHand[h] - (forwardNeck[h] - neck);
                    var error = arms[h].CaptureForward(neck, yaw, hand, forwardRotation[h], h == (int)PilotSide.Right);
                    if (error != CalibrationError.None)
                    {
                        Log($"{(PilotSide)h} 공통 기준으로 다시 계산하지 못해 그 손 값 그대로 씁니다: {Explain(error)}");
                        arms[h].CaptureForward(forwardNeck[h], forwardYaw[h], forwardHand[h], forwardRotation[h], h == (int)PilotSide.Right);
                    }
                }
                var heightGap = Mathf.Abs(forwardHand[0].y - forwardHand[1].y);
                Log($"공통 몸 정면 {yaw:F0}° (손마다 {forwardYaw[0]:F0}° / {forwardYaw[1]:F0}°), 두 손 높이 차 {heightGap * 100f:F0}cm" +
                    (heightGap > 0.15f ? " (차이가 커요. 두 손을 같은 높이로 뻗었는지 확인)" : ""));
            }
            if (head != null) follow.SetReference(reference);
            for (var h = 0; h < 2; h++)
            {
                var arm = arms[h];
                if (!arm.HasReach || !input.IsArmHand((PilotSide)h)) continue;
                var local = Quaternion.Inverse(arm.BodyRotation) * (arm.Shoulder - reference);
                Log($"{(PilotSide)h} 어깨: 목에서 좌우 {local.x * 100f:F0}cm, 아래 {-local.y * 100f:F0}cm, 팔 길이 {arm.EffectiveReach:F2} m");
            }
        }

        /// <summary>
        /// 정면 잽 확인(선우 10/28): 보정 뒤 처음 잽 몇 번의 가장 멀리 뻗은 점(몸 기준, 사람 어깨 원점)을 받아,
        /// 평균이 정면에서 조금 벗어나 있으면 어깨 좌우를 그만큼 옮겨 잽이 곧게 나가게 한다. 펀치 분류기가 잽마다 부른다.
        /// </summary>
        public void ReportJab(PilotSide hand, Vector3 peakBody)
        {
            var h = (int)hand;
            if (!jabCheck || !IsComplete || jabChecked[h] || !ArmCalibration.Finite(peakBody)) return;
            var arm = arms[h];
            if (peakBody.z < arm.EffectiveReach * 0.6f) return;
            jabSum[h] += peakBody;
            if (++jabCount[h] < Mathf.Max(1, jabCheckCount)) return;
            jabChecked[h] = true;
            var mean = jabSum[h] / jabCount[h];
            var degrees = Mathf.Atan2(mean.x, mean.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(degrees) > jabCheckMaxDegrees)
            {
                Log($"{hand} 정면 잽 확인: 평균 {degrees:F0}° 틀어져 있어 자동으로 맞추지 않았어요. 다시 보정해 보세요.");
                return;
            }
            var shift = Mathf.Clamp(mean.x, -jabCheckMaxShift, jabCheckMaxShift);
            arm.MoveShoulder(arm.Shoulder + arm.BodyRotation * new Vector3(shift, 0f, 0f));
            Log($"{hand} 정면 잽 확인: 평균 {degrees:F1}° → 어깨를 {(shift >= 0f ? "오른쪽" : "왼쪽")}으로 {Mathf.Abs(shift) * 100f:F1}cm 옮김");
            Adjusted?.Invoke();
        }

        /// <summary>목 위치(트래킹 공간). 머리 회전 중심이라 고개를 돌려도 거의 움직이지 않는다.</summary>
        Vector3 NeckPosition()
        {
            var eye = HeadPosition();
            return TryGetHeadRotation(out var rotation) ? BodyFollow.NeckPoint(eye, rotation, eyeToNeck) : eye;
        }

        /// <summary>인게임 조절 메뉴: 보정 방식(선우 10/28). 순서를 바꾸면 다시 보정해야 적용된다.</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "보정";
            list.Describe(g, "할 것: 다시 보정 → 정면 잽 3번 → 좌우로 한 걸음 옮겨 잽 → 고개 돌리고 가드\n볼 것: 잽이 곧게 나감, 옮기거나 고개 돌려도 팔이 안 밀림");
            list.AddChoice(g, "calib.steps", "보정 순서(다시 보정)", () => (int)steps, v =>
            {
                if ((int)steps == v) return;
                steps = (CalibrationSteps)v;
                Restart();   // 단계 순서가 달라 진행 중인 보정은 처음부터
            }, new[] { "2단계: 정면 뻗기→가드", "3단계(이전)" });
            list.AddToggle(g, "calib.jabCheck", "보정 뒤 정면 잽으로 맞춤", () => jabCheck, v => jabCheck = v);
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
                case CalibrationStage.Reach when TwoStep:
                    return $"팔 길이 {arm.EffectiveReach:F2} m, 헤드셋 정면 {arm.BodyYawDegrees:F0}°";
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
                case CalibrationError.ReachWrongSide: return "손을 몸 가운데를 넘기지 말고 자기 어깨 앞으로 뻗어 주세요.";
                case CalibrationError.ReachNotLevel: return "손을 어깨 높이로 뻗어 주세요.";
                case CalibrationError.LookForward: return "고개를 숙이거나 들지 말고 정면을 봐 주세요.";
                case CalibrationError.ReachMissing: return "정면 뻗기를 먼저 저장해야 합니다.";
                default: return error.ToString();
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void Log(string message)
        {
            Debug.Log($"[PilotCalibration] {message}", this);
            Notice?.Invoke(message);
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
