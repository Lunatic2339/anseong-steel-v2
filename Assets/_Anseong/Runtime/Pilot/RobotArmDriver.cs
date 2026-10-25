using System;
using System.Diagnostics;
using Anseong.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Anseong.Pilot
{
    /// <summary>
    /// 조종사 손 하나 → 로봇 팔 하나. Animator가 대기 클립을 평가한 뒤 LateUpdate에서 위팔·아래팔·손 회전을 IK로 덮어쓰고
    /// 손가락을 주먹 포즈로 고정한다. 보정이 끝나기 전에는 팔을 건드리지 않는다(대기 클립 그대로, 손가락만 주먹).
    /// 1인 양팔 모드에서는 왼손 → 왼팔, 오른손 → 오른팔. 뼈 참조·주먹 포즈는 Build Player Robot Prefab 메뉴가 채운다.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class RobotArmDriver : MonoBehaviour, ITunable
    {
        /// <summary>Animator 평가 뒤, 조종석(CockpitSeat) 전.</summary>
        public const int ExecutionOrder = 100;

        [Header("로봇")]
        [SerializeField, Tooltip("이 드라이버가 움직일 로봇 팔 쪽.")]
        PilotSide side = PilotSide.Left;

        [SerializeField, Tooltip("로봇 정면 기준(배율 1 루트, 정면 = +Z).")]
        Transform robotRoot;

        [SerializeField, Tooltip("위팔 뼈(mixamorig:LeftArm / RightArm). 쇄골 뼈가 없으므로 여기서부터 푼다.")]
        Transform upperArm;

        [SerializeField, Tooltip("아래팔 뼈(mixamorig:LeftForeArm / RightForeArm).")]
        Transform foreArm;

        [SerializeField, Tooltip("손 뼈(mixamorig:LeftHand / RightHand).")]
        Transform hand;

        [SerializeField, Tooltip("손 뼈 로컬 기준 주먹 정면(손목 → 손가락 뿌리). 메뉴가 손가락 위치로 잰다.")]
        Vector3 fistForwardLocal = Vector3.up;

        [SerializeField, Tooltip("손 뼈 로컬 기준 손등 방향. 메뉴가 손가락 위치로 잰다.")]
        Vector3 fistBackLocal = Vector3.forward;

        [SerializeField, Tooltip("주먹으로 고정할 손가락 뼈. 메뉴가 채운다.")]
        Transform[] fingerJoints = new Transform[0];

        [SerializeField, Tooltip("fingerJoints 각각의 주먹 로컬 회전. 메뉴가 채운다.")]
        Quaternion[] fingerFistRotations = new Quaternion[0];

        [Header("입력")]
        [SerializeField, Tooltip("손 입력(XR Origin의 PilotHandInput). Add Pilot Input 메뉴가 연결한다.")]
        PilotHandInput input;

        [SerializeField, Tooltip("개인 보정(XR Origin의 PilotCalibrationFlow). Add Pilot Input 메뉴가 연결한다.")]
        PilotCalibrationFlow calibration;

        [SerializeField, Tooltip("허리 회전(같은 오브젝트의 TorsoTwistDriver). 비어 있으면 시작할 때 같은 오브젝트에서 찾는다. 없으면 허리 보정 없이 매핑한다.")]
        TorsoTwistDriver torso;

        [Header("매핑")]
        [SerializeField, Tooltip("Linear = 로봇 어깨 + (손 − 사람 어깨) × (로봇 팔 ÷ 개인 팔), 사람 가드가 그대로 커진다(기본). " +
            "GuardMatched = 사람 가드를 아래 로봇 가드 위치로 맞추고 뻗을수록 로봇 정면으로 섞는다.")]
        ArmMappingMode mappingMode = ArmMappingMode.Linear;

        [SerializeField, Tooltip("어깨 높이 보정(m, 사람 크기). 사람 어깨 기준점을 이만큼 올린다. 양수면 로봇 주먹이 내려가고 음수면 올라간다. Linear에서만 쓴다.")]
        float shoulderHeightOffset;

        [SerializeField, Tooltip("GuardMatched의 로봇 가드 주먹 위치. 로봇 팔 길이 배수, 로봇 어깨 원점. X = 몸 안쪽(+), Y = 위, Z = 앞.")]
        Vector3 robotGuardRatio = new Vector3(0.25f, 0.1f, 0.55f);

        [SerializeField, Tooltip("손 궤적으로 팔꿈치를 들어 올리는 힌트(훅에서 팔꿈치가 옆으로 올라감).")]
        bool useElbowHint = true;

        [Header("팔 반응")]
        [SerializeField, Min(0f), Tooltip("주먹 목표가 손을 따라가는 시간(초). 0 = 즉시(기본). 키우면 묵직하게 늦게 따라온다. 카메라와는 무관.")]
        float responseSeconds;

        [SerializeField, Min(0f), Tooltip("주먹 목표 최대 속도(m/s, 로봇 크기). 0 = 제한 없음(기본).")]
        float maxTargetSpeed;

        [SerializeField, Tooltip("GripHeld = 그립을 쥔 동안 손목을 아래팔과 일직선으로 고정하고, 놓으면 컨트롤러 각도를 다시 따라간다(기본, 선우 10/18). " +
            "Always = 그립과 상관없이 늘 고정. 주먹 비틀림은 두 경우 모두 아래팔이 돌아서 따라간다.")]
        WristLockMode wristLockMode = WristLockMode.GripHeld;

        [SerializeField, Tooltip("IK 세부 수치.")]
        ArmSolverSettings solverSettings = new ArmSolverSettings();

        TwoBoneArmSolver solver;
        readonly ElbowLiftHint elbowHint = new ElbowLiftHint();
        Vector3 filteredLocal, filteredVelocity;
        bool active;

        public PilotSide Side => side;

        /// <summary>마지막 프레임에 IK를 적용했다.</summary>
        public bool IsActive => active;

        /// <summary>마지막 풀이 결과(월드).</summary>
        public ArmPose LastPose { get; private set; }

        /// <summary>반응 지연 뒤 주먹 목표(로봇 어깨 기준·로봇 정면 기준, 팔 길이 배수).</summary>
        public Vector3 FistRatio { get; private set; }

        /// <summary>반응 지연 전 주먹 목표(사람 손 그대로, 같은 기준).</summary>
        public Vector3 TargetRatio { get; private set; }

        /// <summary>반응 지연 뒤 주먹 목표가 사람 손 목표보다 뒤처진 거리(팔 길이 배수). 0 = 따라잡음.</summary>
        public float LagRatio { get; private set; }

        /// <summary>마지막 주먹 목표(월드, 반응 지연 적용 뒤).</summary>
        public Vector3 LastTarget { get; private set; }

        /// <summary>위팔 뼈 위치(월드, 로봇 어깨).</summary>
        public Vector3 ShoulderPosition => upperArm != null ? upperArm.position : transform.position;

        /// <summary>로봇 팔 길이(m, 위팔 + 아래팔).</summary>
        public float ArmLength => solver != null ? solver.Bind.Total : 0f;

        /// <summary>로봇 정면 회전.</summary>
        public Quaternion BodyRotation => robotRoot != null ? robotRoot.rotation : transform.rotation;

        /// <summary>화면에 보이는 로봇 전완 방향(팔꿈치 → 손목, 월드, 정규화). 뼈가 없으면 0.</summary>
        public Vector3 ForearmDirection => foreArm != null && hand != null ? (hand.position - foreArm.position).normalized : Vector3.zero;

        /// <summary>
        /// 주먹 목표를 마지막에 한 번 더 옮기는 함수(M1-05 공격 보정이 보스 쪽으로 끌어당길 때). 비어 있으면 손 그대로.
        /// 받는 값·돌려주는 값은 월드 좌표 주먹 목표.
        /// </summary>
        public Func<Vector3, Vector3> TargetModifier { get; set; }

        public ArmMappingMode MappingMode
        {
            get => mappingMode;
            set => mappingMode = value;
        }

        public float ShoulderHeightOffset
        {
            get => shoulderHeightOffset;
            set => shoulderHeightOffset = value;
        }

        void Awake()
        {
            // Animator가 평가하기 전이라 뼈가 프리팹에 저장된 바인드 포즈에 있다.
            if (upperArm == null || foreArm == null || hand == null || robotRoot == null)
            {
                Debug.LogWarning($"[RobotArmDriver] {side} 팔 뼈 참조가 비었습니다. Build Player Robot Prefab 메뉴를 다시 실행하세요.", this);
                enabled = false;
                return;
            }
            var bind = ArmBind.FromPose(upperArm.position, foreArm.position, hand.position,
                upperArm.rotation, foreArm.rotation, hand.rotation, fistForwardLocal, fistBackLocal, side, robotRoot.rotation);
            if (!bind.IsValid)
            {
                Debug.LogWarning($"[RobotArmDriver] {side} 팔 뼈 길이가 0입니다.", this);
                enabled = false;
                return;
            }
            solver = new TwoBoneArmSolver(bind, solverSettings);
        }

        void Start()
        {
            // 프리팹에는 씬의 XR Origin 참조를 저장할 수 없다. Add Pilot Input 메뉴가 연결하지만,
            // 비어 있으면 시작할 때 한 번만 씬에서 찾는다(매 프레임 찾지 않는다).
            if (torso == null) torso = GetComponent<TorsoTwistDriver>();
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (input == null || calibration == null)
                Debug.LogWarning($"[RobotArmDriver] {side} 팔: 씬에서 PilotHandInput·PilotCalibrationFlow를 찾지 못해 팔을 움직이지 않습니다. " +
                                 "XR Origin에 'Anseong → Setup → Add Pilot Input'을 실행하세요.", this);
        }

        void LateUpdate()
        {
            var arm = calibration != null && calibration.IsComplete ? calibration.Get(side) : null;
            var drive = input != null && arm != null && input.IsArmHand(side);
            if (!drive)
            {
                active = false;
                ApplyFingers();
                return;
            }

            if (!active)
            {
                solver.Reset();
                elbowHint.Reset();
                filteredVelocity = Vector3.zero;
                LogStart(arm);
            }

            // 추적이 끊기면 샘플이 끊기기 직전 자세를 유지하므로 팔도 그 자리에 멈춘다.
            var sample = input.Get(side);
            // 플레이어가 걸어 옮긴 만큼은 빼고 잰다(가드 자세로 걸으면 팔이 그대로).
            var handPosition = calibration.CompensateHand(sample.Position);
            var dt = Time.deltaTime;
            var body = robotRoot.rotation;
            var length = solver.Bind.Total;
            Vector3 local;
            var mapped = mappingMode == ArmMappingMode.Linear
                ? ArmMapping.TryLinear(arm, handPosition, length, shoulderHeightOffset,
                    torso != null ? torso.HumanShoulderShift(side) : Vector3.zero, out local)
                : ArmMapping.TryGuardMatched(arm, handPosition, length, RobotGuardLocal(length), out local);
            if (!mapped)
            {
                active = false;
                ApplyFingers();
                return;
            }

            if (!active || responseSeconds <= 0f && maxTargetSpeed <= 0f)
            {
                filteredLocal = local;
                filteredVelocity = Vector3.zero;
            }
            else if (responseSeconds > 0f)
            {
                filteredLocal = Vector3.SmoothDamp(filteredLocal, local, ref filteredVelocity, responseSeconds,
                    maxTargetSpeed > 0f ? maxTargetSpeed : Mathf.Infinity, dt);
            }
            else
            {
                filteredLocal = Vector3.MoveTowards(filteredLocal, local, maxTargetSpeed * dt);
            }

            FistRatio = length > 0f ? filteredLocal / length : Vector3.zero;
            TargetRatio = length > 0f ? local / length : Vector3.zero;
            LagRatio = length > 0f ? (local - filteredLocal).magnitude / length : 0f;

            // 목표는 로봇 정면 기준으로 저장하므로 로봇이 돌거나 움직여도 손이 따로 움직인 것으로 보지 않는다.
            var shoulder = upperArm.position;
            LastTarget = shoulder + body * filteredLocal;
            if (TargetModifier != null) LastTarget = TargetModifier(LastTarget);
            var fist = body * ArmMapping.FistLocal(arm, sample.Rotation);
            var lift = useElbowHint ? elbowHint.Update(ArmMapping.GuardDelta(arm, handPosition), dt) : 0f;
            var pose = solver.Solve(shoulder, LastTarget, fist, body, wristLockMode == WristLockMode.Always || sample.GripHeld ? 1f : 0f, lift, dt);

            upperArm.rotation = pose.Upper;
            foreArm.rotation = pose.Lower;
            hand.rotation = pose.Hand;
            LastPose = pose;
            active = true;
            ApplyFingers();
        }

        Vector3 RobotGuardLocal(float length)
        {
            // 왼팔은 몸 안쪽이 +X, 오른팔은 −X.
            var inward = side == PilotSide.Left ? 1f : -1f;
            return new Vector3(robotGuardRatio.x * inward, robotGuardRatio.y, robotGuardRatio.z) * length;
        }

        void ApplyFingers()
        {
            if (fingerJoints == null || fingerFistRotations == null || fingerJoints.Length != fingerFistRotations.Length) return;
            for (var i = 0; i < fingerJoints.Length; i++)
                if (fingerJoints[i] != null) fingerJoints[i].localRotation = fingerFistRotations[i];
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void LogStart(ArmCalibration arm)
        {
            Debug.Log($"[RobotArmDriver] {side} 팔 IK 시작: 로봇 팔 {solver.Bind.Total:F1} m(위팔 {solver.Bind.UpperLength:F1}, 아래팔 {solver.Bind.LowerLength:F1}), " +
                $"개인 팔 {arm.EffectiveReach:F2} m, 배율 {solver.Bind.Total / arm.EffectiveReach:F1}배, 매핑 {mappingMode}, 어깨 높이 보정 {shoulderHeightOffset:F2} m", this);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!active) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(LastTarget, 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(upperArm.position, LastPose.Elbow);
            Gizmos.DrawLine(LastPose.Elbow, LastPose.Wrist);
        }
#endif

        /// <summary>인게임 조절 메뉴. 왼팔·오른팔이 같은 키로 등록해 한 번에 같이 바뀐다.</summary>
        public void AddTuning(TuningList list)
        {
            list.Describe("묵직함", "할 것: 팔 반응 시간 0 → 0.15 → 0.3마다 잽·훅 10번\n       그다음 주먹 최대 속도 30 → 20\n볼 것: 손보다 묵직하게 늦되 답답하지 않게, 시점은 그대로");
            list.AddCheck("묵직함", "arm.heavy", "육중한 로봇 느낌 값 정함");
            list.AddCheck("묵직함", "arm.camera", "시점은 안 늦음(멀미 없음)");
            list.AddCheck("묵직함", "arm.still", "가만히 있으면 로봇 팔도 가만히");
            list.AddCheck("묵직함", "arm.hitTiming", "판정이 로봇 주먹이 도착할 때 남");
            list.AddFloat("묵직함", "arm.responseSeconds", "팔 반응 시간(초, 0 = 즉시)", () => responseSeconds, v => responseSeconds = v, 0f, 0.6f, 0.05f);
            list.AddFloat("묵직함", "arm.maxTargetSpeed", "주먹 최대 속도(m/s 로봇, 0 = 제한 없음)", () => maxTargetSpeed, v => maxTargetSpeed = v, 0f, 80f, 5f, 0);
            list.Describe("손목", "할 것: 그립 쥐고·놓고 손목 돌려 보기\n볼 것: 쥐면 일자, 놓으면 컨트롤러 각도, 흐느적 없음");
            list.AddCheck("손목", "arm.wristGrip", "쥐면 손목 일자");
            list.AddCheck("손목", "arm.wristFree", "놓으면 컨트롤러 각도로");
            list.AddChoice("손목", "arm.wristLockMode", "손목 고정", () => (int)wristLockMode, v => wristLockMode = (WristLockMode)v, new[] { "항상", "그립 쥘 때만" });
            if (solverSettings != null)
                list.AddFloat("손목", "arm.lockedWristPitch", "고정 손목 위아래 꺾임(도)", () => solverSettings.lockedWristPitchDegrees, v => solverSettings.lockedWristPitchDegrees = v, -30f, 30f, 2f, 0);
            list.Describe("팔 매핑", "할 것: 가드 자세와 끝까지 뻗기, 매핑 방식 바꿔 비교\n볼 것: 가드가 시야를 안 가리고, 뻗으면 끝까지");
            list.AddCheck("팔 매핑", "arm.guardPose", "가드 자세가 자연스러움");
            list.AddCheck("팔 매핑", "arm.reach", "뻗으면 로봇 팔도 끝까지");
            list.AddChoice("팔 매핑", "arm.mappingMode", "매핑 방식", () => (int)mappingMode, v => mappingMode = (ArmMappingMode)v, new[] { "Linear(기본)", "가드 맞춤" });
            list.AddFloat("팔 매핑", "arm.shoulderHeightOffset", "어깨 높이 보정(m, +면 주먹 내려감)", () => shoulderHeightOffset, v => shoulderHeightOffset = v, -0.3f, 0.3f, 0.02f);
        }
    }
}
