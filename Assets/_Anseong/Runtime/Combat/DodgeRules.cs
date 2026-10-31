using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class DodgeSettings
    {
        [Tooltip("스틱을 이만큼 이상 밀어야 입력으로 친다(01: 0.5).")]
        [Range(0.1f, 1f)] public float minMagnitude = 0.5f;

        [Tooltip("두 스틱 방향 차이가 이 각도(°) 이내여야 같은 방향(01: 25).")]
        [Range(1f, 90f)] public float maxAngle = 25f;

        [Tooltip("좌/우 회피로 치는 방향: 수평에서 이 각도(°) 이내.")]
        [Range(10f, 89f)] public float maxFromHorizontal = 45f;

        [Tooltip("명중 순간 이 시간(초) 전부터 시작한 입력만 회피로 인정(01: 0.6). 미리 밀고 있던 것은 안 됨.")]
        [Range(0.1f, 2f)] public float windowSeconds = 0.6f;
    }

    /// <summary>
    /// 협동 회피(01 "가드와 회피"·"이동"): 두 보조 스틱이 같은 좌/우 방향(크기·각도 기준)으로 "새로" 밀린 순간을 기록하고,
    /// 명중 순간 기준 창 안에서 시작했을 때만 회피로 친다. 미리 밀고 있었거나 미리 옆에 가 있던 것은 인정하지 않는다. 순수 클래스.
    /// </summary>
    public sealed class DodgeRules
    {
        readonly DodgeSettings settings;
        readonly Vector2[] sticks = new Vector2[2];
        bool agreeing;

        public DodgeRules(DodgeSettings settings = null)
        {
            this.settings = settings ?? new DodgeSettings();
            OnsetTime = double.NegativeInfinity;
        }

        public DodgeSettings Settings => settings;
        /// <summary>마지막으로 두 스틱이 같은 방향으로 새로 밀린 시각.</summary>
        public double OnsetTime { get; private set; }
        /// <summary>마지막 회피 방향: -1 왼쪽, +1 오른쪽.</summary>
        public int Direction { get; private set; }
        public bool Agreeing => agreeing;
        public Vector2 Stick(PilotSide side) => sticks[(int)side];

        /// <summary>한 조종사의 스틱 값. 두 스틱이 같은 방향으로 새로 맞은 순간이면 true(회피 시작).</summary>
        public bool Update(PilotSide side, Vector2 stick, double time)
        {
            sticks[(int)side] = stick;
            var now = Agree(sticks[0], sticks[1], settings, out var direction);
            var onset = now && (!agreeing || direction != Direction);
            agreeing = now;
            if (!onset) return false;
            Direction = direction;
            OnsetTime = time;
            return true;
        }

        /// <summary>명중 순간에 회피가 인정되는지.</summary>
        public bool Dodged(double impactTime) => OnsetTime <= impactTime && OnsetTime >= impactTime - settings.windowSeconds;

        public void Reset()
        {
            sticks[0] = sticks[1] = Vector2.zero;
            agreeing = false;
            Direction = 0;
            OnsetTime = double.NegativeInfinity;
        }

        public static bool Agree(Vector2 a, Vector2 b, DodgeSettings s, out int direction)
        {
            direction = 0;
            if (a.magnitude < s.minMagnitude || b.magnitude < s.minMagnitude) return false;
            if (Vector2.Angle(a, b) > s.maxAngle) return false;
            var mean = (a.normalized + b.normalized) * 0.5f;
            if (mean.sqrMagnitude < 1e-6f) return false;
            var fromHorizontal = Mathf.Atan2(Mathf.Abs(mean.y), Mathf.Abs(mean.x)) * Mathf.Rad2Deg;
            if (fromHorizontal > s.maxFromHorizontal) return false;
            direction = mean.x < 0f ? -1 : 1;
            return true;
        }
    }
}
