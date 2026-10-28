using Anseong.Core;
using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class TwoBoneArmSolverTests
    {
        const float Upper = 15f;    // 로봇 크기 위팔·아래팔(m)
        const float Lower = 14f;
        const float Dt = 1f / 90f;
        static readonly Vector3 Shoulder = new Vector3(-8f, 40f, 0f);

        /// <summary>
        /// 왼팔 T자세 바인드: 팔이 −X로 곧게 뻗고 손바닥이 아래. 뼈의 로컬 +Y가 뼈 방향(Mixamo와 같다).
        /// 위팔에는 일부러 비틀린 roll을 넣어 바인드 회전이 그대로 보존되는지 본다.
        /// </summary>
        static ArmBind LeftBind()
        {
            var along = Quaternion.FromToRotation(Vector3.up, Vector3.left);
            var upperRotation = along * Quaternion.AngleAxis(37f, Vector3.up);
            var lowerRotation = along;
            var handRotation = along * Quaternion.AngleAxis(-12f, Vector3.up);
            var fistBackLocal = Quaternion.Inverse(handRotation) * Vector3.up;
            return ArmBind.FromPose(Shoulder, Shoulder + Vector3.left * Upper, Shoulder + Vector3.left * (Upper + Lower),
                upperRotation, lowerRotation, handRotation, Vector3.up, fistBackLocal, PilotSide.Left, Quaternion.identity);
        }

        static TwoBoneArmSolver NewSolver() => new TwoBoneArmSolver(LeftBind());

        /// <summary>목표 쪽을 보는 주먹(손등 위).</summary>
        static Quaternion FistToward(Vector3 target)
        {
            var direction = target - Shoulder;
            return Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .95f ? Vector3.back : Vector3.up);
        }

        static ArmPose Solve(TwoBoneArmSolver solver, Vector3 target, Quaternion? fist = null, float wristLock = 1f, int frames = 1)
        {
            var pose = default(ArmPose);
            for (var i = 0; i < frames; i++)
                pose = solver.Solve(Shoulder, target, fist ?? FistToward(target), Quaternion.identity, wristLock, 0f, Dt);
            return pose;
        }

        static void AssertLengths(ArmPose pose)
        {
            Assert.AreEqual(Upper, Vector3.Distance(Shoulder, pose.Elbow), 1e-2f, "위팔 길이");
            Assert.AreEqual(Lower, Vector3.Distance(pose.Elbow, pose.Wrist), 1e-2f, "아래팔 길이");
        }

        [Test]
        public void ReachableTarget_WristLandsOnTarget_BonesPointAlongSegments()
        {
            var solver = NewSolver();
            var target = Shoulder + new Vector3(2f, -6f, 16f);
            var pose = Solve(solver, target, frames: 30);
            AssertLengths(pose);
            Assert.Less(Vector3.Distance(target, pose.Wrist), 1e-2f, "손목이 목표에");
            // 뼈의 로컬 +Y가 실제 뼈 방향을 향한다(바인드 기준 재구성).
            Assert.Less(Vector3.Angle(pose.Upper * Vector3.up, pose.Elbow - Shoulder), 0.1f, "위팔 방향");
            Assert.Less(Vector3.Angle(pose.Lower * Vector3.up, pose.Wrist - pose.Elbow), 0.1f, "아래팔 방향");
        }

        [Test]
        public void ElbowBendsOutwardAndDown_ForLeftArm()
        {
            var solver = NewSolver();
            var pose = Solve(solver, Shoulder + new Vector3(4f, -4f, 15f), frames: 30);
            var middle = Shoulder + (pose.Wrist - Shoulder) * 0.5f;
            Assert.Less(pose.Elbow.x, middle.x, "왼팔 팔꿈치는 바깥(−X)");
            Assert.Less(pose.Elbow.y, middle.y + 0.5f, "팔꿈치가 위로 들리지 않음");
        }

        [Test]
        public void BeyondReach_ConvergesSmoothlyBelowFullLength()
        {
            var solver = NewSolver();
            var total = Upper + Lower;
            var last = 0f;
            for (var i = 0; i <= 200; i++)
            {
                var distance = total * (0.8f + i * 0.004f);   // 0.8 → 1.6배
                var pose = Solve(solver, Shoulder + Vector3.forward * distance, frames: 2);
                var reach = Vector3.Distance(Shoulder, pose.Wrist);
                Assert.IsTrue(reach >= last - 1e-3f, $"단조 증가 {distance}: {reach} < {last}");
                Assert.LessOrEqual(reach, solver.MaximumReach + 1e-3f, "최대 도달 이하");
                last = reach;
                AssertLengths(pose);
            }
            Assert.Greater(last, solver.SoftReachStart, "멀리 뻗으면 완화 구간 끝까지 간다");
        }

        [Test]
        public void SoftDistance_IsContinuousAtOnset()
        {
            const float length = 10f;
            var before = MechaArmStability.SoftDistance(9.399f, length);
            var at = MechaArmStability.SoftDistance(9.4f, length);
            var after = MechaArmStability.SoftDistance(9.401f, length);
            Assert.AreEqual(0.001f, at - before, 1e-4f);
            Assert.AreEqual(0.001f, after - at, 1e-4f, "시작점 기울기 1");
            Assert.Less(MechaArmStability.SoftDistance(100f, length), 9.9f + 1e-4f);
        }

        [Test]
        public void TargetAtShoulder_StaysOutsideInnerSphere()
        {
            var solver = NewSolver();
            Solve(solver, Shoulder + new Vector3(0f, -5f, 12f), frames: 20);
            var pose = Solve(solver, Shoulder + new Vector3(0f, 0f, 0.1f), frames: 20);
            Assert.IsTrue(pose.InsideMinimum);
            Assert.Greater(Vector3.Distance(Shoulder, pose.Wrist), solver.MinimumReach - 1e-2f);
            AssertLengths(pose);
        }

        [Test]
        public void WristLocked_FistFollowsForearm()
        {
            var solver = NewSolver();
            var target = Shoulder + new Vector3(0f, -3f, 18f);
            var sideways = Quaternion.LookRotation(Vector3.right, Vector3.up);
            var pose = Solve(solver, target, sideways, wristLock: 1f, frames: 60);
            var fistForward = pose.Hand * Vector3.up;
            Assert.Less(Vector3.Angle(fistForward, pose.Wrist - pose.Elbow), 0.5f, "그립 중 손목이 아래팔과 일직선");
        }

        [Test]
        public void WristLocked_PitchOffset_TiltsTowardBackOfHand()
        {
            var settings = new ArmSolverSettings { lockedWristPitchDegrees = 15f };
            var solver = new TwoBoneArmSolver(LeftBind(), settings);
            var target = Shoulder + new Vector3(0f, -3f, 18f);
            var pose = Solve(solver, target, wristLock: 1f, frames: 60);
            var bind = solver.Bind;
            var forearm = (pose.Wrist - pose.Elbow).normalized;
            var fistForward = pose.Hand * bind.FistForwardLocal;
            var fistBack = pose.Hand * bind.FistBackLocal;
            Assert.AreEqual(15f, Vector3.Angle(fistForward, forearm), 0.5f, "고정 + 젖힘 각도");
            // 손등 쪽으로 젖히면 아래팔 방향이 손바닥 쪽(손등 반대)에 놓인다.
            Assert.Less(Vector3.Dot(forearm, fistBack), 0f, "손등 쪽으로 젖힘");
        }

        [Test]
        public void WristFree_BendsTowardFistUpToLimit()
        {
            var solver = NewSolver();
            var target = Shoulder + new Vector3(0f, -3f, 18f);
            var sideways = Quaternion.LookRotation(Vector3.right, Vector3.up);
            var pose = Solve(solver, target, sideways, wristLock: 0f, frames: 90);
            var bend = Vector3.Angle(pose.Hand * Vector3.up, pose.Wrist - pose.Elbow);
            Assert.Greater(bend, 20f, "놓으면 손목이 꺾인다");
            Assert.LessOrEqual(bend, new ArmSolverSettings().freeWristSwingDegrees + 0.5f, "꺾임 한계");
        }

        [Test]
        public void HandOverHeadAndBehindBody_NoSuddenFlip()
        {
            var solver = NewSolver();
            var total = Upper + Lower;
            // 가드 → 머리 위 → 몸 뒤 → 반대쪽 어깨 → 가드. 2초씩.
            Vector3[] path =
            {
                new Vector3(2f, -6f, 14f), new Vector3(-2f, 22f, 6f), new Vector3(-4f, 10f, -18f),
                new Vector3(-6f, -10f, -14f), new Vector3(20f, 2f, 8f), new Vector3(2f, -6f, 14f),
            };
            // 전완 롤은 rollSpeed까지 돌 수 있다(선우 10/28: 회전은 빠르게). 그 위로 튀면 실패.
            var lowerLimit = new ArmSolverSettings().rollSpeed * Dt + 4f;
            var previous = Solve(solver, Shoulder + path[0], frames: 30);
            for (var segment = 0; segment + 1 < path.Length; segment++)
            {
                for (var i = 1; i <= 180; i++)
                {
                    var target = Shoulder + Vector3.Lerp(path[segment], path[segment + 1], i / 180f);
                    var pose = Solve(solver, target);
                    Assert.Less(Quaternion.Angle(previous.Upper, pose.Upper), 6f, $"위팔이 한 프레임에 크게 돌지 않음 (구간 {segment}, {i})");
                    Assert.Less(Quaternion.Angle(previous.Lower, pose.Lower), lowerLimit, $"아래팔이 한 프레임에 크게 돌지 않음 (구간 {segment}, {i})");
                    Assert.Less(Vector3.Distance(previous.Elbow, pose.Elbow), total * 0.05f, $"팔꿈치가 튀지 않음 (구간 {segment}, {i})");
                    Assert.LessOrEqual(Mathf.Abs(pose.Swivel), new ArmSolverSettings().maxSwivelDegrees + 1e-3f, "swivel 한계");
                    AssertLengths(pose);
                    previous = pose;
                }
            }
        }

        [Test]
        public void TwoMinutesOfCircles_NoRollDrift()
        {
            var start = Shoulder + new Vector3(2f, -6f, 14f);
            var fresh = Solve(NewSolver(), start, frames: 180);

            var solver = NewSolver();
            Solve(solver, start, frames: 30);
            const int frames = 120 * 90;
            for (var i = 0; i < frames; i++)
            {
                var angle = i * Dt * Mathf.PI;   // 2초에 한 바퀴
                var target = Shoulder + new Vector3(2f + 10f * Mathf.Cos(angle), 2f + 10f * Mathf.Sin(angle), 14f);
                // 주먹도 원을 따라 계속 같은 방향으로 비튼다.
                var fist = FistToward(target) * Quaternion.AngleAxis(40f * Mathf.Sin(angle), Vector3.forward);
                solver.Solve(Shoulder, target, fist, Quaternion.identity, i % 300 < 150 ? 1f : 0f, 0f, Dt);
            }
            var back = Solve(solver, start, frames: 180);
            Assert.Less(Quaternion.Angle(fresh.Upper, back.Upper), 0.5f, "위팔 roll 드리프트");
            Assert.Less(Quaternion.Angle(fresh.Lower, back.Lower), 0.5f, "아래팔 roll 드리프트");
            Assert.Less(Quaternion.Angle(fresh.Hand, back.Hand), 0.5f, "손 roll 드리프트");
        }

        [Test]
        public void BodyTurn_RotatesWholeSolution()
        {
            var turn = Quaternion.Euler(0f, 70f, 0f);
            var local = new Vector3(2f, -6f, 14f);
            var a = Solve(NewSolver(), Shoulder + local, frames: 60);

            var solver = NewSolver();
            var b = default(ArmPose);
            for (var i = 0; i < 60; i++)
                b = solver.Solve(Shoulder, Shoulder + turn * local, turn * FistToward(Shoulder + local), turn, 1f, 0f, Dt);
            Assert.Less(Vector3.Distance(Shoulder + turn * (a.Elbow - Shoulder), b.Elbow), 1e-2f, "팔꿈치");
            Assert.Less(Quaternion.Angle(turn * a.Upper, b.Upper), 0.2f, "위팔 회전");
            Assert.Less(Quaternion.Angle(turn * a.Hand, b.Hand), 0.2f, "손 회전");
        }

        [Test]
        public void ExtendedArm_TurningFist_DoesNotSwingElbow()
        {
            // 거의 다 뻗은 팔(92%)에서 주먹을 위·옆으로 40° 돌려도 팔꿈치는 거의 그대로(선우 10/28).
            var target = Shoulder + new Vector3(0f, -2f, (Upper + Lower) * 0.92f);
            var straight = Solve(NewSolver(), target, frames: 90);
            var turned = Solve(NewSolver(), target, Quaternion.Euler(-40f, 40f, 0f) * FistToward(target), frames: 90);
            Assert.Less(Vector3.Distance(straight.Elbow, turned.Elbow), 0.3f, "팔꿈치가 원을 그리지 않음");

            var settings = new ArmSolverSettings { holdElbowWhenExtended = false };
            var old = Solve(new TwoBoneArmSolver(LeftBind(), settings), target, Quaternion.Euler(-40f, 40f, 0f) * FistToward(target), frames: 90);
            Assert.Greater(Vector3.Distance(straight.Elbow, old.Elbow), 0.3f, "끄면 예전처럼 주먹 방향이 팔꿈치를 돌림");
        }

        [Test]
        public void BoundSwivel_HoldsSideNearOppositeLimits()
        {
            Assert.AreEqual(50f, TwoBoneArmSolver.BoundSwivel(50f, 0f), 1e-4f);
            Assert.AreEqual(105f, TwoBoneArmSolver.BoundSwivel(130f, 0f), 1e-4f);
            Assert.AreEqual(-105f, TwoBoneArmSolver.BoundSwivel(-130f, 0f), 1e-4f);
            // ±180 근처: 직전이 +쪽이면 +한계, −쪽이면 −한계를 유지한다.
            Assert.AreEqual(105f, TwoBoneArmSolver.BoundSwivel(-178f, 100f), 1e-4f);
            Assert.AreEqual(-105f, TwoBoneArmSolver.BoundSwivel(178f, -100f), 1e-4f);
        }
    }
}
