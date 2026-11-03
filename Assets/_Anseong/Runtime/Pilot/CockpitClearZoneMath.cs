using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>
    /// 조종석 비움 영역 판정(선우 10/28: 로봇 모델이 조종석 안으로 들어오지 않게).
    /// 렌더러의 월드 AABB를 조종석 로컬로 옮겨 조종석 상자와 겹치는지 본다.
    /// </summary>
    public static class CockpitClearZoneMath
    {
        /// <summary>
        /// 월드 AABB(중심·반크기)가 조종석 로컬 상자(바닥 중심 원점, X 오른쪽, Y 위, Z 앞, 단위 m)와 겹치면 true.
        /// 조종석이 기울어 있으면 AABB를 감싸는 로컬 AABB로 넓혀서 본다(겹침을 놓치지 않는 쪽).
        /// </summary>
        public static bool Overlaps(Vector3 worldCenter, Vector3 worldExtents, Vector3 cockpitPosition, Quaternion cockpitRotation,
            Vector3 boxCenter, Vector3 boxSize)
        {
            var toLocal = Quaternion.Inverse(cockpitRotation);
            var center = toLocal * (worldCenter - cockpitPosition);
            var x = toLocal * Vector3.right;
            var y = toLocal * Vector3.up;
            var z = toLocal * Vector3.forward;
            var extents = new Vector3(
                Mathf.Abs(x.x) * worldExtents.x + Mathf.Abs(y.x) * worldExtents.y + Mathf.Abs(z.x) * worldExtents.z,
                Mathf.Abs(x.y) * worldExtents.x + Mathf.Abs(y.y) * worldExtents.y + Mathf.Abs(z.y) * worldExtents.z,
                Mathf.Abs(x.z) * worldExtents.x + Mathf.Abs(y.z) * worldExtents.y + Mathf.Abs(z.z) * worldExtents.z);
            var half = boxSize * 0.5f;
            return Mathf.Abs(center.x - boxCenter.x) <= extents.x + half.x
                && Mathf.Abs(center.y - boxCenter.y) <= extents.y + half.y
                && Mathf.Abs(center.z - boxCenter.z) <= extents.z + half.z;
        }
    }
}
