using Anseong.Boss;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M4-03: 상황이 무기를 고름, 방패 하나로 양쪽 막기, 무기 연계 = 번갈아 세 번.</summary>
    public class WeaponRulesTests
    {
        [Test]
        public void LoadoutFollowsScene()
        {
            Assert.AreEqual(Loadout.Bare, WeaponRules.For(MatchScene.Checkup, false));
            Assert.AreEqual(Loadout.Ranged, WeaponRules.For(MatchScene.DroneHunt, false));
            Assert.AreEqual(Loadout.Ranged, WeaponRules.For(MatchScene.Approach, false));
            Assert.AreEqual(Loadout.Melee, WeaponRules.For(MatchScene.FightCannon, false));
            Assert.AreEqual(Loadout.Ranged, WeaponRules.For(MatchScene.FightCannon, true));
            Assert.AreEqual(Loadout.Melee, WeaponRules.For(MatchScene.FightArmor, true)); // 포를 잃은 뒤엔 드론이 와도 근접
            Assert.AreEqual(Loadout.Bare, WeaponRules.For(MatchScene.FightCore, false));
            Assert.AreEqual(Loadout.Melee, WeaponRules.For(MatchScene.Rebooting, false, MatchScene.FightArmor));
        }

        [Test]
        public void ShieldCoversEitherSide()
        {
            // 왼팔이 오른쪽을 덮으면 오른쪽 펀치를 막는다.
            Assert.AreEqual(DefenseResult.Blocked, DefenseRules.JudgeShield(BossCounter.RightGuard, PilotSide.Right, true, PilotSide.Right, false, false));
            // 반대쪽을 덮고 있으면 맞는다.
            Assert.AreEqual(DefenseResult.Hit, DefenseRules.JudgeShield(BossCounter.LeftGuard, PilotSide.Left, true, PilotSide.Right, false, false));
            // 오른팔은 막기를 맡지 않는다.
            Assert.AreEqual(DefenseResult.Hit, DefenseRules.JudgeShield(BossCounter.RightGuard, PilotSide.Right, false, PilotSide.Right, true, false));
            // 내려찍기는 그대로 두 팔.
            Assert.AreEqual(DefenseResult.Blocked, DefenseRules.JudgeShield(BossCounter.BothGuard, PilotSide.Left, true, PilotSide.Left, true, false));
            Assert.AreEqual(DefenseResult.PartialBlock, DefenseRules.JudgeShield(BossCounter.BothGuard, PilotSide.Left, true, PilotSide.Left, false, false));
        }

        [Test]
        public void CoverSideFromHandPosition()
        {
            Assert.AreEqual(PilotSide.Left, GuardRules.CoverSide(new GuardResult { SideOfFace = -0.1f }));
            Assert.AreEqual(PilotSide.Right, GuardRules.CoverSide(new GuardResult { SideOfFace = 0.1f }));
        }

        [Test]
        public void WeaponComboIsAnyThreeAlternating()
        {
            var combo = new ComboRules { AnyKind = true };
            Assert.AreEqual(1, combo.OnHit(PilotSide.Right, PunchKind.Hook, 0).Step);
            Assert.AreEqual(2, combo.OnHit(PilotSide.Left, PunchKind.Uppercut, 0.5).Step);
            Assert.IsTrue(combo.OnHit(PilotSide.Right, PunchKind.Jab, 1.0).Finished);
        }
    }
}
