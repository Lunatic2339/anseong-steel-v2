using System;
using Anseong.Core;
using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>바인드 포즈에서 잰 팔 기준값. 로봇 전체 회전과 무관하다.</summary>
    public struct ArmBind
    {
        public float UpperLength;
        public float LowerLength;
        /// <summary>LookRotation(위팔 방향, 굽힘면 법선) → 위팔 뼈 회전.</summary>
        public Quaternion UpperFrame;
        /// <summary>LookRotation(아래팔 방향, 굽힘면 법선) → 아래팔 뼈 회전.</summary>
        public Quaternion LowerFrame;
        /// <summary>아래팔 기준 손 회전. 손바닥 앞쪽이 아래팔과 일직선.</summary>
        public Quaternion LockedWrist;
        /// <summary>손 뼈 기준 주먹 정면(손목 → 손가락 뿌리).</summary>
        public Vector3 FistForwardLocal;
        /// <summary>손 뼈 기준 손등 방향.</summary>
        public Vector3 FistBackLocal;
        /// <summary>왼팔 −1, 오른팔 +1. 팔꿈치가 바깥으로 빠지는 쪽.</summary>
        public float SideSign;

        public float Total => UpperLength + LowerLength;
        public bool IsValid => UpperLength > 1e-4f && LowerLength > 1e-4f;

        /// <summary>
        /// 바인드 포즈 월드 자세로 기준값을 만든다. body는 로봇 정면 회전(굽힘면이 정해지지 않을 때 기준).
        /// fistForwardLocal·fistBackLocal은 손 뼈 로컬 축.
        /// </summary>
        public static ArmBind FromPose(Vector3 shoulder, Vector3 elbow, Vector3 wrist,
            Quaternion upperRotation, Quaternion lowerRotation, Quaternion handRotation,
            Vector3 fistForwardLocal, Vector3 fistBackLocal, PilotSide side, Quaternion body)
        {
            var bind = new ArmBind
            {
                UpperLength = Vector3.Distance(shoulder, elbow),
                LowerLength = Vector3.Distance(elbow, wrist),
                SideSign = side == PilotSide.Left ? -1f : 1f,
            };
            var a = (elbow - shoulder).normalized;
            var b = (wrist - elbow).normalized;
            var normal = Vector3.Cross(a, b);
            if (normal.sqrMagnitude < .001f)
                normal = Vector3.Cross(body * new Vector3(bind.SideSign, -.5f, -.2f), a);
            normal = TwoBoneArmSolver.Perpendicular(a, normal, body * Vector3.up);
            bind.UpperFrame = Quaternion.Inverse(Quaternion.LookRotation(a, normal)) * upperRotation;
            bind.LowerFrame = Quaternion.Inverse(Quaternion.LookRotation(b, TwoBoneArmSolver.Perpendicular(b, normal, body * Vector3.up))) * lowerRotation;

            var forward = fistForwardLocal.normalized;
            bind.FistForwardLocal = forward;
            bind.FistBackLocal = Vector3.ProjectOnPlane(fistBackLocal, forward).normalized;
            // 저장된 자세의 손목이 꺾여 있어도 손바닥 길이 축을 아래팔과 맞춘 회전을 기준으로 쓴다.
            var handLocal = Quaternion.Inverse(lowerRotation) * handRotation;
            var forearmLocal = Quaternion.Inverse(lowerRotation) * b;
            bind.LockedWrist = Quaternion.FromToRotation(handLocal * forward, forearmLocal) * handLocal;
            return bind;
        }
    }

    /// <summary>한 프레임의 풀이 결과. 회전은 뼈의 월드 회전.</summary>
    public struct ArmPose
    {
        public Vector3 Elbow;
        public Vector3 Wrist;
        public Quaternion Upper;
        public Quaternion Lower;
        public Quaternion Hand;
        /// <summary>어깨–손목 거리 ÷ 팔 길이(완화 뒤).</summary>
        public float Extension;
        /// <summary>목표가 팔 안쪽 최소 거리보다 가깝다.</summary>
        public bool InsideMinimum;
        /// <summary>팔꿈치 swivel(도). ±maxSwivel 안.</summary>
        public float Swivel;
    }

    [Serializable]
    public class ArmSolverSettings
    {
        [Tooltip("팔꿈치가 가장 많이 접히는 각도(도). 이보다 가까운 목표는 내부구 경계에 둔다.")]
        [Range(110f, 175f)]
        public float maxBendDegrees = 160f;

        [Tooltip("팔꿈치가 이만큼(도)만 굽은 거리부터 더 먼 목표를 지수형으로 완화한다. 매핑이 끝까지 뻗은 손을 3° 굽힘에 두므로 기본은 거기서 시작.")]
        [Range(1f, 40f)]
        public float softReachStartBendDegrees = 3f;

        [Tooltip("손이 팔 길이 밖으로 나가도 남기는 팔꿈치 굽힘(도). 0이면 일직선(특이점).")]
        [Range(0.5f, 10f)]
        public float fullReachBendDegrees = 2f;

        [Tooltip("팔꿈치 swivel 한계(도). 반대편 팔꿈치로 넘어가지 않게 막는다.")]
        [Range(60f, 150f)]
        public float maxSwivelDegrees = 105f;

        [Tooltip("swivel 한계 양 끝이 이만큼(도) 비슷하면 직전 쪽을 유지한다(히스테리시스).")]
        public float swivelHysteresisDegrees = 20f;

        [Tooltip("어깨→손 방향이 1초에 돌 수 있는 최대 각도(도/초). 어깨 근처를 지날 때 휙 뒤집힘을 막는다.")]
        public float axisTurnSpeed = 720f;

        [Tooltip("팔꿈치 swivel 변화 속도(도/초).")]
        public float swivelSpeed = 180f;

        [Tooltip("굽힘면 기준 방향 변화 속도(도/초).")]
        public float poleTurnSpeed = 90f;

        [Tooltip("아래팔 비틀림 변화 속도(도/초).")]
        public float rollSpeed = 180f;

        [Tooltip("아래팔 비틀림 한계(도).")]
        [Range(90f, 179f)]
        public float maxRollDegrees = 165f;

        [Tooltip("그립을 놓았을 때 손목이 꺾일 수 있는 각도(도).")]
        [Range(0f, 90f)]
        public float freeWristSwingDegrees = 60f;

        [Tooltip("그립을 놓았을 때 손목 비틀림 한계(도).")]
        [Range(0f, 90f)]
        public float freeWristRollDegrees = 30f;

        [Tooltip("손목 고정 ↔ 풀림 전환 속도(1/초).")]
        public float wristLockSpeed = 6f;
    }

    /// <summary>
    /// 해석적 2본 팔 IK. 기존 레포 ControllerArmFollower.Practice.cs 방식을 Transform 없이 다시 썼다.
    /// 매 프레임 바인드 포즈 기준으로 뼈 회전을 절대 재구성하므로 roll이 누적되지 않는다.
    /// 쇄골 뼈 없이 상완부터 푼다. 월드 좌표를 받고 월드 회전을 돌려준다.
    /// </summary>
    public sealed class TwoBoneArmSolver
    {
        readonly ArmBind bind;
        readonly ArmSolverSettings settings;

        // 로봇 정면 기준(body 로컬) 방향. 몸이 돌아도 같이 돈다.
        Vector3 axisLocal, poleLocal, backLocal, lowerAxisLocal, positionPoleLocal;
        float roll, swivel, wristAlignment;
        bool solved;

        public TwoBoneArmSolver(ArmBind bind, ArmSolverSettings settings = null)
        {
            this.bind = bind;
            this.settings = settings ?? new ArmSolverSettings();
            Reset();
        }

        public ArmBind Bind => bind;
        public ArmSolverSettings Settings => settings;

        /// <summary>이 거리(m)보다 가까운 목표는 내부구 경계에 둔다.</summary>
        public float MinimumReach => Mathf.Max(Mathf.Abs(bind.UpperLength - bind.LowerLength) + bind.Total * .002f,
            ArmCalibration.DistanceForBend(bind.UpperLength, bind.LowerLength, settings.maxBendDegrees));

        /// <summary>완화가 수렴하는 최대 거리(m).</summary>
        public float MaximumReach => ArmCalibration.DistanceForBend(bind.UpperLength, bind.LowerLength, settings.fullReachBendDegrees);

        /// <summary>이 거리(m)까지는 목표를 그대로 따른다.</summary>
        public float SoftReachStart => ArmCalibration.DistanceForBend(bind.UpperLength, bind.LowerLength,
            Mathf.Max(settings.softReachStartBendDegrees, settings.fullReachBendDegrees));

        public void Reset()
        {
            solved = false;
            roll = swivel = 0f;
            wristAlignment = 1f;
        }

        /// <summary>
        /// shoulder: 위팔 뼈 위치. target: 손목 목표. fist: 원하는 주먹 회전(정면 = +Z, 손등 = +Y).
        /// body: 로봇 정면 회전. wristLock: 1 = 손목을 아래팔에 고정(그립 중), 0 = 제한된 꺾임 허용.
        /// elbowLift: 0 = 팔꿈치를 옆구리에 붙임, 1 = 들어 올림(훅). dt: 초.
        /// </summary>
        public ArmPose Solve(Vector3 shoulder, Vector3 target, Quaternion fist, Quaternion body,
            float wristLock, float elbowLift, float dt)
        {
            dt = Mathf.Max(0f, dt);
            float total = bind.Total;
            var up = body * Vector3.up;
            var offset = target - shoulder;
            var lastAxis = body * axisLocal;
            var lastPole = body * poleLocal;
            var lastBack = body * backLocal;

            var minimum = MinimumReach;
            var inside = offset.magnitude < minimum;
            float innerBand = minimum + total * .04f;
            var axis = offset.sqrMagnitude > total * total * 1e-10f ? offset.normalized : (solved ? lastAxis : body * Vector3.forward);
            if (solved)
            {
                // 길이가 다른 두 뼈는 안쪽 구를 지날 수 없다. 띠 안에서는 직전 방향을 유지하고, 밖에서는 제한된 속도로 돈다.
                if (offset.magnitude < innerBand) axis = lastAxis;
                else axis = RotateDirection(lastAxis, axis, lastPole, settings.axisTurnSpeed * dt);
            }

            float limitRatio = MaximumReach / total;
            float onset = Mathf.Min(SoftReachStart / total, limitRatio - 0.00001f);
            float distance = MechaArmStability.SoftDistance(offset.magnitude, total, onset, limitRatio);
            distance = Mathf.Clamp(distance, minimum, total * (1f - .00001f));
            var goal = shoulder + axis * distance;

            // 팔꿈치 기본 방향: 바깥 아래. 뻗거나 들어 올리면 옆으로.
            float extension = Mathf.InverseLerp(.65f, .98f, distance / total);
            float raised = Mathf.Max(extension, Mathf.Clamp01(elbowLift));
            var preferred = body * new Vector3(bind.SideSign, Mathf.Lerp(-.65f, 0f, raised), Mathf.Lerp(-.1f, 0f, raised));
            var transported = solved ? Transport(lastAxis, axis, body * positionPoleLocal) : preferred;
            transported = Perpendicular(axis, transported, up);
            var positionPole = Vector3.ProjectOnPlane(preferred, axis);
            if (positionPole.sqrMagnitude < .04f) positionPole = transported;
            else positionPole.Normalize();
            if (solved) positionPole = RotateDirection(transported, positionPole, axis, settings.poleTurnSpeed * dt);

            float along = (bind.UpperLength * bind.UpperLength - bind.LowerLength * bind.LowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, bind.UpperLength * bind.UpperLength - along * along));

            // 팔꿈치 원 위에서 원하는 아래팔 방향 f에 가장 가까운 점은 −project(f, axis) 쪽이다.
            var wantedForward = fist * Vector3.forward;
            var wantedBack = fist * Vector3.up;
            var orientationPole = -Vector3.ProjectOnPlane(wantedForward, axis);
            float gripWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.05f, .3f, orientationPole.magnitude))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.015f, .10f, height / total))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, minimum + total * .12f, distance));
            float targetSwivel = orientationPole.sqrMagnitude > .0001f
                ? BoundSwivel(Vector3.SignedAngle(positionPole, orientationPole.normalized, axis), swivel,
                    settings.maxSwivelDegrees, settings.swivelHysteresisDegrees) * gripWeight
                : 0f;
            // 범위가 정해진 스칼라라서 한 바퀴씩 쌓이거나 반대편 팔꿈치로 튀지 않는다.
            swivel = solved ? Mathf.MoveTowards(swivel, targetSwivel, settings.swivelSpeed * dt) : targetSwivel;
            var pole = Quaternion.AngleAxis(swivel, axis) * positionPole;
            var elbow = shoulder + axis * along + pole * height;
            var plane = Vector3.Cross(pole, axis).normalized;
            var upperDirection = (elbow - shoulder).normalized;
            var lowerDirection = (goal - elbow).normalized;

            // 바인드 기준 절대 재구성. 누적 FromTo/roll을 쓰지 않는다.
            var upper = Quaternion.LookRotation(upperDirection, plane) * bind.UpperFrame;
            var lower = Quaternion.LookRotation(lowerDirection, plane) * bind.LowerFrame;

            var back = Vector3.ProjectOnPlane(up, lowerDirection);
            var carriedBack = solved ? Transport(body * lowerAxisLocal, lowerDirection, lastBack) : up;
            if (back.sqrMagnitude < .04f) back = Perpendicular(lowerDirection, carriedBack, body * Vector3.right);
            else back.Normalize();
            if (solved)
                back = Vector3.RotateTowards(Perpendicular(lowerDirection, carriedBack, back), back,
                    settings.rollSpeed * Mathf.Deg2Rad * dt, 0f).normalized;
            var handBackNow = (lower * bind.LockedWrist) * bind.FistBackLocal;
            float neutralRoll = Vector3.SignedAngle(Vector3.ProjectOnPlane(handBackNow, lowerDirection), back, lowerDirection);
            lower = Quaternion.AngleAxis(neutralRoll, lowerDirection) * lower;

            // 손등 방향은 아래팔 축을 따라 가장 작게 돌려서 맞춘다.
            var desired = Transport(wantedForward, lowerDirection, wantedBack);
            desired = Perpendicular(lowerDirection, desired, back);
            var current = Quaternion.AngleAxis(roll, lowerDirection) * back;
            float rollDelta = Vector3.SignedAngle(current, desired, lowerDirection);
            if (solved) rollDelta = Mathf.Clamp(rollDelta, -settings.rollSpeed * dt, settings.rollSpeed * dt);
            roll = Mathf.Clamp(roll + rollDelta, -settings.maxRollDegrees, settings.maxRollDegrees);
            lower = Quaternion.AngleAxis(roll, lowerDirection) * lower;

            // 그립 중에는 손목이 아래팔에 고정, 놓으면 제한된 꺾임. 놓는 순간 손이 튀지 않게 천천히 바뀐다.
            wristAlignment = solved
                ? Mathf.MoveTowards(wristAlignment, Mathf.Clamp01(wristLock), settings.wristLockSpeed * dt)
                : Mathf.Clamp01(wristLock);
            float free = 1f - wristAlignment;
            var lockedHand = lower * bind.LockedWrist;
            var handForward = Vector3.RotateTowards(lowerDirection, wantedForward, free * settings.freeWristSwingDegrees * Mathf.Deg2Rad, 0f).normalized;
            var handBack = Perpendicular(handForward, lockedHand * bind.FistBackLocal, back);
            var gripBack = Perpendicular(handForward, Transport(wantedForward, handForward, wantedBack), handBack);
            float wristRollLimit = settings.freeWristRollDegrees * free;
            float wristRoll = Mathf.Clamp(Vector3.SignedAngle(handBack, gripBack, handForward), -wristRollLimit, wristRollLimit);
            handBack = Quaternion.AngleAxis(wristRoll, handForward) * handBack;
            var hand = Quaternion.LookRotation(handForward, handBack)
                * Quaternion.Inverse(Quaternion.LookRotation(bind.FistForwardLocal, bind.FistBackLocal));

            var inverseBody = Quaternion.Inverse(body);
            axisLocal = inverseBody * axis;
            poleLocal = inverseBody * pole;
            backLocal = inverseBody * back;
            lowerAxisLocal = inverseBody * lowerDirection;
            positionPoleLocal = inverseBody * positionPole;
            solved = true;

            return new ArmPose
            {
                Elbow = elbow,
                Wrist = elbow + lowerDirection * bind.LowerLength,
                Upper = upper,
                Lower = lower,
                Hand = hand,
                Extension = distance / total,
                InsideMinimum = inside,
                Swivel = swivel,
            };
        }

        /// <summary>axis에 수직인 preferred 성분. 0이면 fallback, 그다음 고정 축 순서로 대신한다.</summary>
        public static Vector3 Perpendicular(Vector3 axis, Vector3 preferred, Vector3 fallback)
        {
            var p = Vector3.ProjectOnPlane(preferred, axis);
            if (p.sqrMagnitude < .0001f) p = Vector3.ProjectOnPlane(fallback, axis);
            if (p.sqrMagnitude < .0001f) p = Vector3.ProjectOnPlane(Vector3.right, axis);
            if (p.sqrMagnitude < .0001f) p = Vector3.ProjectOnPlane(Vector3.forward, axis);
            return p.normalized;
        }

        /// <summary>from → to로 가장 작게 돌리는 회전을 value에 적용한다(평행 이동).</summary>
        public static Vector3 Transport(Vector3 from, Vector3 to, Vector3 value)
        {
            if (Vector3.Dot(from.normalized, to.normalized) < -.999f)
                return Quaternion.AngleAxis(180f, Perpendicular(from, value, Vector3.up)) * value;
            return Quaternion.FromToRotation(from, to) * value;
        }

        /// <summary>from을 to 쪽으로 최대 maxDegrees만큼 돌린다. 정반대면 turnAxis를 축으로 돈다.</summary>
        public static Vector3 RotateDirection(Vector3 from, Vector3 to, Vector3 turnAxis, float maxDegrees)
        {
            if (Vector3.Dot(from, to) < -.999f)
                return Quaternion.AngleAxis(Mathf.Min(180f, maxDegrees), Perpendicular(from, turnAxis, Vector3.up)) * from;
            return Vector3.RotateTowards(from, to, maxDegrees * Mathf.Deg2Rad, 0f).normalized;
        }

        /// <summary>
        /// swivel 각도를 ±limit 안으로 넣는다. 한계 밖에서 두 끝이 비슷하게 가까우면 직전 쪽을 유지한다
        /// (±180 근처 작은 흔들림에 양 끝을 오가지 않게).
        /// </summary>
        public static float BoundSwivel(float angle, float previous, float limit = 105f, float hysteresis = 20f)
        {
            if (Mathf.Abs(angle) <= limit) return angle;
            float positive = Mathf.Abs(Mathf.DeltaAngle(angle, limit));
            float negative = Mathf.Abs(Mathf.DeltaAngle(angle, -limit));
            if (Mathf.Abs(positive - negative) < hysteresis && Mathf.Abs(previous) > 1f) return previous > 0f ? limit : -limit;
            return positive <= negative ? limit : -limit;
        }
    }
}
