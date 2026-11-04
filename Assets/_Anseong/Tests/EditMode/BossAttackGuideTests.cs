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
            Assert.AreEqual("왼팔 들어 막기", BossAttackGuide.Action(BossAttackKind.PunchLeft, PilotSide.Left));
            Assert.AreEqual("오른팔 들어 막기", BossAttackGuide.Action(BossAttackKind.PunchRight, PilotSide.Right));
            Assert.AreEqual("오른팔부터 번갈아 막기", BossAttackGuide.Action(BossAttackKind.AlternatingCombo, PilotSide.Right));
            Assert.AreEqual("두 팔 다 들어 막기", BossAttackGuide.Action(BossAttackKind.SlamBoth, PilotSide.Right));
            StringAssert.Contains("피하기", BossAttackGuide.Action(BossAttackKind.Charge, PilotSide.Right));
            Assert.AreEqual("좌우 연타 4번", BossAttackGuide.Name(BossAttackKind.AlternatingCombo, 4));
        }

        [Test]
        public void Combo_NextHit_CountsAndSide()
        {
            Assert.AreEqual("2/4 → 왼팔", BossAttackGuide.Next(0, 4, PilotSide.Left));
            Assert.AreEqual("4/4 → 왼팔", BossAttackGuide.Next(2, 4, PilotSide.Left));
            Assert.AreEqual(string.Empty, BossAttackGuide.Next(3, 4, PilotSide.Right), "마지막 타 뒤엔 없음");
        }
    }
}
