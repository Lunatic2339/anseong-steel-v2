using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>한 프레임에 같은 장치에서 읽은 손 하나의 입력. 위치·회전은 트래킹 공간(XR Origin의 Camera Offset 기준).</summary>
    public struct PilotHandSample
    {
        /// <summary>위치·회전이 둘 다 추적 중이고 값이 유효하다.</summary>
        public bool Tracked;

        /// <summary>마지막으로 추적된 위치(m). 추적이 끊기면 끊기기 직전 값을 유지한다.</summary>
        public Vector3 Position;

        /// <summary>마지막으로 추적된 회전. 추적이 끊기면 끊기기 직전 값을 유지한다.</summary>
        public Quaternion Rotation;

        /// <summary>그립 아날로그 값 0~1.</summary>
        public float Grip;

        /// <summary>그립을 쥐고 있다(히스테리시스 적용).</summary>
        public bool GripHeld;

        /// <summary>트리거 아날로그 값 0~1.</summary>
        public float Trigger;

        /// <summary>스틱 -1~1.</summary>
        public Vector2 Stick;

        /// <summary>그립 유지 + 추적 유효 + (끊김 후라면) 그립을 한 번 놓음.</summary>
        public bool AttackIntent;

        /// <summary>이 손이 로봇 팔 하나를 맡는다(IK 목표·펀치).</summary>
        public bool IsArmHand;

        /// <summary>이 손의 스틱이 이동 입력이다.</summary>
        public bool IsMoveHand;
    }

    /// <summary>
    /// 손별 역할. 일반 모드는 담당 팔 쪽 손 = 팔, 반대 손 = 이동(기존 버그 7: 이동 스틱이 펀치하는 손이면 안 된다).
    /// 1인 양팔 모드는 양손이 모두 팔이고 스틱도 둘 다 이동 입력이다.
    /// </summary>
    public static class PilotHandRoles
    {
        public static bool IsArmHand(PilotSide hand, PilotSide armSide, bool controlsBothArms)
        {
            return controlsBothArms || hand == armSide;
        }

        public static bool IsMoveHand(PilotSide hand, PilotSide armSide, bool controlsBothArms)
        {
            return controlsBothArms || hand != armSide;
        }
    }
}
