using Anseong.Boss;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M2-06: 보스 공격 안내 글자(무슨 공격·어느 팔로 무엇을·연타 다음 타).</summary>
    public class BossAttackGuideTests
    {
        [Test]
        public void EachAttack_SaysWhatToDo()
        {
            // 1인 양팔 글: 판정 규칙대로(한쪽 공격은 왼팔 방패를 그쪽으로, 내려찍기는 X자).
            Assert.AreEqual("방패(왼팔)를 왼쪽으로 들어 막기", BossAttackGuide.Action(BossAttackKind.PunchLeft, PilotSide.Left));
            Assert.AreEqual("방패(왼팔)를 오른쪽으로 들어 막기", BossAttackGuide.Action(BossAttackKind.PunchRight, PilotSide.Right));
            Assert.AreEqual("오른쪽부터 번갈아 방패(왼팔)로 막기", BossAttackGuide.Action(BossAttackKind.AlternatingCombo, PilotSide.Right));
            Assert.AreEqual("두 팔을 들어 X자로 막기", BossAttackGuide.Action(BossAttackKind.SlamBoth, PilotSide.Right));
            StringAssert.Contains("피하기", BossAttackGuide.Action(BossAttackKind.Charge, PilotSide.Right));
            StringAssert.Contains("오른쪽", BossAttackGuide.Action(BossAttackKind.Charge, PilotSide.Right), "돌진은 피할 쪽을 말한다");
            Assert.AreEqual("좌우 연타 4번", BossAttackGuide.Name(BossAttackKind.AlternatingCombo, 4));
        }

        [Test]
        public void NoText_TellsRightArmToBlock()
        {
            // 규칙은 왼팔 방패만 인정(오른팔은 막기를 맡지 않는다) → "오른팔 들어 막기" 같은 글이 없어야 한다.
            foreach (BossAttackKind kind in System.Enum.GetValues(typeof(BossAttackKind)))
            foreach (var side in new[] { PilotSide.Left, PilotSide.Right })
            foreach (var viewer in new[] { PilotSide.Left, PilotSide.Right })
            foreach (var both in new[] { false, true })
            foreach (var sees in new[] { false, true })
            {
                var text = BossAttackGuide.Action(kind, BossAttackSpec.Default(kind).counter, side, viewer, both, sees);
                Assert.IsNotEmpty(text, $"{kind}");
                StringAssert.DoesNotContain("오른팔", text, $"{kind}");
            }
        }

        [Test]
        public void TwoPilots_OnlyTheOneWhoSeesGetsTheSide()
        {
            const PilotSide L = PilotSide.Left, R = PilotSide.Right;
            // 왼쪽 조종사(못 봄): 쪽이 드러나지 않는다(이름도).
            var blind = BossAttackGuide.Action(BossAttackKind.PunchRight, BossCounter.RightGuard, R, L, false, false);
            StringAssert.Contains("동료가 부르는 쪽", blind);
            StringAssert.DoesNotContain("오른쪽", blind);
            Assert.AreEqual("펀치", BossAttackGuide.Name(BossAttackKind.PunchRight, 1, false));
            Assert.AreEqual("오른손 펀치", BossAttackGuide.Name(BossAttackKind.PunchRight, 1, true));
            // 오른쪽 조종사(봄): 쪽을 보고 불러 준다.
            var caller = BossAttackGuide.Action(BossAttackKind.PunchLeft, BossCounter.LeftGuard, L, R, false, true);
            StringAssert.Contains("왼쪽", caller);
            StringAssert.Contains("불러", caller);
            // 돌진: 못 보는 사람에게는 피할 쪽이 없다.
            var charge = BossAttackGuide.Action(BossAttackKind.Charge, BossCounter.Dodge, L, L, false, false);
            StringAssert.DoesNotContain("왼쪽", charge);
            StringAssert.Contains("동료가 부르는 쪽", charge);
            StringAssert.Contains("왼쪽", BossAttackGuide.Action(BossAttackKind.Charge, BossCounter.Dodge, L, R, false, true));
            // 붙잡기는 양팔 막기, 충격파는 회피(쪽 없음).
            StringAssert.Contains("X자", BossAttackGuide.Action(BossAttackKind.Grab, BossCounter.BothGuard, R, L, false, false));
            StringAssert.Contains("피하기", BossAttackGuide.Action(BossAttackKind.Shockwave, BossCounter.Dodge, R, R, false, true));
        }

        [Test]
        public void Combo_NextHit_CountsAndSide()
        {
            Assert.AreEqual("2/4 → 왼쪽", BossAttackGuide.Next(0, 4, PilotSide.Left));
            Assert.AreEqual("4/4 → 왼쪽", BossAttackGuide.Next(2, 4, PilotSide.Left));
            Assert.AreEqual("2/4", BossAttackGuide.Next(0, 4, PilotSide.Left, false), "쪽을 못 보는 사람은 몇 번째인지만");
            Assert.AreEqual(string.Empty, BossAttackGuide.Next(3, 4, PilotSide.Right), "마지막 타 뒤엔 없음");
        }
    }
}
