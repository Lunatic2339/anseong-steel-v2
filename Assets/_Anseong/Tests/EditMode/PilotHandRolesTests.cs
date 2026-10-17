using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class PilotHandRolesTests
    {
        [Test]
        public void TwoPilotMode_ArmHandIsNotMoveHand()
        {
            // 기존 버그 7: 이동 스틱이 펀치하는 손이면 안 된다.
            foreach (var arm in new[] { PilotSide.Left, PilotSide.Right })
            foreach (var hand in new[] { PilotSide.Left, PilotSide.Right })
            {
                var isArm = PilotHandRoles.IsArmHand(hand, arm, false);
                var isMove = PilotHandRoles.IsMoveHand(hand, arm, false);
                Assert.AreEqual(hand == arm, isArm, $"arm {arm}, hand {hand}");
                Assert.AreNotEqual(isArm, isMove, $"arm {arm}, hand {hand}");
            }
        }

        [Test]
        public void BothArmsMode_BothHandsAreArmAndMove()
        {
            foreach (var arm in new[] { PilotSide.Left, PilotSide.Right })
            foreach (var hand in new[] { PilotSide.Left, PilotSide.Right })
            {
                Assert.IsTrue(PilotHandRoles.IsArmHand(hand, arm, true));
                Assert.IsTrue(PilotHandRoles.IsMoveHand(hand, arm, true));
            }
        }
    }
}
