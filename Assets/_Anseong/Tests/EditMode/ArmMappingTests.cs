using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class ArmMappingTests
    {
        const float RobotLength = 29f;
        const float Reach = 0.62f;
        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);
        static readonly Quaternion PunchPose = Quaternion.Euler(10f, -20f, 80f);   // 도달 보정 때 손 회전(임의 장치 축)

        static void AssertVector(Vector3 expected, Vector3 actual, float eps, string message = "")
        {
            Assert.AreEqual(expected.x, actual.x, eps, "x " + message);
            Assert.AreEqual(expected.y, actual.y, eps, "y " + message);
            Assert.AreEqual(expected.z, actual.z, eps, "z " + message);
        }

        /// <summary>왼팔: 몸이 bodyYaw를 보고, 어깨 = 머리 + (-0.2, -0.25, 0), 가드 앞 0.3·위 0.1, 도달 앞 0.62.</summary>
        static ArmCalibration Calibrated(float bodyYaw = 0f)
        {
            var body = Quaternion.Euler(0f, bodyYaw, 0f);
            var shoulder = Head + body * new Vector3(-0.2f, -0.25f, 0f);
            var calibration = new ArmCalibration();
            Assert.AreEqual(CalibrationError.None, calibration.CaptureShoulder(Head, shoulder + Vector3.down * 0.6f));
            Assert.AreEqual(CalibrationError.None, calibration.CaptureGuard(shoulder + body * new Vector3(0.05f, 0.1f, 0.3f), Quaternion.identity));
            Assert.AreEqual(CalibrationError.None, calibration.CaptureReach(shoulder + body * new Vector3(0f, 0f, Reach), body * PunchPose));
            return calibration;
        }

        [Test]
        public void Linear_ScalesHandOffsetByArmRatio()
        {
            var c = Calibrated();
            var offset = new Vector3(0.05f, 0.1f, 0.3f);   // 가드
            Assert.IsTrue(ArmMapping.TryLinear(c, c.Shoulder + offset, RobotLength, 0f, out var robot));
            AssertVector(offset * (RobotLength / Reach), robot, 0.02f * RobotLength, "가드가 배율만큼 커진다");
            Assert.AreEqual(Vector3.Angle(offset, robot), 0f, 0.01f, "방향 그대로");
        }

        [Test]
        public void Linear_FullReach_LandsNearlyStraight()
        {
            var c = Calibrated();
            Assert.IsTrue(ArmMapping.TryLinear(c, c.ReachPoint, RobotLength, 0f, out var robot));
            AssertVector(Vector3.forward * (ArmCalibration.DefaultFullRatio * RobotLength), robot, 1e-2f);
            Assert.IsTrue(ArmMapping.TryLinear(c, c.Shoulder + Vector3.forward * 2f, RobotLength, 0f, out var far));
            Assert.LessOrEqual(far.magnitude, ArmCalibration.DefaultLimitRatio * RobotLength + 1e-3f, "더 뻗어도 한계 안");
        }

        [Test]
        public void Linear_IsMonotonicAlongReach()
        {
            var c = Calibrated();
            var last = -1f;
            for (var i = 0; i <= 150; i++)
            {
                Assert.IsTrue(ArmMapping.TryLinear(c, c.Shoulder + Vector3.forward * (i * 0.01f), RobotLength, 0f, out var robot));
                Assert.Greater(robot.magnitude, last - 1e-4f, $"{i}");
                last = robot.magnitude;
            }
        }

        [Test]
        public void ShoulderHeightOffset_LowersRobotFist()
        {
            var c = Calibrated();
            var hand = c.Shoulder + new Vector3(0.05f, 0.1f, 0.3f);
            ArmMapping.TryLinear(c, hand, RobotLength, 0f, out var baseline);
            ArmMapping.TryLinear(c, hand, RobotLength, 0.05f, out var raised);
            Assert.Less(raised.y, baseline.y, "사람 어깨 기준을 올리면 로봇 주먹이 내려간다");
        }

        [Test]
        public void BodyYaw_IsRemoved()
        {
            var c = Calibrated(bodyYaw: 90f);
            Assert.IsTrue(ArmMapping.TryLinear(c, c.ReachPoint, RobotLength, 0f, out var robot));
            Assert.Less(Vector3.Angle(Vector3.forward, robot), 0.5f, "몸 정면으로 뻗으면 로봇 정면");
        }

        [Test]
        public void NotCalibrated_ReturnsFalse()
        {
            Assert.IsFalse(ArmMapping.TryLinear(new ArmCalibration(), Vector3.one, RobotLength, 0f, out _));
            Assert.IsFalse(ArmMapping.TryGuardMatched(null, Vector3.one, RobotLength, Vector3.forward, out _));
        }

        [Test]
        public void GuardMatched_HumanGuardGoesToRobotGuard()
        {
            var c = Calibrated();
            var robotGuard = new Vector3(5f, 3f, 15f);
            Assert.IsTrue(ArmMapping.TryGuardMatched(c, c.Guard, RobotLength, robotGuard, out var robot));
            AssertVector(robotGuard, robot, 0.05f);
        }

        [Test]
        public void Fist_PunchPoseIsForwardAndBackUp()
        {
            foreach (var yaw in new[] { 0f, 35f, -80f })
            {
                var c = Calibrated(yaw);
                var fist = ArmMapping.FistLocal(c, c.ReachRotation);
                Assert.Less(Quaternion.Angle(Quaternion.identity, fist), 0.1f, $"보정 펀치 자세 = 정면 주먹 (몸 {yaw}°)");

                // 펀치 자세에서 몸 기준으로 30° 안쪽으로 돌리면 주먹도 30° 돈다.
                var body = Quaternion.Euler(0f, yaw, 0f);
                var turned = body * Quaternion.Euler(0f, 30f, 0f) * Quaternion.Inverse(body) * c.ReachRotation;
                var turnedFist = ArmMapping.FistLocal(c, turned);
                Assert.AreEqual(30f, Vector3.SignedAngle(Vector3.forward, turnedFist * Vector3.forward, Vector3.up), 0.1f);
                Assert.Less(Vector3.Angle(Vector3.up, turnedFist * Vector3.up), 0.1f, "손등은 위 그대로");
            }
        }

        // 기존 ArmMotionValidation 팔꿈치 힌트 경우.
        [Test]
        public void ElbowHint_StraightJabStaysTucked()
        {
            var hint = new ElbowLiftHint();
            for (var i = 0; i < 60; i++) hint.Update(new Vector3(0f, 0f, i / 60f * .35f), 1f / 90f);
            Assert.Less(hint.Lift, .01f);
        }

        [Test]
        public void ElbowHint_LateralHookRaisesThenRelaxes()
        {
            var hint = new ElbowLiftHint();
            for (var i = 0; i < 60; i++) hint.Update(new Vector3(i / 60f * .23f, 0f, .12f), 1f / 90f);
            Assert.Greater(hint.Lift, .8f, "옆으로 휘두르면 팔꿈치가 올라간다");
            for (var i = 0; i < 180; i++) hint.Update(Vector3.zero, 1f / 90f);
            Assert.Less(hint.Lift, .01f, "가드로 돌아오면 내려간다");
        }
    }
}
