using System;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 주먹 구가 이전 프레임 위치 → 지금 위치로 지나가는 동안 표면에 닿았는지(순수). 빠른 주먹이 한 프레임에 보스를 뚫고 지나가도 잡는다.
    /// closestPoint는 표면에서 가장 가까운 점(Collider.ClosestPoint). 기존 레포 MechaVRBossContact의 쓸어 보기를 단순하게 다시 씀.
    /// </summary>
    public static class FistContact
    {
        /// <summary>닿았으면 true와 처음 닿은 지점. 쓸어 보는 간격은 반지름의 절반, 최대 maxSteps.</summary>
        public static bool Sweep(Vector3 from, Vector3 to, float radius, Func<Vector3, Vector3> closestPoint, out Vector3 point, int maxSteps = 32)
        {
            point = to;
            if (closestPoint == null || radius <= 0f) return false;
            var distance = Vector3.Distance(from, to);
            var steps = Mathf.Clamp(Mathf.CeilToInt(distance / (radius * 0.5f)), 1, Mathf.Max(1, maxSteps));
            for (var i = 0; i <= steps; i++)
            {
                var p = Vector3.Lerp(from, to, (float)i / steps);
                var surface = closestPoint(p);
                if ((surface - p).sqrMagnitude <= radius * radius)
                {
                    point = surface;
                    return true;
                }
            }
            return false;
        }
    }
}
