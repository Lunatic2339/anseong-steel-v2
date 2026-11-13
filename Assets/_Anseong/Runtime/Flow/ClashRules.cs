using System;
using UnityEngine;

namespace Anseong.Flow
{
    [Serializable]
    public class ClashSettings
    {
        [Tooltip("장면에 들어온 뒤 이만큼(초) 지나 검이 맞물린다(보스의 보복 내려치기가 떨어지는 시간).")]
        [Range(0f, 6f)] public float delaySeconds = 2.6f;

        [Tooltip("힘겨루기 최대 시간(초). 지나면 그때 게이지가 절반 이상이면 이긴다.")]
        [Range(2f, 15f)] public float maxSeconds = 8f;

        [Tooltip("시작 게이지(0 = 밀림, 1 = 밀어냄).")]
        [Range(0.1f, 0.9f)] public float startGauge = 0.4f;

        [Tooltip("둘 다 팔을 들어 버틸 때 초당 오르는 양.")]
        [Range(0.05f, 1f)] public float riseBoth = 0.2f;

        [Tooltip("한 사람만 버틸 때 초당 내려가는 양(혼자서는 못 버틴다).")]
        [Range(0f, 1f)] public float slipOne = 0.12f;

        [Tooltip("아무도 안 버틸 때 초당 내려가는 양.")]
        [Range(0.05f, 2f)] public float fallNone = 0.4f;

        [Tooltip("밀어내면 보스가 휘청이는 시간(초).")]
        [Range(0.5f, 8f)] public float winStagger = 3.5f;

        [Tooltip("밀어내면 핵에 미리 들어가는 피해(보스 HP %).")]
        [Range(0f, 15f)] public float winDamage = 4f;

        [Tooltip("버티는 동안 불꽃·소리·진동을 내는 간격(초).")]
        [Range(0.1f, 2f)] public float tickSeconds = 0.45f;
    }

    public enum ClashStep
    {
        None,
        /// <summary>검이 맞물렸다(힘겨루기 시작).</summary>
        Started,
        /// <summary>버티는 중 불꽃 한 번.</summary>
        Tick,
        /// <summary>둘이 밀어냈다.</summary>
        Won,
        /// <summary>밀렸다.</summary>
        Lost,
    }

    /// <summary>
    /// 검 맞부딪힘 힘겨루기(14_scenario_changes.md, 11/13). 보스 방패가 깨진 뒤 보스가 검을 가장 크게 내려치고,
    /// 둘이 검·방패를 들어(막기 자세) 함께 버텨 밀어낸다. 한 사람만 버티면 밀린다. 이기든 지든 양쪽 검·방패는 부서진다(대본 장면 7).
    /// 이기면 보스가 크게 휘청이고 핵에 금이 먼저 간다. 순수 클래스, 서버 시각.
    /// </summary>
    public sealed class ClashRules
    {
        readonly ClashSettings settings;
        double begin = double.NaN, started = double.NaN, last, nextTick;

        public ClashRules(ClashSettings settings = null)
        {
            this.settings = settings ?? new ClashSettings();
        }

        public ClashSettings Settings => settings;
        /// <summary>0 = 밀림, 1 = 밀어냄.</summary>
        public float Gauge { get; private set; }
        public bool Active => !double.IsNaN(started) && !Done;
        public bool Done { get; private set; }
        public bool Won { get; private set; }

        /// <summary>장면에 들어올 때.</summary>
        public void Begin(double now)
        {
            begin = now;
            started = double.NaN;
            Done = Won = false;
            Gauge = settings.startGauge;
        }

        public void Reset()
        {
            begin = started = double.NaN;
            Done = Won = false;
            Gauge = 0f;
        }

        public ClashStep Update(double now, bool leftBracing, bool rightBracing)
        {
            if (double.IsNaN(begin) || Done) return ClashStep.None;
            if (double.IsNaN(started))
            {
                if (now - begin < settings.delaySeconds) return ClashStep.None;
                started = last = now;
                nextTick = now + settings.tickSeconds;
                return ClashStep.Started;
            }
            var dt = (float)Math.Max(0.0, now - last);
            last = now;
            var rate = leftBracing && rightBracing ? settings.riseBoth : leftBracing || rightBracing ? -settings.slipOne : -settings.fallNone;
            Gauge = Mathf.Clamp01(Gauge + rate * dt);
            if (Gauge >= 1f) return End(true);
            if (Gauge <= 0f) return End(false);
            if (now - started >= settings.maxSeconds) return End(Gauge >= 0.5f);
            if (now < nextTick) return ClashStep.None;
            nextTick = now + settings.tickSeconds;
            return ClashStep.Tick;
        }

        ClashStep End(bool won)
        {
            Done = true;
            Won = won;
            return won ? ClashStep.Won : ClashStep.Lost;
        }
    }
}
