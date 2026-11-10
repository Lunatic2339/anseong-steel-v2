using Anseong.Boss;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M4-02: 보스 무기 셋이 체력을 30씩 나눠 가짐, 금 10·20·30, 재기동 때 구간 처음.</summary>
    public class BossWeaponRulesTests
    {
        static readonly float[] T = BossWeaponRules.Thresholds(30f);

        [Test]
        public void ThresholdsFromDurability()
        {
            CollectionAssert.AreEqual(new[] { 70f, 40f, 10f }, T);
        }

        [Test]
        public void CurrentWeaponFollowsHealth()
        {
            Assert.AreEqual(BossWeapon.ArmCannon, BossWeaponRules.Current(100f, T));
            Assert.AreEqual(BossWeapon.ArmCannon, BossWeaponRules.Current(70.5f, T));
            Assert.AreEqual(BossWeapon.ChestArmor, BossWeaponRules.Current(70f, T)); // 바닥에 닿음 = 부서짐
            Assert.AreEqual(BossWeapon.Core, BossWeaponRules.Current(40f, T));
            Assert.AreEqual(BossWeapon.None, BossWeaponRules.Current(10f, T));
        }

        [Test]
        public void CracksEveryTen()
        {
            Assert.AreEqual(0, BossWeaponRules.CrackCount(100f, T));
            Assert.AreEqual(0, BossWeaponRules.CrackCount(91f, T));
            Assert.AreEqual(1, BossWeaponRules.CrackCount(90f, T));
            Assert.AreEqual(2, BossWeaponRules.CrackCount(80f, T));
            Assert.AreEqual(0, BossWeaponRules.CrackCount(70f, T)); // 다음 무기 금 0
            Assert.AreEqual(1, BossWeaponRules.CrackCount(55f, T));
            Assert.AreEqual(BossWeaponRules.Cracks, BossWeaponRules.CrackCount(5f, T));
        }

        [Test]
        public void SegmentTopForReboot()
        {
            Assert.AreEqual(100f, BossWeaponRules.SegmentTop(BossWeapon.ArmCannon, T));
            Assert.AreEqual(70f, BossWeaponRules.SegmentTop(BossWeapon.ChestArmor, T));
            Assert.AreEqual(40f, BossWeaponRules.SegmentTop(BossWeapon.Core, T));
        }
    }
}
