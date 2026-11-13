using Anseong.Boss;
using Anseong.Pilot;
using Anseong.UI;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M4-06: 공격 방향은 오른쪽만, 드론 방향은 왼쪽만, 해킹되면 동료가 대신 다 봄, 1인 양팔은 다 보임.</summary>
    public class HudRulesTests
    {
        const PilotSide L = PilotSide.Left, R = PilotSide.Right;

        [Test]
        public void EachSideSeesItsOwnInfo()
        {
            Assert.IsTrue(HudRules.Visible(HudInfo.AttackSide, R, false, null));
            Assert.IsFalse(HudRules.Visible(HudInfo.AttackSide, L, false, null));
            Assert.IsTrue(HudRules.Visible(HudInfo.DroneDirection, L, false, null));
            Assert.IsFalse(HudRules.Visible(HudInfo.DroneDirection, R, false, null));
            Assert.IsTrue(HudRules.Visible(HudInfo.Shared, L, false, null));
        }

        [Test]
        public void HackedViewerBlind_PartnerSeesAll()
        {
            Assert.IsFalse(HudRules.Visible(HudInfo.AttackSide, R, false, R));
            Assert.IsFalse(HudRules.Visible(HudInfo.Shared, R, false, R));
            Assert.IsTrue(HudRules.Visible(HudInfo.AttackSide, L, false, R));
            Assert.IsTrue(HudRules.Visible(HudInfo.DroneDirection, R, false, L));
        }

        [Test]
        public void SoloSeesEverything()
        {
            Assert.IsTrue(HudRules.Visible(HudInfo.DroneDirection, R, true, R));
        }

        [Test]
        public void AttackCallout_FollowsTheCounter_NotLeftRight()
        {
            // 붙잡기(양팔 막기)·충격파(회피)에 "왼쪽/오른쪽 공격"을 띄우지 않는다. 둘 다 본다.
            var grab = HudRules.AttackCallout(BossAttackKind.Grab, BossCounter.BothGuard, R, false, false);
            StringAssert.Contains("X자", grab);
            StringAssert.DoesNotContain("오른쪽", grab);
            var wave = HudRules.AttackCallout(BossAttackKind.Shockwave, BossCounter.Dodge, R, false, false);
            StringAssert.Contains("피해라", wave);
            StringAssert.DoesNotContain("오른쪽", wave);
            StringAssert.Contains("X자", HudRules.AttackCallout(BossAttackKind.SlamBoth, BossCounter.BothGuard, R, false, false));
        }

        [Test]
        public void AttackCallout_SideOnlyForWhoSeesIt()
        {
            Assert.AreEqual("◀◀ 왼쪽 공격", HudRules.AttackCallout(BossAttackKind.PunchLeft, BossCounter.LeftGuard, L, true, true));
            Assert.AreEqual("오른쪽 공격 ▶▶", HudRules.AttackCallout(BossAttackKind.Feint, BossCounter.RightGuard, R, true, true));
            Assert.AreEqual(string.Empty, HudRules.AttackCallout(BossAttackKind.PunchLeft, BossCounter.LeftGuard, L, false, false), "왼쪽 조종사는 오는 쪽을 못 본다");
            StringAssert.Contains("연타", HudRules.AttackCallout(BossAttackKind.AlternatingCombo, BossCounter.AlternateGuard, R, true, true));
        }

        [Test]
        public void ChargeCallout_DodgeSideOnlyOnTheRight()
        {
            Assert.AreEqual("돌진! ◀◀ 왼쪽으로 피해라", HudRules.AttackCallout(BossAttackKind.Charge, BossCounter.Dodge, L, true, true));
            Assert.AreEqual("돌진! 오른쪽으로 피해라 ▶▶", HudRules.AttackCallout(BossAttackKind.Charge, BossCounter.Dodge, R, true, true));
            Assert.AreEqual("돌진! 동료가 부르는 쪽으로", HudRules.AttackCallout(BossAttackKind.Charge, BossCounter.Dodge, L, false, false));
            // 2인: 피할 쪽은 오른쪽 조종사 것.
            Assert.IsTrue(HudRules.Visible(HudInfo.DodgeSide, R, false, null));
            Assert.IsFalse(HudRules.Visible(HudInfo.DodgeSide, L, false, null));
            Assert.IsTrue(HudRules.Visible(HudInfo.DodgeSide, L, true, null), "1인 양팔은 다 보임");
        }

        [Test]
        public void DirectionWords()
        {
            Assert.AreEqual("앞", HudRules.Direction(0f, 0f));
            Assert.AreEqual("왼쪽", HudRules.Direction(-60f, 0f));
            Assert.AreEqual("오른쪽 위", HudRules.Direction(60f, 30f));
            Assert.AreEqual("뒤", HudRules.Direction(170f, 0f));
        }
    }
}
