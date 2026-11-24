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
        public void DirectionWords()
        {
            Assert.AreEqual("앞", HudRules.Direction(0f, 0f));
            Assert.AreEqual("왼쪽", HudRules.Direction(-60f, 0f));
            Assert.AreEqual("오른쪽 위", HudRules.Direction(60f, 30f));
            Assert.AreEqual("뒤", HudRules.Direction(170f, 0f));
        }
    }
}
