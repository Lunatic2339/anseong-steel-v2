using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class CockpitSeatMathTests
    {
        const float Eps = 1e-4f;

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
            Assert.AreEqual(expected.z, actual.z, Eps, "z");
        }

        [Test]
        public void RigPose_Identity_AddsOffsetInMeters()
        {
            var (position, rotation) = CockpitSeatMath.RigPose(new Vector3(0f, 55f, 0f), Quaternion.identity, new Vector3(0f, 0.4f, 1.5f));
            AssertVector(new Vector3(0f, 55.4f, 1.5f), position);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(Quaternion.identity, rotation)), Eps);
        }

        [Test]
        public void RigPose_RobotYawed_RotatesOffsetWithRobot()
        {
            var (position, rotation) = CockpitSeatMath.RigPose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), new Vector3(0f, 0f, 2f));
            AssertVector(new Vector3(2f, 0f, 0f), position);
            AssertVector(Vector3.right, rotation * Vector3.forward);
        }

        [Test]
        public void RigPose_RobotPitchedAndRolled_KeepsHorizon()
        {
            var robot = Quaternion.Euler(20f, 30f, 15f);
            var (_, rotation) = CockpitSeatMath.RigPose(Vector3.zero, robot, Vector3.zero);
            AssertVector(Vector3.up, rotation * Vector3.up);
            var flatForward = Vector3.ProjectOnPlane(robot * Vector3.forward, Vector3.up).normalized;
            AssertVector(flatForward, rotation * Vector3.forward);
        }

        [Test]
        public void YawOnly_FacingStraightDown_UsesUpAxis()
        {
            // 앞으로 90도 엎어짐: 정면이 아래, 머리 위쪽 축이 +Z.
            var rotation = CockpitSeatMath.YawOnly(Quaternion.Euler(90f, 0f, 0f));
            AssertVector(Vector3.forward, rotation * Vector3.forward);
        }

        [Test]
        public void YawOnly_FacingStraightUp_UsesNegativeUpAxis()
        {
            // 뒤로 90도 누움: 정면이 위, 머리 위쪽 축이 -Z → 원래 정면은 +Z.
            var rotation = CockpitSeatMath.YawOnly(Quaternion.Euler(-90f, 0f, 0f));
            AssertVector(Vector3.forward, rotation * Vector3.forward);
        }

        [Test]
        public void SideOffset_SeatsAreSpacingApartAndCentered()
        {
            var left = CockpitSeatMath.SideOffset(PilotSide.Left, 2.1f);
            var right = CockpitSeatMath.SideOffset(PilotSide.Right, 2.1f);
            Assert.AreEqual(2.1f, Vector3.Distance(left, right), Eps);
            AssertVector(Vector3.zero, (left + right) * 0.5f);
            Assert.Less(left.x, right.x);
        }
    }
}
