using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>
    /// 2단계 보정 범용성 시뮬레이션(선우 10/28: 어떤 기기·체격이든, 좌우로 옮겨도 정면 = 정면, docs/design/09_calibration.md).
    /// 체격(어깨 폭·높이·팔 길이), 컨트롤러 기준점 차이, 헤드셋 눈→목 차이, 보정 때 고개 흔들림, 서 있는 자리·고개 방향을 바꾼
    /// 가상 조종사들이 정면 잽을 하면 로봇 주먹 목표가 로봇 정면에서 3° 안이어야 한다.
    /// </summary>
    public class CalibrationSimulationTests
    {
        const float RobotArm = 19.4f;
        const float MaxErrorDegrees = 3f;
        static readonly Vector3 AssumedEyeToNeck = new Vector3(0f, -0.1f, -0.09f);

        struct VirtualPilot
        {
            public Vector3 Neck;         // 보정 때 목(트래킹 공간)
            public float BodyYaw;        // 실제 몸 정면
            public float HalfWidth;      // 목 → 어깨 좌우
            public float Drop;           // 목 → 어깨 아래
            public float ArmLength;      // 어깨 → 끝까지 뻗은 컨트롤러
            public Vector3 Controller;   // 컨트롤러 기준점 차이(몸 기준, 기기마다 다름)
            public Vector3 EyeToNeck;    // 실제 눈 → 목(머리 로컬)
            public float HeadYawJitter;  // 보정 때 고개가 몸 정면에서 벗어난 각도

            public Quaternion Body => Quaternion.Euler(0f, BodyYaw, 0f);

            public Vector3 Shoulder(bool right, Vector3 moved) =>
                Neck + moved + Body * new Vector3(right ? HalfWidth : -HalfWidth, -Drop, 0f);

            /// <summary>정면 잽·정면 뻗기 끝의 컨트롤러 위치. extend = 팔 길이 비율.</summary>
            public Vector3 StraightHand(bool right, Vector3 moved, float extend) =>
                Shoulder(right, moved) + Body * (Vector3.forward * (ArmLength * extend) + Controller);

            /// <summary>목이 neck이고 머리 회전이 head일 때 헤드셋(눈) 위치.</summary>
            public Vector3 Eye(Vector3 neck, Quaternion head) => neck - head * EyeToNeck;
        }

        static System.Collections.Generic.IEnumerable<VirtualPilot> Pilots()
        {
            var yaws = new[] { 0f, 37f, -120f };
            var widths = new[] { 0.14f, 0.18f, 0.22f };
            var drops = new[] { 0.08f, 0.16f };
            var lengths = new[] { 0.5f, 0.62f, 0.72f };
            var controllers = new[] { Vector3.zero, new Vector3(0.03f, -0.02f, 0.04f), new Vector3(-0.025f, 0.03f, -0.03f) };
            var eyes = new[] { AssumedEyeToNeck, AssumedEyeToNeck + new Vector3(0.005f, 0.01f, -0.01f), AssumedEyeToNeck + new Vector3(-0.005f, -0.01f, 0.01f) };
            var jitters = new[] { 0f, 1.5f, -1.5f };
            var i = 0;
            foreach (var yaw in yaws)
            foreach (var width in widths)
            foreach (var drop in drops)
            foreach (var length in lengths)
            foreach (var controller in controllers)
            {
                i++;
                yield return new VirtualPilot
                {
                    Neck = new Vector3(0.4f * (i % 3) - 0.4f, 1.35f + 0.05f * (i % 4), 0.3f * (i % 2)),
                    BodyYaw = yaw,
                    HalfWidth = width,
                    Drop = drop,
                    ArmLength = length,
                    Controller = controller,
                    EyeToNeck = eyes[i % eyes.Length],
                    HeadYawJitter = jitters[(i / 3) % jitters.Length],
                };
            }
        }

        /// <summary>그 조종사가 2단계 보정을 한다. 반환: 손마다 보정, 몸 이동 보정 기준 목.</summary>
        static (ArmCalibration left, ArmCalibration right, Vector3 reference) Calibrate(VirtualPilot p)
        {
            var head = Quaternion.Euler(0f, p.BodyYaw + p.HeadYawJitter, 0f);
            var neck = BodyFollow.NeckPoint(p.Eye(p.Neck, head), head, AssumedEyeToNeck);
            var yaw = p.BodyYaw + p.HeadYawJitter;
            var rotation = p.Body;
            var left = new ArmCalibration();
            var right = new ArmCalibration();
            Assert.AreEqual(CalibrationError.None, left.CaptureForward(neck, yaw, p.StraightHand(false, Vector3.zero, 0.97f), rotation, false), "왼손 정면 뻗기");
            Assert.AreEqual(CalibrationError.None, right.CaptureForward(neck, yaw, p.StraightHand(true, Vector3.zero, 0.97f), rotation, true), "오른손 정면 뻗기");
            // 가드: 어깨 앞 안쪽, 몸 이동 보정 기준이 정해진 뒤라 같은 자리면 보정량 0.
            Assert.AreEqual(CalibrationError.None, left.CaptureGuardAfterReach(p.Shoulder(false, Vector3.zero) + p.Body * new Vector3(0.08f, 0.05f, 0.2f), rotation), "왼손 가드");
            Assert.AreEqual(CalibrationError.None, right.CaptureGuardAfterReach(p.Shoulder(true, Vector3.zero) + p.Body * new Vector3(-0.08f, 0.05f, 0.2f), rotation), "오른손 가드");
            return (left, right, neck);
        }

        static float JabErrorDegrees(VirtualPilot p, ArmCalibration arm, Vector3 reference, bool right, Vector3 moved, Quaternion headNow)
        {
            var follow = new BodyFollow();
            follow.SetReference(reference);
            var neckNow = BodyFollow.NeckPoint(p.Eye(p.Neck + moved, headNow), headNow, AssumedEyeToNeck);
            follow.Update(neckNow, 1f / 90f, new BodyFollowSettings());
            var hand = p.StraightHand(right, moved, 0.95f) - follow.Offset;
            Assert.IsTrue(ArmMapping.TryLinear(arm, hand, RobotArm, 0f, out var robot));
            return Vector3.Angle(robot, Vector3.forward);
        }

        [Test]
        public void StraightJab_GoesStraight_ForAnyBodyDeviceAndStance()
        {
            var worst = 0f;
            var count = 0;
            var moves = new[] { Vector3.zero, new Vector3(0.3f, 0f, 0f), new Vector3(-0.25f, -0.05f, 0.15f) };
            var heads = new[] { Vector2.zero, new Vector2(40f, 0f), new Vector2(-35f, 15f) };
            foreach (var p in Pilots())
            {
                var (left, right, reference) = Calibrate(p);
                foreach (var move in moves)
                foreach (var h in heads)
                {
                    var moved = p.Body * move;
                    var headNow = Quaternion.Euler(h.y, p.BodyYaw + h.x, 0f);
                    worst = Mathf.Max(worst, JabErrorDegrees(p, left, reference, false, moved, headNow));
                    worst = Mathf.Max(worst, JabErrorDegrees(p, right, reference, true, moved, headNow));
                    count += 2;
                }
            }
            Assert.Less(worst, MaxErrorDegrees, $"정면 잽 {count}번 중 가장 큰 오차 {worst:F2}°");
        }

        [Test]
        public void TwoHandsSavedAtDifferentMoments_CommonReferenceMatchesSameMoment()
        {
            // 왼손 저장 뒤 오른손 저장 전에 10cm 옮겨 갔다. 공통 목으로 옮겨 계산하면 같은 순간에 저장한 것과 같다.
            var p = new VirtualPilot { Neck = new Vector3(0f, 1.45f, 0f), BodyYaw = 10f, HalfWidth = 0.18f, Drop = 0.12f, ArmLength = 0.6f, EyeToNeck = AssumedEyeToNeck };
            var shift = p.Body * new Vector3(0.1f, 0f, 0.02f);
            var leftNeck = p.Neck;
            var rightNeck = p.Neck + shift;
            var (neck, yaw) = ArmCalibration.CommonReference(leftNeck, 10f, rightNeck, 10f);
            var right = new ArmCalibration();
            var rightHand = p.StraightHand(true, shift, 0.97f) - (rightNeck - neck);
            Assert.AreEqual(CalibrationError.None, right.CaptureForward(neck, yaw, rightHand, Quaternion.identity, true));
            var same = new ArmCalibration();
            Assert.AreEqual(CalibrationError.None, same.CaptureForward(p.Neck, 10f, p.StraightHand(true, Vector3.zero, 0.97f), Quaternion.identity, true));
            // 공통 목은 두 목의 가운데라 어깨도 같은 만큼(shift/2) 옮겨 있고, 모양(어깨 → 손)은 같다.
            Assert.AreEqual(0f, Vector3.Distance(right.ReachPoint - right.Shoulder, same.ReachPoint - same.Shoulder), 1e-4f);
        }

        [Test]
        public void CaptureForward_RejectsBadPoses()
        {
            var neck = new Vector3(0f, 1.45f, 0f);
            var arm = new ArmCalibration();
            Assert.AreEqual(CalibrationError.ReachNotForward, arm.CaptureForward(neck, 0f, neck + new Vector3(0.18f, -0.1f, 0.1f), Quaternion.identity, true), "앞으로 덜 뻗음");
            Assert.AreEqual(CalibrationError.ReachNotLevel, arm.CaptureForward(neck, 0f, neck + new Vector3(0.18f, -0.7f, 0.4f), Quaternion.identity, true), "너무 낮음");
            Assert.AreEqual(CalibrationError.ReachWrongSide, arm.CaptureForward(neck, 0f, neck + new Vector3(-0.15f, -0.1f, 0.5f), Quaternion.identity, true), "오른손이 왼쪽");
            Assert.AreEqual(CalibrationError.ReachMissing, new ArmCalibration().CaptureGuardAfterReach(neck, Quaternion.identity), "가드 먼저");
            Assert.IsFalse(arm.IsComplete);
        }

        [Test]
        public void GuardTooFarOut_IsRejected()
        {
            var neck = new Vector3(0f, 1.45f, 0f);
            var arm = new ArmCalibration();
            Assert.AreEqual(CalibrationError.None, arm.CaptureForward(neck, 0f, neck + new Vector3(0.18f, -0.12f, 0.6f), Quaternion.identity, true));
            Assert.AreEqual(CalibrationError.GuardOutOfRange, arm.CaptureGuardAfterReach(neck + new Vector3(0.18f, -0.12f, 0.58f), Quaternion.identity), "가드가 다 뻗은 자세");
            Assert.AreEqual(CalibrationError.None, arm.CaptureGuardAfterReach(neck + new Vector3(0.1f, -0.05f, 0.2f), Quaternion.identity));
            Assert.IsTrue(arm.IsComplete);
        }

        [Test]
        public void HeadPitch_DownIsPositive()
        {
            Assert.AreEqual(0f, ArmCalibration.HeadPitchDegrees(Vector3.forward), 1e-3f);
            Assert.AreEqual(30f, ArmCalibration.HeadPitchDegrees(Quaternion.Euler(30f, 0f, 0f) * Vector3.forward), 1e-3f);
        }
    }
}
