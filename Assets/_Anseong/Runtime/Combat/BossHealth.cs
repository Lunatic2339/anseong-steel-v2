using System;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class BossHealthSettings
    {
        [Tooltip("보스 최대 HP. 피해는 이것 대비 %로 정한다.")]
        [Min(1f)]
        public float maxHealth = 100f;

        [Tooltip("잽 피해(최대 HP 대비 %).")]
        public float jabDamage = 3f;

        [Tooltip("훅 피해(%).")]
        public float hookDamage = 5f;

        [Tooltip("어퍼컷 피해(%, 01 제안값).")]
        public float uppercutDamage = 4f;

        [Tooltip("부스트 성공 추가 피해(%). 훅 피해에 더해진다.")]
        public float boostDamage = 6f;
    }

    /// <summary>보스 HP(01 "공격" 피해 표). 0이 되면 더 깎이지 않는다(쓰러짐 연출은 M2). 서버만 계산(M3).</summary>
    public sealed class BossHealth
    {
        readonly BossHealthSettings settings;

        public BossHealth(BossHealthSettings settings = null)
        {
            this.settings = settings ?? new BossHealthSettings();
            Current = this.settings.maxHealth;
        }

        public float Current { get; private set; }
        public float Max => settings.maxHealth;
        public float Fraction => Max > 0f ? Current / Max : 0f;
        public bool IsDefeated => Current <= 0f;

        public void Reset() => Current = settings.maxHealth;

        public float DamageOf(PunchKind kind)
        {
            switch (kind)
            {
                case PunchKind.Jab: return settings.jabDamage;
                case PunchKind.Hook: return settings.hookDamage;
                case PunchKind.Uppercut: return settings.uppercutDamage;
                default: return 0f;
            }
        }

        public float BoostDamage => settings.boostDamage;

        /// <summary>피해(%)를 준다. 실제로 깎인 양을 돌려준다.</summary>
        public float Apply(float percent)
        {
            if (IsDefeated || percent <= 0f || float.IsNaN(percent)) return 0f;
            var amount = Mathf.Min(Current, settings.maxHealth * percent / 100f);
            Current -= amount;
            return amount / settings.maxHealth * 100f;
        }
    }
}
