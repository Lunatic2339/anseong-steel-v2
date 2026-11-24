using Anseong.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class CoopMoveRulesTests
    {
        static readonly CoopMoveSettings S = new CoopMoveSettings();
        static readonly Vector3 Boss = new Vector3(0f, 0f, 40f);

        [Test]
        public void ForwardBack_NeedsBothSticks_SameDirection()
        {
            Assert.AreEqual(MoveKind.Approach, CoopMoveRules.ForwardBack(Vector2.up, new Vector2(0.1f, 0.9f), S));
            Assert.AreEqual(MoveKind.Retreat, CoopMoveRules.ForwardBack(Vector2.down, Vector2.down * 0.6f, S));
            Assert.AreEqual(MoveKind.None, CoopMoveRules.ForwardBack(Vector2.up, Vector2.zero, S), "한 명이 놓음");
            Assert.AreEqual(MoveKind.None, CoopMoveRules.ForwardBack(Vector2.up, Vector2.up * 0.3f, S), "약하게 밈");
            Assert.AreEqual(MoveKind.None, CoopMoveRules.ForwardBack(Vector2.up, new Vector2(0.8f, 0.6f), S), "한쪽이 너무 옆으로");
            Assert.AreEqual(MoveKind.Approach, CoopMoveRules.ForwardBack(new Vector2(-0.25f, 0.97f), new Vector2(0.30f, 0.91f), S), "엄지로 앞 밀기(33° 벌어짐, 11/04 기록)");
            Assert.AreEqual(MoveKind.None, CoopMoveRules.ForwardBack(Vector2.right, Vector2.right, S), "좌/우는 회피 규칙");
        }

        [Test]
        public void Approach_StopsAtMeleeDistance()
        {
            var next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, Vector3.zero, Quaternion.identity, Boss, S, ref next, out var seg));
            Assert.AreEqual(MoveKind.Approach, seg.Kind);
            Assert.AreEqual(S.stepMeters, seg.To.z, 1e-4f, "한 걸음");
            Assert.Greater(next, 1.0 + seg.Duration - 1e-6);

            // 보스와 30 m: 26 m까지 4 m만.
            next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, new Vector3(0f, 0f, 10f), Quaternion.identity, Boss, S, ref next, out seg));
            Assert.AreEqual(Boss.z - S.approachStopMeters, seg.To.z, 1e-4f);

            // 이미 근접 거리.
            next = 0d;
            Assert.IsFalse(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, new Vector3(0f, 0f, 14f), Quaternion.identity, Boss, S, ref next, out _));
        }

        [Test]
        public void Retreat_StopsAtMaxDistance()
        {
            var next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Retreat, Vector3.zero, Quaternion.identity, Boss, S, ref next, out var seg));
            Assert.AreEqual(-S.stepMeters, seg.To.z, 1e-4f);
            next = 0d;
            var far = new Vector3(0f, 0f, Boss.z - S.retreatStopMeters);
            Assert.IsFalse(CoopMoveRules.TryPlan(1.0, MoveKind.Retreat, far, Quaternion.identity, Boss, S, ref next, out _));
        }

        [Test]
        public void Dodge_GoesSideways_AndCoolsDown()
        {
            double next = 0d, nextDodge = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlanDodge(1.0, -1, default, Vector3.zero, Quaternion.identity, Boss, S, ref next, ref nextDodge, out var seg));
            Assert.AreEqual(MoveKind.DodgeLeft, seg.Kind);
            Assert.Less(seg.To.x, -S.dodgeMeters * 0.9f, "왼쪽");
            Assert.AreEqual(Boss.z, Vector3.Distance(seg.To, Boss), 1e-3f, "보스 거리 그대로(둘레로)");
            Assert.AreEqual(1.0 + S.stepSeconds + S.dodgeCooldownSeconds, nextDodge, 1e-6);
            Assert.AreEqual(1.0 + S.stepSeconds + S.stepGapSeconds, next, 1e-6, "앞뒤 걸음은 회피 끝나면 바로");
            Assert.IsFalse(CoopMoveRules.TryPlanDodge(1.2, 1, seg, Vector3.zero, Quaternion.identity, Boss, S, ref next, ref nextDodge, out _), "회피 도중");
            Assert.IsFalse(CoopMoveRules.TryPlanDodge(1.6, 1, seg, Vector3.zero, Quaternion.identity, Boss, S, ref next, ref nextDodge, out _), "회피 쉼");
            Assert.IsTrue(CoopMoveRules.TryPlan(1.6, MoveKind.Approach, Vector3.zero, Quaternion.identity, Boss, S, ref next, out _), "걸음은 됨");
        }

        [Test]
        public void Dodge_InterruptsStep_FromCurrentPosition()
        {
            double next = 0d, nextDodge = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, Vector3.zero, Quaternion.identity, Boss, S, ref next, out var step));
            var mid = step.Evaluate(1.2);
            Assert.IsTrue(CoopMoveRules.TryPlanDodge(1.2, 1, step, mid, Quaternion.identity, Boss, S, ref next, ref nextDodge, out var seg), "걸음 도중 회피");
            Assert.AreEqual(MoveKind.DodgeRight, seg.Kind);
            Assert.AreEqual(mid.z, seg.From.z, 1e-4f);
            Assert.Greater(seg.To.x, S.dodgeMeters * 0.9f, "오른쪽");
            Assert.AreEqual(Vector3.Distance(mid, Boss), Vector3.Distance(seg.To, Boss), 1e-3f);
            Assert.IsFalse(CoopMoveRules.TryPlanDodge(1.2, 0, step, mid, Quaternion.identity, Boss, S, ref next, ref nextDodge, out _), "방향 없음");
        }

        [Test]
        public void LookYaw_FacesBoss()
        {
            Assert.AreEqual(0f, CoopMoveRules.LookYaw(Vector3.zero, Boss, 5f), 1e-4f);
            Assert.AreEqual(90f, CoopMoveRules.LookYaw(Vector3.zero, new Vector3(30f, 0f, 0f), 5f), 1e-4f);
            Assert.AreEqual(5f, CoopMoveRules.LookYaw(Boss, Boss, 5f), 1e-4f, "겹치면 그대로");
        }

        [Test]
        public void Nothing_WhenNoInput()
        {
            var next = 0d;
            Assert.IsFalse(CoopMoveRules.TryPlan(1.0, MoveKind.None, Vector3.zero, Quaternion.identity, Boss, S, ref next, out _));
        }

        [Test]
        public void Segment_EvaluatesSmoothly()
        {
            var seg = new MoveSegment { Kind = MoveKind.Approach, From = Vector3.zero, To = new Vector3(0f, 0f, 8f), Start = 10.0, Duration = 0.4f };
            Assert.AreEqual(0f, seg.Evaluate(9.0).z, 1e-4f);
            Assert.AreEqual(4f, seg.Evaluate(10.2).z, 1e-3f);
            Assert.AreEqual(8f, seg.Evaluate(11.0).z, 1e-4f);
            Assert.IsTrue(seg.IsMoving(10.1));
            Assert.IsFalse(seg.IsMoving(10.5));
        }

        [Test]
        public void Turn_StartsPast20_StopsNearZero_LimitedSpeed()
        {
            var turning = false;
            // 보스가 정면에서 10°: 안 돈다.
            var boss10 = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward * 40f;
            Assert.AreEqual(0f, CoopMoveRules.TurnYaw(0f, Vector3.zero, boss10, 1f, S, ref turning), 1e-4f);
            Assert.IsFalse(turning);

            // 30°: 1초에 30°까지만, 2° 안에서 멈춤.
            var boss30 = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward * 40f;
            var yaw = CoopMoveRules.TurnYaw(0f, Vector3.zero, boss30, 0.5f, S, ref turning);
            Assert.IsTrue(turning);
            Assert.AreEqual(15f, yaw, 1e-3f);
            for (var i = 0; i < 100; i++) yaw = CoopMoveRules.TurnYaw(yaw, Vector3.zero, boss30, 0.1f, S, ref turning);
            Assert.IsFalse(turning);
            Assert.AreEqual(30f, yaw, S.turnStopDegrees + 1e-3f);
        }
    }
}
