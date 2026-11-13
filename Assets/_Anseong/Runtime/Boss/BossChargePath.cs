using System;
using UnityEngine;

namespace Anseong.Boss
{
    [Serializable]
    public class BossChargeSettings
    {
        [Tooltip("예고 동안 몸을 낮추며 근접 거리에서 이만큼(m) 더 물러나 자세를 잡는다.")]
        [Range(0f, 15f)] public float windUpMeters = 5f;

        [Tooltip("명중 순간 이만큼(초) 전부터 달려온다(예고의 60%를 넘지 않는다). 짧을수록 빠른 돌진.")]
        [Range(0.3f, 2f)] public float runSeconds = 0.9f;

        [Tooltip("명중 순간 조종사와의 앞뒤 거리(m). 너무 작으면 보스 몸이 조종석 카메라와 겹친다.")]
        [Range(8f, 30f)] public float closeDistance = 20f;

        [Tooltip("명중 순간 옆으로 비켜 있는 거리(m). 안전한 쪽의 반대편으로 스쳐 지나간다.")]
        [Range(0f, 25f)] public float sideOffset = 10f;

        [Tooltip("명중 순간 뒤 달리던 방향으로 미끄러지는 거리(m).")]
        [Range(0f, 10f)] public float skidMeters = 3f;

        [Tooltip("미끄러지며 멈추는 시간(초).")]
        [Range(0.05f, 1.5f)] public float skidSeconds = 0.5f;

        [Tooltip("멈춘 뒤 근접 거리로 돌아가는 속도(m/s).")]
        [Range(2f, 30f)] public float returnSpeed = 10f;

        [Tooltip("돌진·복귀 동안 조종사 쪽으로 도는 속도(°/s).")]
        [Range(10f, 360f)] public float turnDegreesPerSecond = 90f;
    }

    /// <summary>
    /// 돌진 경로(05 기술표 "돌진", 대본 장면 5). 순수 함수 → EditMode 테스트.
    /// 조종사를 원점으로, 조종사 → 보스 방향을 "앞뒤(y)", 조종사 오른쪽을 "옆(x, +)"으로 본 평면 좌표를 돌려준다.
    /// 흐름: 예고 앞부분 = 몸을 낮추며 뒤로 조금 물러남 → 예고 끝부분 = 가속해 달려옴 → 명중 순간 = 안전한 쪽 반대편으로 스침 → 미끄러지며 멈춤.
    /// 그 뒤 근접 거리로 돌아오는 것은 <see cref="BossBrain"/>이 속도로 움직인다.
    /// </summary>
    public static class BossChargePath
    {
        /// <summary>달리는 시간(초): 설정값, 단 예고의 60%까지.</summary>
        public static float RunSeconds(BossChargeSettings s, float telegraphSeconds) =>
            Mathf.Max(0.05f, Mathf.Min(s.runSeconds, telegraphSeconds * 0.6f));

        /// <summary>경로가 끝나는 시각(공격 시작 기준 초) = 예고 + 미끄러짐.</summary>
        public static float EndSeconds(BossChargeSettings s, float telegraphSeconds) => telegraphSeconds + Mathf.Max(0f, s.skidSeconds);

        /// <summary>
        /// 공격 시작 뒤 t초의 위치. from = 시작할 때 조종사와의 거리(m), home = 근접 거리(m),
        /// safeSide = 조종사가 피해야 하는 쪽(-1 왼쪽, +1 오른쪽). 보스는 그 반대쪽으로 비켜 달린다.
        /// </summary>
        public static Vector2 At(BossChargeSettings s, float from, float home, float telegraphSeconds, float t, int safeSide)
        {
            var back = Mathf.Max(from, home + s.windUpMeters);
            var close = Mathf.Min(s.closeDistance, back);
            var side = safeSide < 0 ? s.sideOffset : -s.sideOffset;
            var run = RunSeconds(s, telegraphSeconds);
            var windEnd = telegraphSeconds - run;
            var start = new Vector2(0f, back);
            var pass = new Vector2(side, close);

            if (t <= windEnd)
            {
                // 자세 잡기: 부드럽게 물러난다.
                var u = windEnd > 1e-4f ? Mathf.Clamp01(t / windEnd) : 1f;
                return new Vector2(0f, Mathf.Lerp(from, back, u * u * (3f - 2f * u)));
            }
            if (t <= telegraphSeconds)
            {
                // 달려옴: 처음엔 느리게, 갈수록 빠르게(육중한 가속).
                var u = Mathf.Clamp01((t - windEnd) / run);
                return Vector2.Lerp(start, pass, u * u);
            }
            // 미끄러지며 멈춤: 달리던 방향으로 조금 더, 갈수록 느리게.
            var direction = pass - start;
            if (direction.sqrMagnitude < 1e-6f) return pass;
            var v = s.skidSeconds > 1e-4f ? Mathf.Clamp01((t - telegraphSeconds) / s.skidSeconds) : 1f;
            return pass + direction.normalized * (s.skidMeters * (1f - (1f - v) * (1f - v)));
        }
    }
}
