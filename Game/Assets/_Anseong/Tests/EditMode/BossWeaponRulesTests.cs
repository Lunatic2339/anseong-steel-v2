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

        [Test]
        public void Thresholds_PerWeaponDurability()
        {
            CollectionAssert.AreEqual(new[] { 70f, 40f, 10f }, BossWeaponRules.Thresholds(new[] { 30f, 30f, 30f }));
            var t = BossWeaponRules.Thresholds(new[] { 25f, 35f, 30f });
            Assert.AreEqual(75f, t[0], 1e-3f);
            Assert.AreEqual(42f, t[1], 1e-3f); // 내구도는 33까지(35 → 33)
            Assert.AreEqual(12f, t[2], 1e-3f);
        }

        [Test]
        public void WeaponSet_DefaultIsCannonShieldCore()
        {
            var set = UnityEngine.ScriptableObject.CreateInstance<BossWeaponSet>();
            Assert.AreEqual("팔뚝 포", set.At(0).name);
            Assert.AreEqual(BossAttackKind.Charge, set.At(0).retaliation);
            Assert.AreEqual("방패", set.At(1).name);
            Assert.AreEqual(BossAttackKind.SlamBoth, set.At(1).retaliation);
            Assert.AreEqual(2, set.At(1).parts.Length); // 방패 + 보스 검(같이 부서짐)
            Assert.IsFalse(set.At(2).retaliate);
            CollectionAssert.AreEqual(new[] { 70f, 40f, 10f }, set.Thresholds());
            Assert.IsNull(set.At(3));
            UnityEngine.Object.DestroyImmediate(set);
        }
    }
}
