using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>조종사가 맡은 로봇 팔 쪽. 조종석 안에서도 같은 쪽 좌석에 선다.</summary>
    public enum PilotSide
    {
        Left,
        Right,
    }

    /// <summary>
    /// 로봇 루트에서 조종석 자세를 계산한다. 결과는 사람 크기(배율 1) 월드 좌표.
    /// </summary>
    public static class CockpitSeatMath
    {
        /// <summary>
        /// 조종석 중심 자세 = 로봇 루트 + 고정 오프셋. 회전은 로봇 정면의 yaw만 따른다
        /// (루트가 기울어도, 애니메이션 본이 흔들려도 시야에 넣지 않는다).
        /// offsetMeters는 로봇 정면 기준 미터 단위이며 로봇 배율과 무관하다.
        /// </summary>
        public static (Vector3 position, Quaternion rotation) RigPose(Vector3 rootPosition, Quaternion robotRotation, Vector3 offsetMeters)
        {
            var yaw = YawOnly(robotRotation);
            return (rootPosition + yaw * offsetMeters, yaw);
        }

        /// <summary>
        /// 본이 기준 위치에서 얼마나 움직였는지를 로봇 정면 기준(yaw만) 오프셋으로 돌려준다. weight배, 길이는 maxOffset까지.
        /// referenceLocal은 같은 기준(루트 원점, 로봇 정면 yaw)의 본 기준 위치(예: 대기 클립 평균 자세의 머리).
        /// </summary>
        public static Vector3 FollowOffset(Vector3 rootPosition, Quaternion robotRotation, Vector3 bonePosition,
            Vector3 referenceLocal, float weight, float maxOffset)
        {
            if (weight <= 0f || maxOffset <= 0f) return Vector3.zero;
            var local = Quaternion.Inverse(YawOnly(robotRotation)) * (bonePosition - rootPosition);
            return Vector3.ClampMagnitude((local - referenceLocal) * weight, maxOffset);
        }

        /// <summary>조종석 중심에서 각 조종사 좌석까지의 로컬 오프셋. 두 좌석 간격 = spacing.</summary>
        public static Vector3 SideOffset(PilotSide side, float spacing)
        {
            var half = spacing * 0.5f;
            return new Vector3(side == PilotSide.Left ? -half : half, 0f, 0f);
        }

        /// <summary>회전에서 수평 방향(yaw)만 남긴다. 정면이 수직이면 위쪽 축으로 방향을 정한다.</summary>
        public static Quaternion YawOnly(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                // 앞으로 엎어지면(정면이 아래) 위쪽 축이 정면, 뒤로 누우면 위쪽 축의 반대가 정면.
                var up = rotation * Vector3.up * -Mathf.Sign(forward.y);
                flat = new Vector3(up.x, 0f, up.z);
            }
            return Quaternion.LookRotation(flat.normalized, Vector3.up);
        }
    }
}
