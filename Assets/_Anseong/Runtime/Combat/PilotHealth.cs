using System;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class PilotHealthSettings
    {
        [Tooltip("조종사 로봇 HP(05: 100). 보스 공격 피해는 이것 대비 %.")]
        [Min(1f)] public float maxHealth = 100f;

        [Tooltip("위기 피해 감소(10/18 채택, 실험 기능): HP가 낮을수록 받는 피해를 줄인다. 진동·소리는 그대로.")]
        public bool crisisReduction = true;

        [Tooltip("이 HP(%) 이상이면 피해 100%.")]
        [Range(0f, 100f)] public float fullDamageAbove = 50f;

        [Tooltip("이 HP(%) 이하이면 최소 배율. 사이는 직선.")]
        [Range(0f, 100f)] public float minDamageBelow = 20f;

        [Tooltip("최소 배율(0보다 커야 패배가 가능). 05: 0.5.")]
        [Range(0.05f, 1f)] public float minMultiplier = 0.5f;
    }

    /// <summary>조종사 로봇 HP와 위기 피해 감소(05 "조종사 로봇", 01 "가드와 회피"). 서버만 계산. 순수 클래스.</summary>
    public sealed class PilotHealth
    {
        readonly PilotHealthSettings settings;

        public PilotHealth(PilotHealthSettings settings = null)
        {
            this.settings = settings ?? new PilotHealthSettings();
            Current = this.settings.maxHealth;
        }

        public PilotHealthSettings Settings => settings;
        public float Current { get; private set; }
        public float Max => settings.maxHealth;
        public float Percent => Max > 0f ? Current / Max * 100f : 0f;
        public bool IsDefeated => Current <= 0f;

        public void Reset() => Current = settings.maxHealth;

        /// <summary>지금 HP에서 받는 피해 배율.</summary>
        public float Multiplier()
        {
            if (!settings.crisisReduction) return 1f;
            var hp = Percent;
            var high = Mathf.Max(settings.fullDamageAbove, settings.minDamageBelow);
            var low = Mathf.Min(settings.fullDamageAbove, settings.minDamageBelow);
            var min = Mathf.Clamp(settings.minMultiplier, 0.05f, 1f);
            if (hp >= high) return 1f;
            if (hp <= low || high - low < 1e-4f) return min;
            return Mathf.Lerp(min, 1f, (hp - low) / (high - low));
        }

        /// <summary>표의 피해(%)에 배율을 곱해 깎는다. 실제로 깎인 양(%)을 돌려준다.</summary>
        public float TakeHit(float baseDamagePercent)
        {
            if (IsDefeated || baseDamagePercent <= 0f || float.IsNaN(baseDamagePercent)) return 0f;
            var amount = Mathf.Min(Current, Max * baseDamagePercent * Multiplier() / 100f);
            Current -= amount;
            return amount / Max * 100f;
        }

        /// <summary>HP가 percent(%) 미만이면 그만큼으로 올린다(페이즈 시작 회복, M2-04).</summary>
        public bool RestoreTo(float percent)
        {
            var target = Max * Mathf.Clamp(percent, 0f, 100f) / 100f;
            if (IsDefeated || Current >= target) return false;
            Current = target;
            return true;
        }
    }
}
