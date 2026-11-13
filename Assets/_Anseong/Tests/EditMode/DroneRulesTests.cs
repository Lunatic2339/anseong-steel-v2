using Anseong.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>M4-04: 스캐너·포 조준 각도로 드론 고르기, 달려드는 드론 위치.</summary>
    public class DroneRulesTests
    {
        [Test]
        public void PicksClosestAngleWithinLimit()
        {
            var targets = new[] { new Vector3(0, 0, 100), new Vector3(10, 0, 100), new Vector3(0, 100, 0) };
            Assert.AreEqual(0, DroneRules.Pick(Vector3.zero, Vector3.forward, targets, null, 10f));
            Assert.AreEqual(1, DroneRules.Pick(Vector3.zero, new Vector3(0.1f, 0, 1), targets, null, 10f));
            Assert.AreEqual(-1, DroneRules.Pick(Vector3.zero, Vector3.right, targets, null, 10f));
        }

        [Test]
        public void SkipsIneligible()
        {
            var targets = new[] { new Vector3(0, 0, 100), new Vector3(5, 0, 100) };
            Assert.AreEqual(1, DroneRules.Pick(Vector3.zero, Vector3.forward, targets, new[] { false, true }, 10f));
        }

        [Test]
        public void RushMovesToTarget()
        {
            Assert.AreEqual(new Vector3(5, 0, 0), DroneRules.RushPosition(Vector3.zero, new Vector3(10, 0, 0), 0.5f));
            Assert.AreEqual(new Vector3(10, 0, 0), DroneRules.RushPosition(Vector3.zero, new Vector3(10, 0, 0), 2f));
        }

        [Test]
        public void ArcedRushStartsSlowEndsOnTargetAndRises()
        {
            var to = new Vector3(100, 0, 0);
            Assert.AreEqual(Vector3.zero, DroneRules.RushPosition(Vector3.zero, to, 0f, 12f));
            var end = DroneRules.RushPosition(Vector3.zero, to, 1f, 12f);
            Assert.AreEqual(100f, end.x, 1e-3f);
            Assert.AreEqual(0f, end.y, 1e-3f);
            var mid = DroneRules.RushPosition(Vector3.zero, to, 0.5f, 12f);
            Assert.Less(mid.x, 50f);      // 처음엔 천천히(쳐낼 틈)
            Assert.Greater(mid.y, 5f);    // 위로 휜다
            // 뒤쪽 절반이 앞쪽 절반보다 멀리 간다(마지막 돌진).
            Assert.Greater(end.x - mid.x, mid.x);
        }

        [Test]
        public void WarningBeepsGetFasterAsDroneCloses()
        {
            Assert.AreEqual(0.9f, DroneRules.WarnInterval(0f, 0.9f, 0.16f), 1e-5f);
            Assert.AreEqual(0.16f, DroneRules.WarnInterval(1f, 0.9f, 0.16f), 1e-5f);
            Assert.Less(DroneRules.WarnInterval(0.8f, 0.9f, 0.16f), DroneRules.WarnInterval(0.2f, 0.9f, 0.16f));
        }

        [Test]
        public void PatrolSwingsAroundPost()
        {
            var post = new Vector3(0, 50, 60);
            Assert.AreEqual(post, DroneRules.Patrol(post, Vector3.right, 14f, 8f, 0f));
            var quarter = DroneRules.Patrol(post, Vector3.right, 14f, 8f, 2f);
            Assert.AreEqual(14f, quarter.x, 1e-3f);
            Assert.AreEqual(50f, quarter.y, 1e-3f);
            Assert.AreEqual(post, DroneRules.Patrol(post, Vector3.right, 0f, 8f, 2f));
        }

        [Test]
        public void FlyEasesBetweenEnds()
        {
            var to = new Vector3(0, 0, 10);
            Assert.AreEqual(Vector3.zero, DroneRules.Fly(Vector3.zero, to, -1f));
            Assert.AreEqual(to, DroneRules.Fly(Vector3.zero, to, 3f));
            Assert.AreEqual(5f, DroneRules.Fly(Vector3.zero, to, 0.5f).z, 1e-4f);
        }

        [Test]
        public void ShootersTakeTurns()
        {
            var canFire = new[] { true, false, true, false };
            Assert.AreEqual(2, DroneRules.NextShooter(canFire, 0));
            Assert.AreEqual(0, DroneRules.NextShooter(canFire, 2));
            Assert.AreEqual(0, DroneRules.NextShooter(new[] { true, false, false, false }, 0));
            Assert.AreEqual(-1, DroneRules.NextShooter(new[] { false, false, false, false }, 1));
        }

        [Test]
        public void BossHitLandsOnArmorAtBodyHeight()
        {
            var feet = new Vector3(0, 0, 100);
            // 가슴 높이로 똑바로 쏘면 몸통 앞면(반지름만큼 앞)에 맞는다. 발밑이 아니다.
            Assert.IsTrue(DroneRules.BossHit(new Vector3(0, 40, 0), Vector3.forward, feet, 8f, 60f, 10f, out var point));
            Assert.AreEqual(40f, point.y, 1e-3f);
            Assert.AreEqual(90f, point.z, 1e-3f);
            // 머리 위·옆으로 빗나가면 안 맞는다.
            Assert.IsFalse(DroneRules.BossHit(new Vector3(0, 40, 0), new Vector3(0, 0.5f, 1f), feet, 8f, 60f, 10f, out _));
            Assert.IsFalse(DroneRules.BossHit(new Vector3(0, 40, 0), new Vector3(0.3f, 0, 1f), feet, 8f, 60f, 10f, out _));
            // 등 뒤로 쏘면 안 맞는다.
            Assert.IsFalse(DroneRules.BossHit(new Vector3(0, 40, 0), Vector3.back, feet, 8f, 60f, 10f, out _));
        }

        [Test]
        public void RicochetBouncesBackAndUp()
        {
            var away = DroneRules.Ricochet(Vector3.forward, Vector3.back);
            Assert.Less(away.z, 0f);
            Assert.Greater(away.y, 0f);
            Assert.AreEqual(1f, away.magnitude, 1e-4f);
        }

        [Test]
        public void ScatterFansOutBehindBoss()
        {
            for (var i = 0; i < 5; i++)
            {
                var d = DroneRules.ScatterDirection(i, 5, Vector3.back, Vector3.right);
                Assert.AreEqual(1f, d.magnitude, 1e-4f);
                Assert.GreaterOrEqual(d.z, -1f);
                Assert.LessOrEqual(d.z, 0.01f); // 등 뒤 반구(앞으로는 안 감)
                Assert.Greater(d.y, 0f);
            }
            Assert.Less(DroneRules.ScatterDirection(0, 5, Vector3.back, Vector3.right).x, 0f);
            Assert.Greater(DroneRules.ScatterDirection(4, 5, Vector3.back, Vector3.right).x, 0f);
        }

        // ── 해킹 드론 뜯어내기(장면 8) ──

        static DroneSettings HackSettings() => new DroneSettings { hackGrabRadius = 9f, hackGrabSeconds = 0.25f, hackPullDistance = 16f };

        [Test]
        public void HackPull_GrabNearThenPullAwayTearsOff()
        {
            var s = HackSettings();
            var pull = new HackPull();
            Assert.IsFalse(pull.Step(false, 5f, 0.1f, s));
            Assert.IsFalse(pull.Step(true, 5f, 0.1f, s));   // 가까이에서 새로 쥠
            Assert.IsFalse(pull.Holding);
            Assert.IsFalse(pull.Step(true, 5f, 0.1f, s));
            Assert.IsFalse(pull.Step(true, 5f, 0.1f, s));   // 0.25초 넘게 버팀 → 붙잡음
            Assert.IsTrue(pull.Holding);
            Assert.IsFalse(pull.Step(true, 12f, 0.1f, s));  // 당기는 중
            Assert.IsTrue(pull.Step(true, 17f, 0.1f, s));   // 뜯김
            Assert.IsFalse(pull.Holding);
            Assert.IsFalse(pull.Step(true, 18f, 0.1f, s));  // 한 번만
        }

        [Test]
        public void HackPull_HandsMerelyCloseDoesNothing()
        {
            // 가드 자세: 그립을 쥔 채 두 손이 가까워졌다 멀어져도 풀리지 않는다(예전 12 m 버그).
            var s = HackSettings();
            var pull = new HackPull();
            Assert.IsFalse(pull.Step(true, 30f, 0.1f, s));  // 멀리서 쥠
            for (var i = 0; i < 20; i++) Assert.IsFalse(pull.Step(true, 4f, 0.1f, s));
            Assert.IsFalse(pull.Holding);
            Assert.IsFalse(pull.Step(true, 30f, 0.1f, s));
            // 그립 없이 가까이 있기만 해도 안 된다.
            pull.Reset();
            for (var i = 0; i < 20; i++) Assert.IsFalse(pull.Step(false, 4f, 0.1f, s));
            Assert.IsFalse(pull.Step(false, 30f, 0.1f, s));
        }

        [Test]
        public void HackPull_QuickPunchFromCloseDoesNotTear()
        {
            // 가까이에서 쥐자마자 뻗는 펀치: 붙잡기 전에 손이 벗어나 무효.
            var s = HackSettings();
            var pull = new HackPull();
            pull.Step(false, 5f, 0.1f, s);
            Assert.IsFalse(pull.Step(true, 5f, 0.1f, s));
            Assert.IsFalse(pull.Step(true, 20f, 0.1f, s));
            Assert.IsFalse(pull.Holding);
            Assert.IsFalse(pull.Step(true, 5f, 1f, s));     // 그립을 놓지 않고 돌아와도 무효
            Assert.IsFalse(pull.Holding);
        }

        [Test]
        public void HackPull_ReleasingGripLetsGo()
        {
            var s = HackSettings();
            var pull = new HackPull();
            pull.Step(false, 5f, 0.1f, s);
            pull.Step(true, 5f, 0.1f, s);
            pull.Step(true, 5f, 0.4f, s);
            Assert.IsTrue(pull.Holding);
            Assert.IsFalse(pull.Step(false, 12f, 0.1f, s));
            Assert.IsFalse(pull.Holding);
            Assert.IsFalse(pull.Step(true, 20f, 0.1f, s));  // 멀리서 다시 쥐어도 뜯기지 않는다
        }
    }
}
