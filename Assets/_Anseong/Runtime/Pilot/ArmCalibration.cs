using System;
using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>보정 저장이 거절된 이유.</summary>
    public enum CalibrationError
    {
        None,
        /// <summary>추적 값이 없거나 NaN·무한대.</summary>
        InvalidPose,
        /// <summary>어깨 단계: 손이 머리 아래로 팔을 내린 위치가 아니다.</summary>
        ArmNotHanging,
        /// <summary>어깨를 먼저 저장해야 한다.</summary>
        ShoulderMissing,
        /// <summary>가드: 어깨에서 너무 가깝거나 멀다.</summary>
        GuardOutOfRange,
        /// <summary>가드를 먼저 저장해야 한다.</summary>
        GuardMissing,
        /// <summary>도달: 가드보다 충분히 멀리 뻗지 않았다.</summary>
        ReachTooShort,
        /// <summary>도달: 위·아래로 뻗어서 몸 정면을 정할 수 없다.</summary>
        ReachNotForward,
    }

    /// <summary>개인 보정 수치. 클래스 기본값 = 기존 레포(backup/local-1015 ArmReachCalibration)에서 쓰던 값.</summary>
    [Serializable]
    public class ArmCalibrationSettings
    {
        [Tooltip("어깨 관절이 눈보다 얼마나 아래인지(m). 어깨 단계에서 어깨 높이 = 머리 높이 − 이 값.")]
        public float shoulderBelowEye = 0.25f;

        [Tooltip("어깨 단계: 내린 손이 어깨에서 이만큼 이상 아래여야 한다(m).")]
        public float minHangingArmLength = 0.35f;

        [Tooltip("어깨 단계: 내린 손이 어깨에서 이만큼 이하로 아래여야 한다(m).")]
        public float maxHangingArmLength = 0.95f;

        [Tooltip("어깨 단계: 내린 손과 머리의 수평 거리 한도(m). 넘으면 팔을 내린 자세가 아니다.")]
        public float maxHandFromHeadHorizontal = 0.5f;

        [Tooltip("가드: 어깨에서 손까지 최소 거리(m).")]
        public float minGuardDistance = 0.04f;

        [Tooltip("가드·도달: 어깨에서 손까지 최대 거리(m).")]
        public float maxReachDistance = 1.4f;

        [Tooltip("도달: 어깨에서 손까지 최소 거리(m).")]
        public float minReachDistance = 0.2f;

        [Tooltip("도달: 가드보다 이만큼 이상 더 뻗어야 한다(m).")]
        public float minReachBeyondGuard = 0.08f;

        [Tooltip("도달: 가드 거리 ÷ 도달 거리가 이 값 이하여야 한다.")]
        public float maxGuardToReachRatio = 0.85f;

        [Tooltip("도달: 어깨 → 손의 수평 길이가 이 값 이상이어야 몸 정면을 정한다(m).")]
        public float minReachHorizontal = 0.15f;
    }

    /// <summary>
    /// 팔 하나의 개인 보정(어깨 → 가드 → 도달)과 그 값으로 하는 도달 매핑.
    /// 모든 위치는 트래킹 공간(XR Origin의 Camera Offset 기준) 미터. 그 판 동안만 메모리에 둔다.
    /// <para>
    /// 몸 정면 yaw는 도달 단계에서 어깨 → 뻗은 손의 수평 방향으로 정한다. 머리 방향을 쓰지 않으므로
    /// 고개를 돌린 채 보정해도, 이후 고개를 돌려도 팔의 정면 기준이 돌지 않는다.
    /// </para>
    /// </summary>
    public sealed class ArmCalibration
    {
        /// <summary>완전히 뻗은 상태에 남기는 팔꿈치 굽힘(°). 로봇 팔이 완전히 펴져 IK가 튀지 않게 한다.</summary>
        public const float FullReachBendDegrees = 3f;

        /// <summary>도달 거리를 넘어 뻗어도 이 굽힘(°)까지만 펴진다.</summary>
        public const float OverreachBendDegrees = 2f;

        /// <summary>FullReachBendDegrees일 때의 (어깨–손 거리 ÷ 팔 길이). 위팔 = 아래팔일 때.</summary>
        public const float DefaultFullRatio = 0.9996573f;

        /// <summary>OverreachBendDegrees일 때의 (어깨–손 거리 ÷ 팔 길이). 위팔 = 아래팔일 때.</summary>
        public const float DefaultLimitRatio = 0.9998477f;

        readonly ArmCalibrationSettings settings;

        public ArmCalibration(ArmCalibrationSettings settings = null)
        {
            this.settings = settings ?? new ArmCalibrationSettings();
        }

        public bool HasShoulder { get; private set; }
        public bool HasGuard { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>어깨 관절 위치(추정).</summary>
        public Vector3 Shoulder { get; private set; }

        /// <summary>어깨 단계에서 잰 개인 팔 길이(어깨 → 내린 손 그립 중심, m).</summary>
        public float ArmLength { get; private set; }

        public Vector3 Guard { get; private set; }
        public Quaternion GuardRotation { get; private set; } = Quaternion.identity;

        public Vector3 ReachPoint { get; private set; }
        public Quaternion ReachRotation { get; private set; } = Quaternion.identity;

        /// <summary>도달 거리(어깨 → 편하게 끝까지 뻗은 손, m).</summary>
        public float EffectiveReach { get; private set; }

        /// <summary>몸 정면 yaw(°, 트래킹 공간 +Z = 0, +X = 90).</summary>
        public float BodyYawDegrees { get; private set; }

        public Quaternion BodyRotation => Quaternion.Euler(0f, BodyYawDegrees, 0f);

        /// <summary>가드 거리 ÷ 도달 거리.</summary>
        public float GuardRatio => IsComplete ? Vector3.Distance(Guard, Shoulder) / EffectiveReach : 0f;

        public void Reset()
        {
            HasShoulder = HasGuard = IsComplete = false;
            Shoulder = Guard = ReachPoint = Vector3.zero;
            GuardRotation = ReachRotation = Quaternion.identity;
            ArmLength = EffectiveReach = BodyYawDegrees = 0f;
        }

        /// <summary>
        /// 어깨 단계: 똑바로 서서 팔을 편하게 아래로 내린 채 저장한다.
        /// 어깨 = 내린 손의 수평 위치 + (머리 높이 − shoulderBelowEye). 이전 보정은 모두 지운다.
        /// </summary>
        public CalibrationError CaptureShoulder(Vector3 head, Vector3 hand)
        {
            if (!Finite(head) || !Finite(hand)) return CalibrationError.InvalidPose;

            var shoulder = new Vector3(hand.x, head.y - settings.shoulderBelowEye, hand.z);
            var hanging = shoulder.y - hand.y;
            var horizontal = new Vector2(hand.x - head.x, hand.z - head.z).magnitude;
            if (hanging < settings.minHangingArmLength || hanging > settings.maxHangingArmLength ||
                horizontal > settings.maxHandFromHeadHorizontal)
                return CalibrationError.ArmNotHanging;

            Reset();
            Shoulder = shoulder;
            ArmLength = hanging;
            HasShoulder = true;
            return CalibrationError.None;
        }

        /// <summary>가드 단계: 편한 가드 자세에서 저장한다. 도달 값은 지운다.</summary>
        public CalibrationError CaptureGuard(Vector3 hand, Quaternion rotation)
        {
            if (!HasShoulder) return CalibrationError.ShoulderMissing;
            if (!Finite(hand) || !ValidRotation(rotation)) return CalibrationError.InvalidPose;
            var distance = Vector3.Distance(Shoulder, hand);
            if (distance < settings.minGuardDistance || distance > settings.maxReachDistance)
                return CalibrationError.GuardOutOfRange;

            Guard = hand;
            GuardRotation = rotation.normalized;
            HasGuard = true;
            IsComplete = false;
            ReachPoint = Vector3.zero;
            ReachRotation = Quaternion.identity;
            EffectiveReach = BodyYawDegrees = 0f;
            return CalibrationError.None;
        }

        /// <summary>도달 단계: 정면으로 편하게 끝까지 뻗고 저장한다. 몸 정면 yaw도 여기서 정한다.</summary>
        public CalibrationError CaptureReach(Vector3 hand, Quaternion rotation)
        {
            if (!HasGuard) return CalibrationError.GuardMissing;
            if (!Finite(hand) || !ValidRotation(rotation)) return CalibrationError.InvalidPose;

            var reach = Vector3.Distance(hand, Shoulder);
            var guard = Vector3.Distance(Guard, Shoulder);
            if (reach < settings.minReachDistance || reach > settings.maxReachDistance ||
                reach - guard < settings.minReachBeyondGuard || guard / reach > settings.maxGuardToReachRatio)
                return CalibrationError.ReachTooShort;

            var flat = new Vector3(hand.x - Shoulder.x, 0f, hand.z - Shoulder.z);
            if (flat.magnitude < settings.minReachHorizontal) return CalibrationError.ReachNotForward;

            EffectiveReach = reach;
            ReachPoint = hand;
            ReachRotation = rotation.normalized;
            BodyYawDegrees = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            IsComplete = true;
            return CalibrationError.None;
        }

        /// <summary>
        /// 1인 양팔 모드: 두 손을 나란히 정면으로 뻗은 위치에서 몸 정면 yaw(°)를 구한다.
        /// 왼손 → 오른손 선에 수직인 방향이라 어깨 추정이 좌우로 틀려도 영향을 받지 않는다.
        /// 두 손이 너무 가까우면 NaN.
        /// </summary>
        public static float BodyYawFromHands(Vector3 leftReach, Vector3 rightReach, float minSpacing = 0.1f)
        {
            var across = new Vector3(rightReach.x - leftReach.x, 0f, rightReach.z - leftReach.z);
            if (!Finite(across) || across.magnitude < minSpacing) return float.NaN;
            var forward = Vector3.Cross(across, Vector3.up);
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 1인 양팔 모드: 두 어깨를 머리(어깨 단계 때, 똑바로 선 자세) 좌우로 대칭이 되게 옮긴 위치. 간격은 잰 두 어깨의 몸 좌우 거리 그대로.
        /// 내린 손으로 어깨를 재면 한쪽 손이 몸 안쪽·바깥쪽으로 조금만 처져도 두 팔이 같은 쪽으로 쏠린다(선우 10/28: 왼손 가드가 22cm 안쪽, 오른손은 7cm).
        /// </summary>
        public static (Vector3 left, Vector3 right) SymmetricShoulders(Vector3 left, Vector3 right, Vector3 head, float bodyYawDegrees)
        {
            var side = Quaternion.Euler(0f, bodyYawDegrees, 0f) * Vector3.right;
            var half = Mathf.Abs(Vector3.Dot(right - left, side)) * 0.5f;
            var center = new Vector3(head.x, 0f, head.z);
            var l = center - side * half;
            var r = center + side * half;
            return (new Vector3(l.x, left.y, l.z), new Vector3(r.x, right.y, r.z));
        }

        /// <summary>
        /// 어깨를 "보정 때 뻗은 손의 바로 뒤"에 둔다(선우 10/28: 오른손 잽이 왼쪽으로 꺾임, 기기별 편차).
        /// 좌우 = 도달 손의 몸 좌우 위치(목 기준, minHalf~maxHalf로 제한), 앞뒤 = 목, 높이 = shoulderY.
        /// 그래서 보정 때처럼 정면으로 뻗으면 로봇도 정면으로 뻗는다. 컨트롤러 원점 차이·어깨 폭 차이가 저절로 맞는다.
        /// </summary>
        public static Vector3 ShoulderFromReach(Vector3 neck, float shoulderY, Vector3 reach, float bodyYawDegrees, bool rightArm,
            float minHalf = 0.1f, float maxHalf = 0.3f)
        {
            var side = Quaternion.Euler(0f, bodyYawDegrees, 0f) * Vector3.right;
            var lateral = Vector3.Dot(new Vector3(reach.x - neck.x, 0f, reach.z - neck.z), side);
            lateral = rightArm ? Mathf.Clamp(lateral, minHalf, maxHalf) : Mathf.Clamp(lateral, -maxHalf, -minHalf);
            var p = new Vector3(neck.x, 0f, neck.z) + side * lateral;
            return new Vector3(p.x, shoulderY, p.z);
        }

        /// <summary>두 yaw(°)의 평균(원형). 350°와 10°의 평균은 0°.</summary>
        public static float MeanYaw(float a, float b)
        {
            var ra = a * Mathf.Deg2Rad;
            var rb = b * Mathf.Deg2Rad;
            return Mathf.Atan2(Mathf.Sin(ra) + Mathf.Sin(rb), Mathf.Cos(ra) + Mathf.Cos(rb)) * Mathf.Rad2Deg;
        }

        /// <summary>보정이 끝난 뒤 어깨 위치만 옮긴다(도달 거리는 새 어깨에서 다시 잰다). 끝나지 않았으면 false.</summary>
        public bool MoveShoulder(Vector3 shoulder)
        {
            if (!IsComplete || !Finite(shoulder)) return false;
            Shoulder = shoulder;
            EffectiveReach = Mathf.Max(0.01f, Vector3.Distance(ReachPoint, Shoulder));
            return true;
        }

        /// <summary>도달까지 끝난 보정의 몸 정면 yaw를 바꾼다(1인 양팔 모드에서 두 손 공통 정면으로 맞출 때).</summary>
        public bool SetBodyYaw(float degrees)
        {
            if (!IsComplete || float.IsNaN(degrees) || float.IsInfinity(degrees)) return false;
            BodyYawDegrees = degrees;
            return true;
        }

        /// <summary>트래킹 공간 위치 → 몸 기준(어깨 원점, +Z = 몸 정면, +Y = 위).</summary>
        public Vector3 ToBody(Vector3 trackingPoint)
        {
            return Quaternion.Inverse(BodyRotation) * (trackingPoint - Shoulder);
        }

        /// <summary>
        /// 손 위치를 로봇 팔 목표로 옮긴다. 모델 값은 로봇 정면 기준(+Z = 앞) 좌표.
        /// 가드에서는 로봇 가드로, 끝까지 뻗으면 로봇 어깨 정면(fullRatio × 팔 길이)으로 가고, 그 사이는 방향을 부드럽게 섞는다.
        /// 거리는 <see cref="MapRadius"/>로 이어 붙인다. 보정이 끝나지 않았거나 값이 잘못되면 modelGuard.
        /// </summary>
        public Vector3 Map(Vector3 hand, Vector3 modelShoulder, Vector3 modelGuard, float modelLength,
            float fullRatio = DefaultFullRatio, float limitRatio = DefaultLimitRatio)
        {
            if (!IsComplete || !Finite(hand) || modelLength <= 0.01f) return modelGuard;
            var current = ToBody(hand);
            var guard = ToBody(Guard);
            var mapping = DirectionMap(hand, modelShoulder, modelGuard);
            var radius = MapRadius(current.magnitude / EffectiveReach, GuardRatio,
                (modelGuard - modelShoulder).magnitude / modelLength, fullRatio, limitRatio);
            var direction = current.sqrMagnitude > 1e-10f ? current.normalized : guard.normalized;
            return modelShoulder + mapping * direction * (radius * modelLength);
        }

        /// <summary>가드(0) → 도달(1) 사이 어디쯤인지. 방향 섞기 비율.</summary>
        public float ReachBlend(Vector3 hand)
        {
            if (!IsComplete) return 0f;
            var guard = Vector3.Distance(Guard, Shoulder);
            var current = Vector3.Distance(hand, Shoulder);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((current - guard) / Mathf.Max(0.001f, EffectiveReach - guard)));
        }

        /// <summary>몸 기준 방향 → 로봇 기준 방향 회전. 가드 기준 회전과 도달 기준 회전을 ReachBlend로 섞는다.</summary>
        public Quaternion DirectionMap(Vector3 hand, Vector3 modelShoulder, Vector3 modelGuard)
        {
            if (!IsComplete) return Quaternion.identity;
            var guardMap = DirectionFrame(modelGuard - modelShoulder) * Quaternion.Inverse(DirectionFrame(ToBody(Guard)));
            var reachMap = DirectionFrame(Vector3.forward) * Quaternion.Inverse(DirectionFrame(ToBody(ReachPoint)));
            return Quaternion.Slerp(guardMap, reachMap, ReachBlend(hand));
        }

        /// <summary>
        /// 사람 거리 비율(어깨–손 ÷ 도달 거리) → 로봇 거리 비율(어깨–주먹 ÷ 로봇 팔 길이).
        /// 0 → 0, 사람 가드 → 로봇 가드, 1 → fullRatio를 Hermite 곡선으로 잇고, 1을 넘으면 limitRatio로 부드럽게 수렴한다.
        /// 단조 증가하고 끊김이 없다.
        /// </summary>
        public static float MapRadius(float ratio, float guardRatio, float modelGuardRatio,
            float fullRatio = DefaultFullRatio, float limitRatio = DefaultLimitRatio)
        {
            guardRatio = Mathf.Clamp(guardRatio, 0.02f, 0.85f);
            modelGuardRatio = Mathf.Clamp(modelGuardRatio, 0.01f, 0.9f);
            ratio = Mathf.Max(0f, ratio);
            fullRatio = Mathf.Clamp(fullRatio, 0.91f, 0.99985f);
            limitRatio = Mathf.Clamp(limitRatio, fullRatio + 0.00001f, 0.99995f);

            var belowSlope = modelGuardRatio / guardRatio;
            var aboveSlope = (fullRatio - modelGuardRatio) / (1f - guardRatio);
            var guardSlope = Mathf.Min(belowSlope, aboveSlope);
            var endSlope = Mathf.Min(0.2f, aboveSlope);

            if (ratio <= guardRatio)
                return Hermite(ratio / guardRatio, 0f, modelGuardRatio, guardRatio * belowSlope, guardRatio * guardSlope);
            if (ratio < 1f)
            {
                var width = 1f - guardRatio;
                return Hermite((ratio - guardRatio) / width, modelGuardRatio, fullRatio, width * guardSlope, width * endSlope);
            }
            var tail = limitRatio - fullRatio;
            return fullRatio + tail * (1f - Mathf.Exp(-(ratio - 1f) / (tail / endSlope)));
        }

        /// <summary>위팔 upper, 아래팔 lower가 bendDegrees만큼 굽었을 때 어깨–손목 거리(0° = 완전히 폄).</summary>
        public static float DistanceForBend(float upper, float lower, float bendDegrees)
        {
            return Mathf.Sqrt(upper * upper + lower * lower + 2f * upper * lower * Mathf.Cos(bendDegrees * Mathf.Deg2Rad));
        }

        public static bool ValidRotation(Quaternion q)
        {
            var length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && length > 0.25f && length < 4f;
        }

        public static bool Finite(Vector3 v)
        {
            return Finite(v.x) && Finite(v.y) && Finite(v.z);
        }

        static bool Finite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        static Quaternion DirectionFrame(Vector3 direction)
        {
            var forward = direction.normalized;
            var up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < 0.001f) up = Vector3.ProjectOnPlane(Vector3.right, forward);
            return Quaternion.LookRotation(forward, up.normalized);
        }

        static float Hermite(float t, float a, float b, float ma, float mb)
        {
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * a + (t3 - 2f * t2 + t) * ma + (-2f * t3 + 3f * t2) * b + (t3 - t2) * mb;
        }
    }
}
