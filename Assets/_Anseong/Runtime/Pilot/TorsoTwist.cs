using System;
using UnityEngine;

namespace Anseong.Pilot
{
    [Serializable]
    public class TorsoTwistSettings
    {
        [Tooltip("두 손 선이 가드 때보다 돈 각도를 허리 회전에 얼마나 넣을지(0~1). 뒤로 뺀 손 쪽으로 허리가 돈다.")]
        [Range(0f, 1f)]
        public float handWeight = 0.5f;

        [Tooltip("고개가 몸 정면에서 돈 각도를 허리 회전에 얼마나 넣을지(0~1). 크면 주위를 둘러볼 때도 허리가 돈다.")]
        [Range(0f, 1f)]
        public float headWeight = 0.2f;

        [Tooltip("허리 회전 한계(도).")]
        [Range(0f, 60f)]
        public float maxDegrees = 30f;

        [Tooltip("두 손 사이가 이보다(m, 사람 크기) 가까우면 손으로 추정하지 않는다(선 방향이 불안정).")]
        public float minHandSpacing = 0.15f;

        [Tooltip("허리가 목표 각도를 따라가는 시간(초). 0이면 즉시.")]
        [Min(0f)]
        public float responseSeconds = 0.06f;
    }

    /// <summary>
    /// 허리 추적 장치 없이 사람 허리 회전(yaw)을 어림잡는다(1인 양팔 모드). 위에서 볼 때 시계 방향(오른쪽으로 돎)이 +.
    /// 손: 왼손 → 오른손 선이 가드 때보다 돈 각도. 오른손을 뒤로 빼면 +(오른쪽으로 감음), 왼손을 뒤로 빼면 −.
    /// 머리: 고개가 몸 정면에서 돈 각도.
    /// </summary>
    public static class TorsoTwist
    {
        /// <summary>목표 허리 yaw(도). 보정이 덜 됐으면 0.</summary>
        public static float EstimateYaw(ArmCalibration left, ArmCalibration right, Vector3 leftHand, Vector3 rightHand,
            float headYawFromBody, TorsoTwistSettings settings)
        {
            if (settings == null || left == null || right == null || !left.IsComplete || !right.IsComplete) return 0f;
            var handYaw = 0f;
            var guardLine = Flat(right.Guard - left.Guard);
            var line = Flat(rightHand - leftHand);
            if (guardLine.magnitude >= settings.minHandSpacing && line.magnitude >= settings.minHandSpacing)
                handYaw = Vector3.SignedAngle(guardLine, line, Vector3.up);
            var yaw = settings.handWeight * handYaw + settings.headWeight * Mathf.DeltaAngle(0f, headYawFromBody);
            return Mathf.Clamp(yaw, -settings.maxDegrees, settings.maxDegrees);
        }

        /// <summary>트래킹 공간 회전의 yaw가 몸 정면 yaw에서 몇 도 돌았는지.</summary>
        public static float HeadYawFromBody(Quaternion head, float bodyYawDegrees)
        {
            var forward = head * Vector3.forward;
            var flat = Flat(forward);
            if (flat.sqrMagnitude < 1e-6f) return 0f;
            return Mathf.DeltaAngle(bodyYawDegrees, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 허리가 yaw만큼 돌았을 때 보정한 사람 어깨가 움직이는 양(트래킹 공간, m). 회전 중심 = 두 어깨의 가운데.
        /// 팔 매핑이 이 어깨를 기준으로 재야 허리를 돌리며 뻗은 손이 로봇에서도 같은 자세가 된다.
        /// </summary>
        public static Vector3 ShoulderShift(ArmCalibration own, ArmCalibration left, ArmCalibration right, float yaw)
        {
            if (own == null || left == null || right == null || !left.HasShoulder || !right.HasShoulder) return Vector3.zero;
            var center = (left.Shoulder + right.Shoulder) * 0.5f;
            var arm = Flat(own.Shoulder - center);
            return Quaternion.AngleAxis(yaw, Vector3.up) * arm - arm;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
