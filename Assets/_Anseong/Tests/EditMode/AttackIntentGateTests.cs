using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class AttackIntentGateTests
    {
        [Test]
        public void GripWhileTracked_HasIntent()
        {
            var gate = new AttackIntentGate();
            Assert.IsFalse(gate.Update(true, false, 0d));
            Assert.IsTrue(gate.Update(true, true, 0.1d));
        }

        [Test]
        public void TrackingLost_NoIntentEvenWithGrip()
        {
            var gate = new AttackIntentGate();
            gate.Update(true, true, 0d);
            Assert.IsFalse(gate.Update(false, true, 0.1d));
            Assert.IsTrue(gate.IsTrackingLost);
        }

        [Test]
        public void Recovered_StaysInvalidUntilGripReleasedOnce()
        {
            var gate = new AttackIntentGate();
            gate.Update(true, true, 0d);
            gate.Update(false, true, 0.1d);

            Assert.IsFalse(gate.Update(true, true, 0.2d), "복구 직후 그립을 쥔 채면 무효");
            Assert.IsTrue(gate.AwaitingRelease);
            Assert.IsFalse(gate.Update(true, true, 0.3d));
            Assert.IsFalse(gate.Update(true, false, 0.4d), "놓는 프레임");
            Assert.IsTrue(gate.Update(true, true, 0.5d), "다시 쥐면 유효");
        }

        [Test]
        public void LostWithoutGrip_RecoversImmediately()
        {
            var gate = new AttackIntentGate();
            gate.Update(false, false, 0d);
            Assert.IsFalse(gate.Update(true, false, 0.1d));
            Assert.IsTrue(gate.Update(true, true, 0.2d));
        }

        [Test]
        public void LostSeconds_MeasuresFromFirstLostFrame_AndClearsOnRecovery()
        {
            // 짧은 끊김을 연계 초기화와 분리: 게이트는 시간만 알려 주고 연계는 건드리지 않는다.
            var gate = new AttackIntentGate();
            gate.Update(true, true, 1d);
            gate.Update(false, true, 1.1d);
            gate.Update(false, true, 1.2d);
            Assert.AreEqual(0.25d, gate.LostSeconds(1.35d), 1e-9);
            gate.Update(true, true, 1.4d);
            Assert.AreEqual(0d, gate.LostSeconds(2d));
        }

        [Test]
        public void RequireRelease_BlocksUntilReleased()
        {
            var gate = new AttackIntentGate();
            Assert.IsTrue(gate.Update(true, true, 0d));
            gate.RequireRelease();
            Assert.IsFalse(gate.Update(true, true, 0.1d));
            gate.Update(true, false, 0.2d);
            Assert.IsTrue(gate.Update(true, true, 0.3d));
        }
    }
}
