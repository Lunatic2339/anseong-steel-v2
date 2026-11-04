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
            Assert.AreEqual(MoveKind.None, CoopMoveRules.ForwardBack(Vector2.up, new Vector2(0.7f, 0.7f), S), "45° 차이");
            Assert.AreEqual(MoveKind.None, CoopMoveRules.ForwardBack(Vector2.right, Vector2.right, S), "좌/우는 회피 규칙");
        }

        [Test]
        public void Approach_StopsAtMeleeDistance()
        {
            var next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, 0, Vector3.zero, Quaternion.identity, Boss, S, ref next, out var seg));
            Assert.AreEqual(MoveKind.Approach, seg.Kind);
            Assert.AreEqual(S.stepMeters, seg.To.z, 1e-4f, "한 걸음");
            Assert.Greater(next, 1.0 + seg.Duration - 1e-6);

            // 보스와 30 m: 26 m까지 4 m만.
            next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, 0, new Vector3(0f, 0f, 10f), Quaternion.identity, Boss, S, ref next, out seg));
            Assert.AreEqual(Boss.z - S.approachStopMeters, seg.To.z, 1e-4f);

            // 이미 근접 거리.
            next = 0d;
            Assert.IsFalse(CoopMoveRules.TryPlan(1.0, MoveKind.Approach, 0, new Vector3(0f, 0f, 14f), Quaternion.identity, Boss, S, ref next, out _));
        }

        [Test]
        public void Retreat_StopsAtMaxDistance()
        {
            var next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.Retreat, 0, Vector3.zero, Quaternion.identity, Boss, S, ref next, out var seg));
            Assert.AreEqual(-S.stepMeters, seg.To.z, 1e-4f);
            next = 0d;
            var far = new Vector3(0f, 0f, Boss.z - S.retreatStopMeters);
            Assert.IsFalse(CoopMoveRules.TryPlan(1.0, MoveKind.Retreat, 0, far, Quaternion.identity, Boss, S, ref next, out _));
        }

        [Test]
        public void Dodge_GoesSideways_AndCoolsDown()
        {
            var next = 0d;
            Assert.IsTrue(CoopMoveRules.TryPlan(1.0, MoveKind.None, -1, Vector3.zero, Quaternion.identity, Boss, S, ref next, out var seg));
            Assert.AreEqual(MoveKind.DodgeLeft, seg.Kind);
            Assert.AreEqual(-S.dodgeMeters, seg.To.x, 1e-4f);
            Assert.AreEqual(1.0 + S.stepSeconds + S.dodgeCooldownSeconds, next, 1e-6);
            Assert.IsFalse(CoopMoveRules.TryPlan(1.5, MoveKind.Approach, 0, Vector3.zero, Quaternion.identity, Boss, S, ref next, out _), "쉬는 중");
        }

        [Test]
        public void Nothing_WhenNoInput()
        {
            var next = 0d;
            Assert.IsFalse(CoopMoveRules.TryPlan(1.0, MoveKind.None, 0, Vector3.zero, Quaternion.identity, Boss, S, ref next, out _));
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
