using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>손 위치를 로봇 주먹 목표로 옮기는 방식.</summary>
    public enum ArmMappingMode
    {
        /// <summary>로봇 어깨 + (손 − 사람 어깨) × (로봇 팔 길이 ÷ 개인 팔 길이). 사람 가드 위치가 그대로 커진다.</summary>
        Linear,
        /// <summary>사람 가드 → 정해 둔 로봇 가드, 끝까지 뻗음 → 로봇 정면으로 방향을 섞는다(M1-02 ArmCalibration.Map).</summary>
        GuardMatched,
    }

    /// <summary>
    /// 조종사 손 → 로봇 주먹 목표. 결과는 로봇 정면 기준(+Z = 앞, +Y = 위) 로봇 어깨 원점 좌표(미터, 로봇 크기).
    /// 사람 쪽 입력은 트래킹 공간 좌표이고 몸 정면 yaw(M1-02 보정)를 기준으로 돌린다.
    /// </summary>
    public static class ArmMapping
    {
        /// <summary>선형 매핑에서 이 비율(어깨–손 ÷ 개인 팔 길이)까지는 거의 그대로 비례하고, 그 뒤로 끝까지 뻗은 자세에 부드럽게 붙는다.</summary>
        public const float LinearSoftStart = 0.85f;

        /// <summary>
        /// 선형 매핑. 끝까지 뻗은 손(개인 팔 길이)은 로봇 팔이 3° 굽은 거리에, 더 뻗으면 2° 굽은 거리에 지수형으로 수렴한다.
        /// shoulderHeightOffset(m, 사람 크기): 사람 어깨 기준점을 이만큼 올린다(양수면 로봇 주먹이 내려간다).
        /// 보정이 끝나지 않았거나 값이 잘못되면 false.
        /// </summary>
        public static bool TryLinear(ArmCalibration calibration, Vector3 hand, float robotArmLength,
            float shoulderHeightOffset, out Vector3 robotLocal)
        {
            robotLocal = Vector3.zero;
            if (calibration == null || !calibration.IsComplete || !ArmCalibration.Finite(hand) || robotArmLength <= 0f) return false;
            var shoulder = calibration.Shoulder + Vector3.up * shoulderHeightOffset;
            var local = Quaternion.Inverse(calibration.BodyRotation) * (hand - shoulder);
            var length = local.magnitude;
            if (length < 1e-6f) return true;
            var radius = ArmCalibration.MapRadius(length / calibration.EffectiveReach, LinearSoftStart, LinearSoftStart);
            robotLocal = local / length * (radius * robotArmLength);
            return true;
        }

        /// <summary>
        /// 가드 맞춤 매핑. robotGuardLocal = 로봇 가드 주먹 위치(로봇 어깨 원점, 로봇 정면 기준, 미터).
        /// 어깨 높이 보정은 쓰지 않는다(가드가 정해진 자세로 맞춰지므로).
        /// </summary>
        public static bool TryGuardMatched(ArmCalibration calibration, Vector3 hand, float robotArmLength,
            Vector3 robotGuardLocal, out Vector3 robotLocal)
        {
            robotLocal = Vector3.zero;
            if (calibration == null || !calibration.IsComplete || !ArmCalibration.Finite(hand) || robotArmLength <= 0f) return false;
            robotLocal = calibration.Map(hand, Vector3.zero, robotGuardLocal, robotArmLength);
            return true;
        }

        /// <summary>
        /// 컨트롤러 회전 → 로봇 주먹 회전(로봇 정면 기준, 주먹 정면 = +Z, 손등 = +Y).
        /// 도달 보정 때의 손 회전(펀치 자세, 손등 위·엄지 안쪽)이 "정면으로 뻗은 주먹"이고, 그 뒤로 돌린 만큼 주먹도 돈다.
        /// </summary>
        public static Quaternion FistLocal(ArmCalibration calibration, Quaternion controller)
        {
            if (calibration == null || !calibration.IsComplete || !ArmCalibration.ValidRotation(controller)) return Quaternion.identity;
            var body = calibration.BodyRotation;
            return (Quaternion.Inverse(body) * controller.normalized * Quaternion.Inverse(calibration.ReachRotation) * body).normalized;
        }

        /// <summary>
        /// 팔꿈치 힌트용 손 이동량: 가드에서 손이 얼마나 움직였는지(몸 정면 기준, 사람 크기 m).
        /// </summary>
        public static Vector3 GuardDelta(ArmCalibration calibration, Vector3 hand)
        {
            if (calibration == null || !calibration.IsComplete || !ArmCalibration.Finite(hand)) return Vector3.zero;
            return Quaternion.Inverse(calibration.BodyRotation) * (hand - calibration.Guard);
        }
    }

    /// <summary>
    /// 손 궤적에서 팔꿈치를 들어 올릴지(0~1) 정하는 연속 힌트. 펀치 분류가 아니다.
    /// 기존 레포 ControllerArmFollower.cs의 ControllerElbowHint를 옮겼다.
    /// </summary>
    public sealed class ElbowLiftHint
    {
        Vector3 previous;
        bool hasPrevious;

        public float Lift { get; private set; }

        public void Reset()
        {
            previous = Vector3.zero;
            hasPrevious = false;
            Lift = 0f;
        }

        /// <summary>delta: <see cref="ArmMapping.GuardDelta"/>. dt: 초.</summary>
        public float Update(Vector3 delta, float dt)
        {
            if (dt <= 0f) return Lift;
            float lateralSpeed = hasPrevious ? Mathf.Abs(delta.x - previous.x) / dt : 0f;
            previous = delta;
            hasPrevious = true;
            float lateral = Mathf.InverseLerp(.05f, .22f, Mathf.Abs(delta.x));
            float sweeping = Mathf.InverseLerp(.25f, 1.1f, lateralSpeed);
            float raised = Mathf.InverseLerp(.06f, .23f, delta.y);
            float cue = Mathf.Max(lateral, Mathf.Max(sweeping * .9f, raised * .8f));
            // 곧은 펀치가 끝까지 뻗어 갈수록 힌트를 줄인다.
            cue *= 1f - .5f * Mathf.InverseLerp(.3f, .6f, delta.z);
            Lift = Mathf.Lerp(Lift, cue, 1f - Mathf.Exp(-dt / (cue > Lift ? .055f : .18f)));
            return Lift;
        }
    }
}
