using System;

namespace Anseong.Boss
{
    /// <summary>보스 무기(10_game_scenario.md "보스 무기와 맞바꾸기"). 차례대로 부순다. 숫자는 끝에만 더한다.</summary>
    public enum BossWeapon : byte
    {
        /// <summary>양 팔뚝 포(주먹 포탄을 쏘던 원통). 부서지면 보복 돌진 → 우리 포 상실.</summary>
        ArmCannon,
        /// <summary>가슴 장갑판. 부서지면 보복 큰 내려찍기 → 우리 검·방패 상실, 핵이 드러남.</summary>
        ChestArmor,
        /// <summary>가슴 핵. 부서지면 무릎 → 필살기.</summary>
        Core,
        /// <summary>다 부서짐.</summary>
        None,
    }

    /// <summary>
    /// 보스 무기 내구도(M4-02, 확정 사항 14: 페이즈 = 보스 무기 부서짐). 순수 클래스.
    /// 보스 체력 100을 무기 세 개가 차례로 나눠 가진다(무기마다 내구도 30 → 경계 70·40·10, 남은 10은 필살기 몫).
    /// 그래서 피해·바닥·페이즈 전환은 지금 규칙(<see cref="BossPhaseRules"/>, BossHealth)을 그대로 쓰고, 이 클래스는 "지금 어느 무기에 금 몇 개"만 계산한다.
    /// 금은 내구도의 1/3마다 하나(대본: 금 10·20·30), 셋째 금 = 부서짐.
    /// </summary>
    public static class BossWeaponRules
    {
        public const int Cracks = 3;
        public const int WeaponCount = 3;

        /// <summary>무기 내구도(최대 체력 대비 %)로 페이즈 경계(%)를 만든다: 30 → 70, 40, 10.</summary>
        public static float[] Thresholds(float durability)
        {
            var d = Math.Max(1f, Math.Min(33f, durability));
            return new[] { 100f - d, 100f - 2f * d, 100f - 3f * d };
        }

        /// <summary>지금 차례 무기. 경계에 닿은 무기는 부서진 것으로 본다.</summary>
        public static BossWeapon Current(float hpPercent, float[] thresholds)
        {
            if (thresholds == null) return BossWeapon.None;
            var n = Math.Min(WeaponCount, thresholds.Length);
            for (var i = 0; i < n; i++)
                if (hpPercent > thresholds[i] + 1e-3f) return (BossWeapon)i;
            return BossWeapon.None;
        }

        /// <summary>그 무기 구간의 처음 체력(%). 재기동하면 여기로 되돌린다(그 구간 처음부터).</summary>
        public static float SegmentTop(BossWeapon weapon, float[] thresholds)
        {
            var i = (int)weapon;
            if (thresholds == null || i <= 0) return 100f;
            if (i >= WeaponCount || i > thresholds.Length) return 0f;
            return thresholds[i - 1];
        }

        /// <summary>지금 무기에 간 금 수(0~2. 3이면 이미 다음 무기로 넘어감).</summary>
        public static int CrackCount(float hpPercent, float[] thresholds)
        {
            var weapon = Current(hpPercent, thresholds);
            if (weapon == BossWeapon.None) return Cracks;
            var top = SegmentTop(weapon, thresholds);
            var bottom = thresholds[(int)weapon];
            var size = top - bottom;
            if (size <= 1e-4f) return 0;
            var dealt = Math.Max(0f, top - hpPercent);
            var cracks = (int)Math.Floor(dealt / (size / Cracks) + 1e-4f);
            return Math.Max(0, Math.Min(Cracks - 1, cracks));
        }

        public static string Name(BossWeapon weapon)
        {
            switch (weapon)
            {
                case BossWeapon.ArmCannon: return "팔뚝 포";
                case BossWeapon.ChestArmor: return "가슴 장갑판";
                case BossWeapon.Core: return "핵";
                default: return "없음";
            }
        }
    }
}
