using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>제작 명세의 타격 멈춤(0.06초), 마무리 멈춤(0.1초), 딱 맞춰 막기의 느린 시간(0.15초, 0.3배), 차폐판 파손.</summary>
    public class HitStopAndShieldTests
    {
        const float Dt = 0.02f;
        static readonly Vector3 Robot = new Vector3(0f, 0f, 65f);

        /// <summary>막기에 성공해 튕겨난 적(가드를 굳히지 않는 설정).</summary>
        static GruntDuel Staggered(GruntSpec spec = null, GruntSettings settings = null)
        {
            var g = new GruntDuel(Robot + Vector3.forward * 22.01f, spec ?? new GruntSpec { guardEveryDamage = 0f }, settings);
            for (int i = 0; i < 1000 && g.Step != GruntStep.Stagger; i++) g.Tick(Dt, Robot, true, PilotSide.Left);
            Assert.AreEqual(GruntStep.Stagger, g.Step);
            return g;
        }

        [Test]
        public void 주먹이_꽂히면_006초_동안_적의_시간이_멈춘다()
        {
            var g = Staggered();
            float timer = g.Timer;
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(0.06f, g.Stop, 0.0001f);

            g.Tick(Dt, Robot, false, PilotSide.Left);
            g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(timer, g.Timer, 0.0001f, "멈춘 동안 튕겨남 시간이 줄지 않는다");
            Assert.AreEqual(GruntStep.Stagger, g.Step);

            g.Tick(Dt, Robot, false, PilotSide.Left);
            g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(0f, g.Stop, 0.0001f);
            Assert.AreEqual(timer - 0.02f, g.Timer, 0.001f, "0.08초 가운데 0.06초는 멈춰 있었다");
        }

        [Test]
        public void 가드에_튕긴_주먹은_멈추지_않는다()
        {
            var g = Staggered(new GruntSpec { firstGuardDamage = 25f, guardEveryDamage = 60f });
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(GruntStep.Guard, g.Step);
            for (int i = 0; i < 5; i++) g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(0f, g.Stop, 0.0001f);

            Assert.AreEqual(PunchResult.Bounced, g.Punch(25f));
            Assert.AreEqual(0f, g.Stop, "튕길 때는 멈추지 않는다");
        }

        [Test]
        public void 마무리_멈춤_동안에는_포가_체력을_되살리지_않는다()
        {
            var g = Staggered();
            for (int i = 0; i < 3; i++) g.Punch(25f);
            Assert.AreEqual(PunchResult.Finished, g.Punch(25f));
            g.CannonHit(4f);
            Assert.AreEqual(0f, g.Hp);
            for (int i = 0; i < 6; i++) g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.IsFalse(g.Alive);
        }

        [Test]
        public void 멈춤_시간이_0이면_바로_쓰러진다()
        {
            var g = Staggered(settings: new GruntSettings { hitStopSeconds = 0f, finishStopSeconds = 0f });
            for (int i = 0; i < 3; i++) g.Punch(25f);
            Assert.AreEqual(0f, g.Stop);
            Assert.AreEqual(PunchResult.Finished, g.Punch(25f));
            Assert.IsFalse(g.Alive);
            Assert.AreEqual(1, g.Events.Count(e => e.Kind == GruntEventKind.Down));
        }

        // ── 교전 규칙 안에서 ──

        static SkirmishSession Scene1()
        {
            var s = new SkirmishSession(SkirmishSettings.Scene1());
            s.SetReady(true, true);
            return s;
        }

        /// <summary>첫 적이 내려치기 시작할 때까지 아무것도 하지 않다가, 그때부터 막아서 딱 맞춰 막는다.</summary>
        static SkirmishSession PerfectBlocked(Vector3 move = default)
        {
            var s = Scene1();
            for (int i = 0; i < 3000 && s.State.gruntStep != GruntStep.Strike; i++) { s.SetMech(default); s.Tick(Dt); }
            Assert.AreEqual(GruntStep.Strike, s.State.gruntStep);
            for (int i = 0; i < 100; i++)
            {
                s.SetMech(new MechInput { guard = true, guardSide = PilotSide.Left, move = move });
                s.Tick(Dt);
                if (s.Events.Any(e => e.Kind == SkirmishEventKind.GruntPerfectBlock)) return s;
            }
            Assert.Fail("딱 맞춰 막기가 나오지 않음");
            return s;
        }

        [Test]
        public void 딱_맞춰_막으면_015초_동안_시간이_03배로_흐른다()
        {
            var s = PerfectBlocked();
            Assert.AreEqual(0.15f, s.State.slowMotion, 0.0001f);
            Assert.AreEqual(100f, s.State.hull);
            float start = s.State.elapsed;

            for (int i = 0; i < 5; i++) s.Tick(Dt);
            Assert.AreEqual(0.1f * 0.3f, s.State.elapsed - start, 0.001f, "실제 0.1초 동안 0.03초만 흐른다");
            Assert.AreEqual(0.05f, s.State.slowMotion, 0.001f);

            for (int i = 0; i < 5; i++) s.Tick(Dt);
            Assert.AreEqual(0.15f * 0.3f + 0.05f, s.State.elapsed - start, 0.001f, "0.15초가 지나면 평소 속도");
            Assert.AreEqual(0f, s.State.slowMotion);
        }

        [Test]
        public void 느린_시간에도_로봇의_이동은_느려지지_않는다()
        {
            var s = PerfectBlocked();
            for (int i = 0; i < 5; i++)
            {
                s.SetMech(new MechInput { move = Vector3.right });
                s.Tick(Dt);
            }
            Assert.Greater(s.State.slowMotion, 0f, "아직 느린 시간 안");
            // 0.4초에 전속력(10)이 되므로 0.1초 뒤에는 2.5.
            Assert.AreEqual(2.5f, s.MechVelocity.x, 0.01f);
        }

        [Test]
        public void 미리_들고_막으면_시간이_느려지지_않는다()
        {
            var s = Scene1();
            bool blocked = false;
            for (int i = 0; i < 3000 && !blocked; i++)
            {
                s.SetMech(new MechInput { guard = true, guardSide = PilotSide.Left });
                s.Tick(Dt);
                blocked = s.Events.Any(e => e.Kind == SkirmishEventKind.GruntBlocked);
                Assert.IsFalse(s.Events.Any(e => e.Kind == SkirmishEventKind.GruntPerfectBlock));
            }
            Assert.IsTrue(blocked);
            Assert.AreEqual(0f, s.State.slowMotion);
        }

        [Test]
        public void 차폐판은_한_번만_부서지고_그_뒤에도_막기는_된다()
        {
            var s = Scene1();
            Assert.IsTrue(s.State.shieldOpen);
            s.BreakShield();
            s.Tick(Dt);
            Assert.IsTrue(s.State.shieldBroken);
            Assert.IsFalse(s.State.shieldOpen);
            Assert.AreEqual(1, s.Events.Count(e => e.Kind == SkirmishEventKind.ShieldBroken));

            s.BreakShield();
            s.Tick(Dt);
            Assert.AreEqual(0, s.Events.Count(e => e.Kind == SkirmishEventKind.ShieldBroken), "두 번 부서지지 않는다");

            // 맨팔로 막아도 판정은 같다: 자동 조작이 전부 막고 끝까지 깬다.
            for (int i = 0; i < 6000 && s.State.phase == SkirmishPhase.Fighting; i++)
            {
                s.SetMech(SkirmishBots.Mech(s.State, s.Settings));
                s.SetGunner(SkirmishBots.Gunner(s.State, s.Settings));
                s.Tick(Dt);
            }
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.AreEqual(100f, s.State.hull);

            s.Reset();
            Assert.IsFalse(s.State.shieldBroken, "다시 시작하면 차폐판도 돌아온다");
            Assert.IsTrue(s.State.shieldOpen);
        }

        [Test]
        public void 조종사가_차폐판을_접으면_상태에_실린다()
        {
            var s = Scene1();
            s.SetMech(new MechInput { shieldFolded = true });
            s.Tick(Dt);
            Assert.IsFalse(s.State.shieldOpen);
            s.SetMech(default);
            s.Tick(Dt);
            Assert.IsTrue(s.State.shieldOpen);
        }

        [Test]
        public void 차폐판과_느린_시간이_P2에게_그대로_간다()
        {
            var s = PerfectBlocked();
            s.BreakShield();
            s.Tick(Dt);
            var shown = new SkirmishState();
            NetSkirmishState.From(s.State, Vector3.forward, Vector3.forward).CopyTo(shown);
            Assert.IsTrue(shown.shieldBroken);
            Assert.IsFalse(shown.shieldOpen);
            Assert.AreEqual(s.State.slowMotion, shown.slowMotion);
            Assert.Greater(shown.slowMotion, 0f);

            var fresh = new SkirmishState();
            NetSkirmishState.From(Scene1().State, Vector3.forward, Vector3.forward).CopyTo(fresh);
            Assert.IsTrue(fresh.shieldOpen);
            Assert.IsFalse(fresh.shieldBroken);
        }
    }
}
