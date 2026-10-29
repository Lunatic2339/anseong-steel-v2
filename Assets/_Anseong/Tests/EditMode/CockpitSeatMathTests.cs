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
        public void RigPose_RootMovedAndYawed_FollowsRootPositionAndYaw()
        {
            var root = new Vector3(10f, 0f, 5f);
            var (position, rotation) = CockpitSeatMath.RigPose(root, Quaternion.Euler(0f, 90f, 0f), new Vector3(0f, 55.9f, 1f));
            AssertVector(new Vector3(11f, 55.9f, 5f), position);
            AssertVector(Vector3.right, rotation * Vector3.forward);
            AssertVector(Vector3.up, rotation * Vector3.up);
        }

        [Test]
        public void RigPose_RootTilted_SeatStaysLevelAboveRoot()
        {
            // 루트가 앞으로·옆으로 기울어도 오프셋은 yaw만 받아 기울지 않는다(높은 좌석이 크게 휘두르지 않음).
            var root = new Vector3(3f, 0f, -2f);
            var tilted = Quaternion.Euler(15f, 40f, 10f);
            var (position, rotation) = CockpitSeatMath.RigPose(root, tilted, new Vector3(0f, 55.9f, 0f));
            AssertVector(new Vector3(3f, 55.9f, -2f), position);
            AssertVector(Vector3.up, rotation * Vector3.up);
            AssertVector(Quaternion.Euler(0f, 40f, 0f) * Vector3.forward, rotation * Vector3.forward);
        }

        [Test]
        public void FollowOffset_ZeroWeight_StaysFixed()
        {
            var offset = CockpitSeatMath.FollowOffset(Vector3.zero, Quaternion.identity, new Vector3(0.3f, 47.6f, 1.4f), new Vector3(0f, 47.5f, 1.2f), 0f, 1f);
            AssertVector(Vector3.zero, offset);
        }

        [Test]
        public void FollowOffset_FollowsBoneDeltaInRobotFrame()
        {
            // 로봇이 90° 돌아 있어도 본의 움직임을 로봇 정면 기준으로 돌려준다(RigPose가 다시 yaw를 곱한다).
            var root = new Vector3(5f, 0f, 5f);
            var yaw = Quaternion.Euler(0f, 90f, 0f);
            var reference = new Vector3(0f, 47.5f, 1.2f);
            var bone = root + yaw * (reference + new Vector3(0.1f, -0.2f, 0.05f));
            AssertVector(new Vector3(0.05f, -0.1f, 0.025f), CockpitSeatMath.FollowOffset(root, yaw, bone, reference, 0.5f, 1f));
        }

        [Test]
        public void FollowOffset_ClampedToMax()
        {
            var offset = CockpitSeatMath.FollowOffset(Vector3.zero, Quaternion.identity, new Vector3(0f, 50f, 0f), new Vector3(0f, 45f, 0f), 1f, 1f);
            Assert.AreEqual(1f, offset.magnitude, Eps);
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

        [Test]
        public void AlignedOrigin_BodyForwardBecomesSeatForward_HeadAtSeatCenter()
        {
            // 트래킹 공간에서 몸 정면이 오른쪽 20°, 머리가 (0.3, 1.7, -0.2)에 있었다.
            var seat = new Vector3(-1.05f, 0f, 0f);
            var head = new Vector3(0.3f, 1.7f, -0.2f);
            var (position, rotation) = CockpitSeatMath.AlignedOrigin(seat, 20f, head);
            var bodyForward = rotation * (Quaternion.Euler(0f, 20f, 0f) * Vector3.forward);
            AssertVector(Vector3.forward, bodyForward);
            var headInSeat = position + rotation * head;
            AssertVector(new Vector3(seat.x, 1.7f, seat.z), headInSeat);
        }

        [Test]
        public void AttachedPose_AtReferencePose_EqualsRigPose()
        {
            var root = new Vector3(3f, 0f, 1f);
            var robot = Quaternion.Euler(0f, 40f, 0f);
            var offset = new Vector3(0.5f, 55f, 1.2f);
            var reference = new Vector3(0f, 54f, 0.3f);
            var referenceRotation = Quaternion.Euler(5f, 0f, 0f);
            var bonePosition = root + robot * reference;
            var boneRotation = robot * referenceRotation;
            var (position, rotation) = CockpitSeatMath.AttachedPose(root, robot, offset, bonePosition, boneRotation, reference, referenceRotation, false);
            var (rigPosition, rigRotation) = CockpitSeatMath.RigPose(root, robot, offset);
            AssertVector(rigPosition, position);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(rigRotation, rotation)), Eps);
        }

        [Test]
        public void AttachedPose_BoneYaws_SeatTurnsAroundBone()
        {
            // 본이 제자리에서 90도 돌면 본 앞 1m에 있던 좌석은 본 오른쪽 1m로 옮겨 가고 같이 돈다.
            var reference = new Vector3(0f, 50f, 0f);
            var offset = new Vector3(0f, 50f, 1f);
            var (position, rotation) = CockpitSeatMath.AttachedPose(Vector3.zero, Quaternion.identity, offset,
                reference, Quaternion.Euler(0f, 90f, 0f), reference, Quaternion.identity, false);
            AssertVector(new Vector3(1f, 50f, 0f), position);
            AssertVector(Vector3.right, rotation * Vector3.forward);
        }

        [Test]
        public void AttachedPose_BoneMoves_SeatMovesSameAmount()
        {
            var reference = new Vector3(0f, 50f, 0f);
            var offset = new Vector3(0f, 50f, 1f);
            var (position, _) = CockpitSeatMath.AttachedPose(Vector3.zero, Quaternion.identity, offset,
                reference + new Vector3(0f, -0.5f, 2f), Quaternion.identity, reference, Quaternion.identity, false);
            AssertVector(new Vector3(0f, 49.5f, 3f), position);
        }

        [Test]
        public void AttachedPose_FollowYawOnly_DropsPitch()
        {
            var reference = new Vector3(0f, 50f, 0f);
            var offset = new Vector3(0f, 50f, 1f);
            var (position, rotation) = CockpitSeatMath.AttachedPose(Vector3.zero, Quaternion.identity, offset,
                reference, Quaternion.Euler(30f, 0f, 0f), reference, Quaternion.identity, true);
            AssertVector(offset, position);
            AssertVector(Vector3.up, rotation * Vector3.up);
        }
    }
}
