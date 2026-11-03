using Anseong.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class GuardRulesTests
    {
        static readonly Vector3 Guard = new Vector3(0.2f, 1.5f, 0.3f);
        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);

        static GuardInput Input(Vector3 hand, Vector3 forearm, bool tracked = true, bool was = false)
        {
            return new GuardInput
            {
                Calibrated = true, Tracked = tracked, Hand = hand, Guard = Guard, Head = Head,
                Body = Quaternion.identity, HumanForearm = forearm, RobotForearm = forearm, WasGuarding = was,
            };
        }

        static readonly Vector3 Up = Vector3.up;

        [Test]
        public void AtCalibratedGuard_ForearmUp_IsGuarding()
        {
            var r = GuardRules.Evaluate(Input(Guard, Up), new GuardSettings());
            Assert.IsTrue(r.IsGuarding);
            Assert.AreEqual(GuardReason.Guarding, r.Reason);
        }

        [Test]
        public void ZoneEdge_InsideAt24cm_OutsideAt26cm()
        {
            var s = new GuardSettings();
            Assert.IsTrue(GuardRules.Evaluate(Input(Guard + Vector3.right * 0.24f, Up), s).IsGuarding);
            Assert.AreEqual(GuardReason.OutOfZone, GuardRules.Evaluate(Input(Guard + Vector3.right * 0.26f, Up), s).Reason);
            Assert.AreEqual(GuardReason.OutOfZone, GuardRules.Evaluate(Input(Guard + Vector3.down * 0.26f, Up), s).Reason);
        }

        [Test]
        public void HandBehindFace_Fails()
        {
            // 영역 안이지만 얼굴보다 뒤(가드 z 0.3에서 0.25 뒤 = 0.05 → 기준 0.05 미만으로).
            var r = GuardRules.Evaluate(Input(Guard + Vector3.back * 0.26f, Up), new GuardSettings { zoneHalfSize = Vector3.one * 0.3f });
            Assert.AreEqual(GuardReason.BehindFace, r.Reason);
        }

        [Test]
        public void ForearmTilt_InsideAt39_OutsideAt41()
        {
            var s = new GuardSettings();
            var at39 = Quaternion.AngleAxis(39f, Vector3.forward) * Up;
            var at41 = Quaternion.AngleAxis(41f, Vector3.forward) * Up;
            Assert.IsTrue(GuardRules.Evaluate(Input(Guard, at39), s).IsGuarding);
            Assert.AreEqual(GuardReason.ForearmTilted, GuardRules.Evaluate(Input(Guard, at41), s).Reason);
        }

        [Test]
        public void ReleaseMargin_KeepsGuardSlightlyOutside()
        {
            var s = new GuardSettings();
            var hand = Guard + Vector3.right * 0.27f;
            Assert.IsFalse(GuardRules.Evaluate(Input(hand, Up), s).IsGuarding);
            Assert.IsTrue(GuardRules.Evaluate(Input(hand, Up, was: true), s).IsGuarding);
            var at43 = Quaternion.AngleAxis(43f, Vector3.forward) * Up;
            Assert.IsTrue(GuardRules.Evaluate(Input(Guard, at43, was: true), s).IsGuarding);
        }

        [Test]
        public void Untracked_AndNotCalibrated_Fail()
        {
            Assert.AreEqual(GuardReason.Untracked, GuardRules.Evaluate(Input(Guard, Up, tracked: false), new GuardSettings()).Reason);
            var input = Input(Guard, Up);
            input.Calibrated = false;
            Assert.AreEqual(GuardReason.NotCalibrated, GuardRules.Evaluate(input, new GuardSettings()).Reason);
        }

        [Test]
        public void ForearmSource_HumanRobotEither()
        {
            var input = Input(Guard, Up);
            input.RobotForearm = Vector3.forward; // 로봇 전완은 누움(90°)
            Assert.IsTrue(GuardRules.Evaluate(input, new GuardSettings { forearmSource = ForearmSource.Human }).IsGuarding);
            Assert.IsFalse(GuardRules.Evaluate(input, new GuardSettings { forearmSource = ForearmSource.Robot }).IsGuarding);
            Assert.IsTrue(GuardRules.Evaluate(input, new GuardSettings { forearmSource = ForearmSource.Either }).IsGuarding);
            input.RobotForearm = Vector3.zero; // 모름
            Assert.IsTrue(float.IsNaN(GuardRules.Evaluate(input, new GuardSettings()).RobotTilt));
        }

        [Test]
        public void BodyYaw_ZoneIsInBodyFrame()
        {
            // 몸이 90° 돌아 있으면 몸 오른쪽(+x)은 트래킹 공간 -z 방향.
            var input = Input(Guard + Vector3.back * 0.2f, Up);
            input.Body = Quaternion.Euler(0f, 90f, 0f);
            input.Head = Guard + Vector3.left * 0.3f; // 얼굴은 몸 정면(+x) 뒤쪽
            var r = GuardRules.Evaluate(input, new GuardSettings());
            Assert.IsTrue(r.IsGuarding);
            Assert.AreEqual(0.2f, r.Offset.x, 1e-4f);
        }

        [Test]
        public void HumanForearm_AtReachPose_IsBodyForward_AndRotatesWithController()
        {
            var reach = Quaternion.Euler(10f, 20f, 30f);
            var body = Quaternion.Euler(0f, 45f, 0f);
            var atReach = GuardRules.HumanForearm(reach, reach, body);
            Assert.Less(Vector3.Angle(body * Vector3.forward, atReach), 0.01f);
            // 펀치 자세에서 손목을 위로 90° 세우면(몸 오른쪽 축 기준) 전완은 위.
            var raised = Quaternion.AngleAxis(-90f, body * Vector3.right) * reach;
            Assert.Less(GuardRules.Tilt(GuardRules.HumanForearm(raised, reach, body)), 0.1f);
        }

        [Test]
        public void BothArms_RequireBoth()
        {
            var on = new GuardResult { IsGuarding = true };
            var off = new GuardResult();
            Assert.IsTrue(GuardRules.BothGuarding(on, on));
            Assert.IsFalse(GuardRules.BothGuarding(on, off));
        }
    }
}
