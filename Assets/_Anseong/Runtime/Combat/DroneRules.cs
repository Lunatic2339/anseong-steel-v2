using System;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>드론 종류(10_game_scenario.md 장면 3·4-2·6·8). 드론 코드 하나로 세 가지.</summary>
    public enum DroneKind : byte
    {
        /// <summary>은폐 사격: 숨어서 쏜다. 스캐너가 훑으면 드러나고, 드러난 동안 포로 격추.</summary>
        Cloaked,
        /// <summary>달려드는 드론(총 잃은 뒤): 날아와 부딪힌다. 주먹·검으로 쳐서 떨군다.</summary>
        Rusher,
        /// <summary>해킹 드론(맨손): 팔에 붙는다. 동료 팔이 뜯어 준다.</summary>
        Hacker,
    }

    public enum DroneState : byte
    {
        None,
        Hidden,
        Revealed,
        Down,
    }

    [Serializable]
    public class DroneSettings
    {
        [Tooltip("숨은 드론 사격 간격(초, 대본 4).")]
        [Min(0.5f)] public float fireInterval = 4f;
        [Tooltip("드론 포탄 피해(기체 HP %, 막으면 0). 대본 3.")]
        [Min(0f)] public float fireDamage = 3f;
        [Tooltip("스캐너가 훑으면 드러나는 시간(초, 대본 3).")]
        [Min(0.5f)] public float revealSeconds = 3f;
        [Tooltip("스캐너 레이저가 드론을 맞혔다고 보는 각도(°).")]
        [Range(1f, 30f)] public float scanDegrees = 6f;
        [Tooltip("포 조준이 붙는 각도(°, 대본: 겨누면 조준이 붙는다).")]
        [Range(1f, 30f)] public float aimDegrees = 10f;
        [Tooltip("드론 장면을 이 시간(초) 안에 못 끝내면 남은 드론이 보스에게 돌아간다(대본 60).")]
        [Min(5f)] public float huntTimeout = 60f;
        [Tooltip("달려드는 드론이 닿기까지(초).")]
        [Min(1f)] public float rushSeconds = 5f;
        [Tooltip("달려드는 드론 사이(초).")]
        [Min(3f)] public float rushInterval = 18f;
        [Tooltip("주먹·검이 이 거리(m) 안에 오면 쳐서 떨군 것으로 본다.")]
        [Min(0.5f)] public float punchRadius = 10f;
        [Tooltip("해킹 드론이 붙는 때(맨손 장면 시작 뒤 초). 대본: 두 번.")]
        public float[] hackTimes = { 20f, 60f };
        [Tooltip("동료 손이 해킹된 손에서 이 거리(m) 안에 오면 뜯어낸 것으로 본다.")]
        [Min(0.5f)] public float hackClearRadius = 12f;
    }

    /// <summary>드론 판정 수학(M4-04). 순수 함수.</summary>
    public static class DroneRules
    {
        /// <summary>origin에서 dir로 본 target의 각도(°).</summary>
        public static float AngleTo(Vector3 origin, Vector3 dir, Vector3 target)
        {
            var to = target - origin;
            if (to.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-8f) return 180f;
            return Vector3.Angle(dir, to);
        }

        /// <summary>각도 안의 것 중 가장 가까운 각도의 번호. 없으면 -1.</summary>
        public static int Pick(Vector3 origin, Vector3 dir, Vector3[] targets, bool[] eligible, float maxDegrees)
        {
            var best = -1;
            var bestAngle = maxDegrees;
            for (var i = 0; i < targets.Length; i++)
            {
                if (eligible != null && (i >= eligible.Length || !eligible[i])) continue;
                var a = AngleTo(origin, dir, targets[i]);
                if (a > bestAngle) continue;
                bestAngle = a;
                best = i;
            }
            return best;
        }

        /// <summary>달려드는 드론의 위치(시작 → 목표, t = 0~1).</summary>
        public static Vector3 RushPosition(Vector3 from, Vector3 to, float t) => Vector3.Lerp(from, to, Mathf.Clamp01(t));
    }
}
