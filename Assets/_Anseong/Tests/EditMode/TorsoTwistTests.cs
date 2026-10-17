using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class TorsoTwistTests
    {
        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);

        /// <summary>한 팔 보정: 어깨 = 머리 + (x, -0.25, 0), 가드 앞 0.3(안쪽으로 조금), 도달 앞 0.62. 몸 정면 = +Z.</summary>
        static ArmCalibration Arm(float x)
        {
            var shoulder = Head + new Vector3(x, -0.25f, 0f);
            var c = new ArmCalibration();
            Assert.AreEqual(CalibrationError.None, c.CaptureShoulder(Head, shoulder + Vector3.down * 0.6f));
            Assert.AreEqual(CalibrationError.None, c.CaptureGuard(shoulder + new Vector3(-x * 0.25f, 0.1f, 0.3f), Quaternion.identity));
            Assert.AreEqual(CalibrationError.None, c.CaptureReach(shoulder + new Vector3(0f, 0f, 0.62f), Quaternion.identity));
            return c;
        }

        static ArmCalibration LeftArm() => Arm(-0.2f);
        static ArmCalibration RightArm() => Arm(0.2f);

        static readonly TorsoTwistSettings Settings = new TorsoTwistSettings { handWeight = 0.5f, headWeight = 0.2f, maxDegrees = 30f };

        [Test]
        public void Guard_NoTwist()
        {
            var l = LeftArm();
            var r = RightArm();
            Assert.AreEqual(0f, TorsoTwist.EstimateYaw(l, r, l.Guard, r.Guard, 0f, Settings), 1e-3f);
        }

        [Test]
        public void PullRightHandBack_TwistsRight()
        {
            var l = LeftArm();
            var r = RightArm();
            var yaw = TorsoTwist.EstimateYaw(l, r, l.Guard, r.Guard + new Vector3(0f, 0f, -0.25f), 0f, Settings);
            Assert.Greater(yaw, 5f, "오른손을 뒤로 빼면 오른쪽(+)으로 감는다");
            var mirrored = TorsoTwist.EstimateYaw(l, r, l.Guard + new Vector3(0f, 0f, -0.25f), r.Guard, 0f, Settings);
            Assert.AreEqual(-yaw, mirrored, 1e-3f, "왼손이면 반대");
        }

        [Test]
        public void HeadTurn_AddsWeightedYaw_AndClamps()
        {
            var l = LeftArm();
            var r = RightArm();
            Assert.AreEqual(0.2f * 40f, TorsoTwist.EstimateYaw(l, r, l.Guard, r.Guard, 40f, Settings), 1e-3f);
            Assert.AreEqual(30f, TorsoTwist.EstimateYaw(l, r, l.Guard + Vector3.forward * 0.4f, r.Guard + Vector3.back * 0.4f, 90f, Settings), 1e-3f);
        }

        [Test]
        public void NotCalibrated_NoTwist()
        {
            Assert.AreEqual(0f, TorsoTwist.EstimateYaw(new ArmCalibration(), new ArmCalibration(), Vector3.zero, Vector3.right, 40f, Settings));
        }

        [Test]
        public void HeadYawFromBody_IsSignedAndWrapped()
        {
            Assert.AreEqual(30f, TorsoTwist.HeadYawFromBody(Quaternion.Euler(10f, 40f, 0f), 10f), 1e-3f);
            Assert.AreEqual(-20f, TorsoTwist.HeadYawFromBody(Quaternion.Euler(0f, 170f, 0f), -170f), 1e-3f);
        }

        [Test]
        public void ShoulderShift_TurningRightMovesLeftShoulderForward()
        {
            var l = LeftArm();
            var r = RightArm();
            Assert.AreEqual(0f, TorsoTwist.ShoulderShift(l, l, r, 0f).magnitude, 1e-5f);
            var left = TorsoTwist.ShoulderShift(l, l, r, 30f);
            var right = TorsoTwist.ShoulderShift(r, l, r, 30f);
            Assert.Greater(left.z, 0.05f, "왼어깨 앞으로");
            Assert.Less(right.z, -0.05f, "오른어깨 뒤로");
            Assert.AreEqual(0f, left.y, 1e-5f);
        }

        [Test]
        public void LinearMapping_WithTwist_UsesTwistedShoulder()
        {
            var l = LeftArm();
            var r = RightArm();
            var shift = TorsoTwist.ShoulderShift(r, l, r, 30f);
            // 허리와 같이 돈 손(어깨에서 본 자세는 그대로)
            var hand = r.Shoulder + shift + new Vector3(0f, 0f, 0.3f);
            Assert.IsTrue(ArmMapping.TryLinear(r, hand, 29f, 0f, shift, out var twisted));
            Assert.IsTrue(ArmMapping.TryLinear(r, r.Shoulder + new Vector3(0f, 0f, 0.3f), 29f, 0f, out var plain));
            Assert.Less(Vector3.Distance(plain, twisted), 1e-3f, "어깨 기준 자세가 같으면 로봇 목표도 같다");
        }
    }
}
