using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class ArmCalibrationTests
    {
        const float Eps = 1e-3f;
        static readonly Vector3 Head = new Vector3(0f, 1.6f, 0f);

        static void AssertVector(Vector3 expected, Vector3 actual, float eps = Eps)
        {
            Assert.AreEqual(expected.x, actual.x, eps, "x");
            Assert.AreEqual(expected.y, actual.y, eps, "y");
            Assert.AreEqual(expected.z, actual.z, eps, "z");
        }

        /// <summary>몸이 bodyYaw(°)를 보고 서 있을 때 몸 기준 점을 트래킹 공간으로.</summary>
        static Vector3 Body(float bodyYaw, Vector3 shoulder, Vector3 local)
        {
            return shoulder + Quaternion.Euler(0f, bodyYaw, 0f) * local;
        }

        /// <summary>왼팔: 어깨 (-0.2, 1.35, 0), 내린 손 0.6 아래, 가드 앞 0.3·위 0.1, 도달 앞 0.62.</summary>
        static ArmCalibration Calibrated(float bodyYaw = 0f, Vector3? headPosition = null)
        {
            var head = headPosition ?? Head;
            var shoulder = head + Quaternion.Euler(0f, bodyYaw, 0f) * new Vector3(-0.2f, -0.25f, 0f);
            var calibration = new ArmCalibration();
            Assert.AreEqual(CalibrationError.None, calibration.CaptureShoulder(head, shoulder + Vector3.down * 0.6f));
            Assert.AreEqual(CalibrationError.None, calibration.CaptureGuard(Body(bodyYaw, shoulder, new Vector3(0.05f, 0.1f, 0.3f)), Quaternion.identity));
            Assert.AreEqual(CalibrationError.None, calibration.CaptureReach(Body(bodyYaw, shoulder, new Vector3(0f, 0f, 0.62f)), Quaternion.identity));
            return calibration;
        }

        [Test]
        public void Capture_StoresShoulderArmLengthGuardReach()
        {
            var c = Calibrated();
            Assert.IsTrue(c.IsComplete);
            AssertVector(new Vector3(-0.2f, 1.35f, 0f), c.Shoulder);
            Assert.AreEqual(0.6f, c.ArmLength, Eps);
            Assert.AreEqual(0.62f, c.EffectiveReach, Eps);
            Assert.AreEqual(Mathf.Sqrt(0.05f * 0.05f + 0.1f * 0.1f + 0.3f * 0.3f) / 0.62f, c.GuardRatio, Eps);
            Assert.AreEqual(0f, c.BodyYawDegrees, 0.1f);
        }

        [Test]
        public void BodyYaw_ComesFromReachNotHead()
        {
            // 몸은 +X(90°)를 보고, 고개는 어디를 보든 상관없다(머리 회전을 입력으로 받지 않는다).
            var c = Calibrated(90f);
            Assert.AreEqual(90f, c.BodyYawDegrees, 0.1f);
            AssertVector(new Vector3(0f, 0f, 0.62f), c.ToBody(c.ReachPoint));
            AssertVector(new Vector3(0.05f, 0.1f, 0.3f), c.ToBody(c.Guard));
        }

        [Test]
        public void ShoulderCapture_RejectsArmNotHanging()
        {
            var c = new ArmCalibration();
            Assert.AreEqual(CalibrationError.ArmNotHanging, c.CaptureShoulder(Head, new Vector3(-0.2f, 1.3f, 0.3f)), "가드 높이의 손");
            Assert.AreEqual(CalibrationError.ArmNotHanging, c.CaptureShoulder(Head, new Vector3(-0.8f, 0.8f, 0f)), "머리에서 너무 먼 손");
            Assert.IsFalse(c.HasShoulder);
        }

        [Test]
        public void Capture_RejectsNaNPose()
        {
            var c = new ArmCalibration();
            var nan = new Vector3(float.NaN, 0f, 0f);
            Assert.AreEqual(CalibrationError.InvalidPose, c.CaptureShoulder(Head, nan));
            Assert.AreEqual(CalibrationError.None, c.CaptureShoulder(Head, new Vector3(-0.2f, 0.75f, 0f)));
            Assert.AreEqual(CalibrationError.InvalidPose, c.CaptureGuard(nan, Quaternion.identity));
            Assert.AreEqual(CalibrationError.InvalidPose, c.CaptureGuard(new Vector3(-0.2f, 1.4f, 0.3f), new Quaternion(0f, 0f, 0f, 0f)));
        }

        [Test]
        public void Capture_RequiresOrder()
        {
            var c = new ArmCalibration();
            Assert.AreEqual(CalibrationError.ShoulderMissing, c.CaptureGuard(new Vector3(0f, 1.4f, 0.3f), Quaternion.identity));
            Assert.AreEqual(CalibrationError.GuardMissing, c.CaptureReach(new Vector3(0f, 1.35f, 0.6f), Quaternion.identity));
        }

        [Test]
        public void ReachCapture_RejectsUnextendedHand()
        {
            // 기존 ArmMotionValidation: "reach calibration rejects unextended hand"
            var c = new ArmCalibration();
            c.CaptureShoulder(Head, new Vector3(-0.2f, 0.75f, 0f));
            c.CaptureGuard(new Vector3(-0.2f, 1.35f, 0.3f), Quaternion.identity);
            Assert.AreEqual(CalibrationError.ReachTooShort, c.CaptureReach(new Vector3(-0.2f, 1.35f, 0.33f), Quaternion.identity));
            Assert.IsFalse(c.IsComplete);
        }

        [Test]
        public void ReachCapture_RejectsStraightUp()
        {
            var c = new ArmCalibration();
            c.CaptureShoulder(Head, new Vector3(-0.2f, 0.75f, 0f));
            c.CaptureGuard(new Vector3(-0.2f, 1.35f, 0.3f), Quaternion.identity);
            Assert.AreEqual(CalibrationError.ReachNotForward, c.CaptureReach(new Vector3(-0.2f, 1.95f, 0.05f), Quaternion.identity));
        }

        [Test]
        public void Recalibrating_ChangesValues()
        {
            var first = Calibrated();
            var shoulderBefore = first.Shoulder;
            // 다시 어깨부터: 이전 가드·도달은 지워진다.
            Assert.AreEqual(CalibrationError.None, first.CaptureShoulder(Head + Vector3.up * 0.1f, new Vector3(-0.22f, 0.8f, 0f)));
            Assert.IsFalse(first.HasGuard);
            Assert.IsFalse(first.IsComplete);
            Assert.AreNotEqual(shoulderBefore, first.Shoulder);
        }

        [Test]
        public void MapRadius_EndpointsAndGuard()
        {
            const float guard = 0.5f, modelGuard = 0.4f;
            Assert.AreEqual(0f, ArmCalibration.MapRadius(0f, guard, modelGuard), 1e-6f);
            Assert.AreEqual(modelGuard, ArmCalibration.MapRadius(guard, guard, modelGuard), 1e-5f);
            Assert.AreEqual(ArmCalibration.DefaultFullRatio, ArmCalibration.MapRadius(1f, guard, modelGuard), 1e-5f);
            Assert.Less(ArmCalibration.MapRadius(5f, guard, modelGuard), ArmCalibration.DefaultLimitRatio + 1e-6f);
        }

        [Test]
        public void MapRadius_MonotonicAndContinuousAcrossGuardAndReach()
        {
            // 기존 ArmMotionValidation: "soft reach remains monotonic across guard and reach boundary"
            foreach (var guard in new[] { 0.3f, 0.5f, 0.75f })
            foreach (var modelGuard in new[] { 0.2f, 0.45f, 0.8f })
            {
                var last = ArmCalibration.MapRadius(0f, guard, modelGuard);
                for (var i = 1; i <= 300; i++)
                {
                    var r = ArmCalibration.MapRadius(i * 0.005f, guard, modelGuard);
                    Assert.Greater(r, last - 1e-6f, $"guard {guard}, model {modelGuard}, ratio {i * 0.005f}");
                    Assert.Less(r - last, 0.05f, $"끊김: guard {guard}, model {modelGuard}, ratio {i * 0.005f}");
                    Assert.Less(r, 0.99995f);
                    last = r;
                }
            }
        }

        [Test]
        public void DistanceForBend_StraightAndFolded()
        {
            Assert.AreEqual(2f, ArmCalibration.DistanceForBend(1f, 1f, 0f), 1e-5f);
            Assert.AreEqual(0f, ArmCalibration.DistanceForBend(1f, 1f, 180f), 1e-3f);
            Assert.AreEqual(ArmCalibration.DefaultFullRatio, ArmCalibration.DistanceForBend(1f, 1f, ArmCalibration.FullReachBendDegrees) / 2f, 1e-6f);
            Assert.AreEqual(ArmCalibration.DefaultLimitRatio, ArmCalibration.DistanceForBend(1f, 1f, ArmCalibration.OverreachBendDegrees) / 2f, 1e-6f);
        }

        [Test]
        public void Map_GuardGoesToModelGuard_ReachGoesToModelForward()
        {
            foreach (var yaw in new[] { 0f, 90f, -135f })
            {
                var c = Calibrated(yaw);
                var modelShoulder = new Vector3(-6f, 52f, 0f);
                var modelGuard = modelShoulder + new Vector3(1f, 3f, 8f);
                const float modelLength = 18f;

                AssertVector(modelGuard, c.Map(c.Guard, modelShoulder, modelGuard, modelLength), 0.01f);
                AssertVector(modelShoulder + Vector3.forward * (ArmCalibration.DefaultFullRatio * modelLength),
                    c.Map(c.ReachPoint, modelShoulder, modelGuard, modelLength), 0.01f);
            }
        }

        [Test]
        public void Map_SameBodyRelativeHand_SameTargetWhateverBodyYaw()
        {
            // 팔의 정면 기준은 보정 때 정한 몸 정면이다. 몸이 어느 쪽을 보고 섰든 몸 기준으로 같은 손이면 같은 목표.
            var modelShoulder = Vector3.zero;
            var modelGuard = new Vector3(0f, 2f, 8f);
            var local = new Vector3(0.1f, 0.05f, 0.45f);

            var facingZ = Calibrated(0f);
            var facingX = Calibrated(90f, new Vector3(1f, 1.6f, -2f));
            var a = facingZ.Map(Body(0f, facingZ.Shoulder, local), modelShoulder, modelGuard, 18f);
            var b = facingX.Map(Body(90f, facingX.Shoulder, local), modelShoulder, modelGuard, 18f);
            AssertVector(a, b, 0.01f);
            Assert.Greater(a.z, modelGuard.z, "가드보다 앞으로 뻗으면 로봇 주먹도 더 앞");
        }

        [Test]
        public void Map_NotCalibrated_ReturnsModelGuard()
        {
            var c = new ArmCalibration();
            var modelGuard = new Vector3(1f, 2f, 3f);
            AssertVector(modelGuard, c.Map(Vector3.one, Vector3.zero, modelGuard, 18f));
        }
    }
}
