using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>M2-05: 필살기 준비 판정(버튼·펴짐·각도·추적), 0.5초 시작 창, 충전 중 놓으면 처음부터, 충전 완료·광선 끝 시각.</summary>
    public class FinisherTests
    {
        const PilotSide L = PilotSide.Left;
        const PilotSide R = PilotSide.Right;

        static FinisherRules Enabled()
        {
            return new FinisherRules(new FinisherSettings()) { Enabled = true };
        }

        // ── 한 팔 준비 판정 ──

        [Test]
        public void ArmReady_NeedsAllButtonsTrackingAndExtension()
        {
            var s = new FinisherSettings();
            var straight = new Vector3(0f, 0f, 0.9f);
            Assert.IsTrue(FinisherRules.IsArmReady(straight, true, true, s));
            Assert.IsFalse(FinisherRules.IsArmReady(straight, true, false, s), "버튼 하나라도 안 누르면 아님");
            Assert.IsFalse(FinisherRules.IsArmReady(straight, false, true, s), "추적 끊기면 아님");
            Assert.IsFalse(FinisherRules.IsArmReady(new Vector3(0f, 0f, 0.8f), true, true, s), "85% 미만은 덜 뻗음");
        }

        [Test]
        public void ArmReady_MustPointForward()
        {
            var s = new FinisherSettings();
            Assert.IsTrue(FinisherRules.IsArmReady(Quaternion.Euler(0f, 30f, 0f) * new Vector3(0f, 0f, 0.9f), true, true, s), "30°는 정면");
            Assert.IsFalse(FinisherRules.IsArmReady(Quaternion.Euler(0f, 45f, 0f) * new Vector3(0f, 0f, 0.9f), true, true, s), "45°는 옆");
            Assert.IsFalse(FinisherRules.IsArmReady(new Vector3(0.95f, 0f, 0f), true, true, s), "옆으로 뻗기는 아님");
        }

        // ── 시작 창 ──

        [Test]
        public void Start_WithinHalfSecond_Charges()
        {
            var f = Enabled();
            f.ArmReady(L, true, 1.0);
            Assert.AreEqual(FinisherStep.None, f.Update(1.0), "한 팔만으로는 시작 안 함");
            f.ArmReady(R, true, 1.5);
            Assert.AreEqual(FinisherStep.ChargeStarted, f.Update(1.5), "정확히 0.5초 차는 허용");
            Assert.IsTrue(f.Charging);
        }

        [Test]
        public void Start_TooFarApart_DoesNotCharge_UntilReraised()
        {
            var f = Enabled();
            f.ArmReady(L, true, 1.0);
            f.ArmReady(R, true, 1.6);
            Assert.AreEqual(FinisherStep.None, f.Update(1.6), "0.6초 차는 따로 뻗음");
            Assert.AreEqual(FinisherStep.None, f.Update(3.0), "계속 들고 있어도 시작 안 함");
            f.ArmReady(L, false, 3.0);
            f.ArmReady(L, true, 3.2);
            Assert.AreEqual(FinisherStep.None, f.Update(3.2), "오른팔은 1.6부터 들고 있어서 여전히 차가 큼");
            f.ArmReady(R, false, 3.3);
            f.ArmReady(R, true, 3.4);
            Assert.AreEqual(FinisherStep.ChargeStarted, f.Update(3.4), "둘 다 다시 함께 뻗으면 시작");
        }

        [Test]
        public void Disabled_BeforeKneel_DoesNothing()
        {
            var f = new FinisherRules();
            f.ArmReady(L, true, 1.0);
            f.ArmReady(R, true, 1.0);
            Assert.AreEqual(FinisherStep.None, f.Update(1.0));
            Assert.IsFalse(f.Charging);
        }

        // ── 충전 ──

        [Test]
        public void ReleaseDuringCharge_ResetsFromStart()
        {
            var f = Enabled();
            f.ArmReady(L, true, 1.0);
            f.ArmReady(R, true, 1.0);
            Assert.AreEqual(FinisherStep.ChargeStarted, f.Update(1.0));
            Assert.AreEqual(FinisherStep.None, f.Update(2.0));
            f.ArmReady(R, false, 2.2);
            Assert.AreEqual(FinisherStep.ChargeReset, f.Update(2.2));
            Assert.IsFalse(f.Charging);
            Assert.AreEqual(0f, f.Progress(2.2));

            f.ArmReady(R, true, 2.4);
            Assert.AreEqual(FinisherStep.None, f.Update(2.4), "왼팔은 1.0부터 들고 있어서 0.5초 창 밖");
            f.ArmReady(L, false, 2.5);
            f.ArmReady(L, true, 2.6);
            Assert.AreEqual(FinisherStep.ChargeStarted, f.Update(2.6));
            Assert.AreEqual(FinisherStep.None, f.Update(3.9), "1.3초: 아직");
            Assert.AreEqual(FinisherStep.Fired, f.Update(4.11), "처음부터 다시 1.5초");
        }

        [Test]
        public void FullCharge_FiresThenFinishesAfterBeam()
        {
            var f = Enabled();
            f.ArmReady(L, true, 10.0);
            f.ArmReady(R, true, 10.3);
            Assert.AreEqual(FinisherStep.ChargeStarted, f.Update(10.3));
            Assert.AreEqual(0.5f, f.Progress(11.05), 1e-3f, "충전 시작은 늦게 뻗은 팔 기준");
            Assert.AreEqual(FinisherStep.None, f.Update(11.7));
            Assert.AreEqual(FinisherStep.Fired, f.Update(11.81));
            Assert.IsTrue(f.Firing);

            f.ArmReady(L, false, 12.0);
            Assert.AreEqual(FinisherStep.None, f.Update(12.0), "발사 뒤에는 놓아도 됨");
            Assert.AreEqual(FinisherStep.None, f.Update(14.7));
            Assert.AreEqual(FinisherStep.Finished, f.Update(14.82), "광선 3초 뒤 끝");
            Assert.IsTrue(f.Done);
            Assert.AreEqual(FinisherStep.None, f.Update(20.0), "한 번만");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var f = Enabled();
            f.ArmReady(L, true, 1.0);
            f.ArmReady(R, true, 1.0);
            f.Update(1.0);
            f.Reset();
            Assert.IsFalse(f.Enabled);
            Assert.IsFalse(f.Charging);
            Assert.IsFalse(f.IsReady(L));
            Assert.IsFalse(f.IsReady(R));
        }
    }
}
