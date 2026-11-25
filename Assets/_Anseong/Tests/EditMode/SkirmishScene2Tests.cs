using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>장면 2 "적치장"의 돌격형 쪽: 서 있는 적의 큰 탄, 두 명이 같이 올 때의 규칙, 내구도 0 재기동, 길과 마당.</summary>
    public class SkirmishScene2Tests
    {
        const float Dt = 0.02f;

        static SkirmishSession Scene2(bool solo = false)
        {
            var s = new SkirmishSession(SkirmishSettings.Scene2()) { Solo = solo };
            s.SetReady(true, true);
            return s;
        }

        /// <summary>자동 조작으로 돌리며 사건을 모은다.</summary>
        static List<SkirmishEvent> Play(SkirmishSession s, float seconds, System.Func<SkirmishSession, bool> until = null)
        {
            var seen = new List<SkirmishEvent>();
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                s.SetMech(SkirmishBots.Mech(s.State, s.Settings));
                s.SetGunner(SkirmishBots.Gunner(s.State, s.Settings));
                s.Tick(Dt);
                seen.AddRange(s.Events);
                if (until != null && until(s)) return seen;
            }
            if (until != null) Assert.Fail($"제한 시간 안에 조건이 되지 않음. 상태 {s.State.phase} / 돌격형 {s.State.gruntIndex} {s.State.gruntStep} / 뒤 {s.State.backIndex} {s.State.backStep}");
            return seen;
        }

        [Test]
        public void 돌격형_3은_서_있다가_로봇이_90을_지나면_큰_탄을_한_번_쏜다()
        {
            var s = Scene2();
            var north = new MechInput { move = Vector3.forward };
            var seen = new List<SkirmishEvent>();
            float windupAt = -1f, firedAt = -1f;
            for (int i = 0; i < 400; i++)
            {
                s.SetMech(north);
                s.Tick(Dt);
                seen.AddRange(s.Events);
                if (windupAt < 0f && s.Events.Any(e => e.Kind == SkirmishEventKind.GruntShellWindup)) windupAt = s.State.elapsed;
                if (firedAt < 0f && s.Events.Any(e => e.Kind == SkirmishEventKind.ShellFired)) firedAt = s.State.elapsed;
                if (windupAt < 0f) Assert.Less(s.State.mech.z, 90.5f, "90을 지나면 바로 겨눈다");
            }
            Assert.AreEqual(new Vector3(0f, 0f, 222f), s.State.gruntPosition, "제자리에 서 있다");
            Assert.Greater(windupAt, 0f);
            Assert.AreEqual(2f, firedAt - windupAt, 0.03f, "2초 겨눈 뒤 발사");
            Assert.AreEqual(1, seen.Count(e => e.Kind == SkirmishEventKind.ShellFired), "한 번만 쏜다");
        }

        [Test]
        public void 돌격형_3은_로봇이_22_안으로_와야_싸운다()
        {
            var s = Scene2();
            Play(s, 60f, x => x.State.gruntStep != GruntStep.Approach);
            Assert.AreEqual(0, s.State.gruntIndex);
            Assert.AreEqual(22f, Vector3.Distance(s.State.mech, s.State.gruntPosition), 0.3f);
            Assert.AreEqual(200f, s.State.mech.z, 0.3f, "로봇이 싸우는 자리는 (0, 0, 200) 근처");
        }

        [Test]
        public void 둘이_같이_오면_한_명만_맞붙고_다른_한_명은_35에서_기다리며_큰_탄을_쏜다()
        {
            var s = Scene2();
            Play(s, 120f, x => x.State.gruntsDown == 1);
            float downAt = s.State.elapsed;
            Play(s, 10f, x => x.State.gruntIndex == 1);
            Assert.AreEqual(2f, s.State.elapsed - downAt, 0.05f, "2초 뒤 4와 5가 동시에 출발");
            Assert.AreEqual(2, s.State.backIndex);

            Play(s, 30f, x => x.State.backStep == GruntStep.Waiting);
            float waitingAt = s.State.elapsed;
            Assert.AreEqual(35f, Vector3.Distance(s.State.mech, s.State.backPosition), 0.5f);

            // 기다리는 적은 6초마다 쏜다(2초 겨눔 포함). 맞붙은 4를 잡기 전에 두 번은 본다.
            var fired = new List<float>();
            float hp = s.State.backHp;
            for (int i = 0; i < 900 && fired.Count < 2 && s.State.gruntIndex == 1; i++)
            {
                s.SetMech(new MechInput { guard = true, guardSide = GruntDuel.IncomingSide(s.State.gruntAttack), guardDirection = Vector3.forward });
                s.Tick(Dt);
                if (s.Events.Any(e => e.Kind == SkirmishEventKind.ShellFired)) fired.Add(s.State.elapsed);
            }
            Assert.AreEqual(2, fired.Count);
            Assert.AreEqual(6f, fired[0] - waitingAt, 0.1f);
            Assert.AreEqual(6f, fired[1] - fired[0], 0.1f);
            Assert.AreEqual(hp, s.State.backHp, "기다리는 적은 주먹이 닿지 않는다");
        }

        [Test]
        public void 맞붙던_적이_쓰러지면_3초_뒤에_기다리던_적이_들어온다()
        {
            var s = Scene2();
            Play(s, 200f, x => x.State.gruntsDown == 2);
            float downAt = s.State.elapsed;
            Assert.AreEqual(2, s.State.backIndex, "5는 아직 뒤에 있다");
            Play(s, 10f, x => x.State.gruntIndex == 2);
            Assert.AreEqual(3f, s.State.elapsed - downAt, 0.05f);
            Assert.AreEqual(-1, s.State.backIndex);
            Assert.AreEqual(GruntStep.Approach, s.State.gruntStep);
            Play(s, 20f, x => x.State.gruntStep == GruntStep.Idle);
            Assert.AreEqual(22f, Vector3.Distance(s.State.mech, s.State.gruntPosition), 0.3f);
        }

        [Test]
        public void 드론이_기다리는_적을_멈추면_그동안은_큰_탄을_쏘지_않는다()
        {
            var s = Scene2();
            Play(s, 150f, x => x.State.backStep == GruntStep.Waiting);
            Assert.IsTrue(s.FreezeGrunt(2, 6f));
            Assert.IsFalse(s.FreezeGrunt(7, 6f), "없는 번호");
            Assert.AreEqual(GruntStep.Frozen, s.State.backStep);
            var guardOnly = new MechInput { guard = true, guardSide = PilotSide.Left, guardDirection = Vector3.forward };
            for (int i = 0; i < 295; i++)
            {
                s.SetMech(guardOnly);
                s.Tick(Dt);
                Assert.IsFalse(s.Events.Any(e => e.Kind == SkirmishEventKind.GruntShellWindup || e.Kind == SkirmishEventKind.ShellFired), "멈춘 6초 동안");
            }
            for (int i = 0; i < 20; i++) { s.SetMech(guardOnly); s.Tick(Dt); }
            Assert.AreEqual(GruntStep.Waiting, s.State.backStep, "풀리면 다시 기다린다");
        }

        [Test]
        public void 혼자_할_때는_5가_4_뒤에_출발하고_큰_탄을_쏘지_않는다()
        {
            var s = Scene2(solo: true);
            var seen = Play(s, 200f, x => x.State.gruntsDown == 2);
            Assert.AreEqual(-1, s.State.backIndex, "뒤에서 기다리는 적이 없다");
            // 서 있는 돌격형 3의 한 발만 나온다.
            Assert.AreEqual(1, seen.Count(e => e.Kind == SkirmishEventKind.ShellFired));
            float downAt = s.State.elapsed;
            Play(s, 10f, x => x.State.gruntIndex == 2);
            Assert.AreEqual(2f, s.State.elapsed - downAt, 0.05f);
        }

        [Test]
        public void 장면_2의_돌격형_셋을_자동_조작이_끝까지_깬다()
        {
            var s = Scene2();
            var seen = Play(s, 240f, x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.AreEqual(3, s.State.gruntsDown);
            Assert.Greater(s.State.hull, 0f);
            Assert.IsTrue(seen.Any(e => e.Kind == SkirmishEventKind.GruntTelegraph && e.Id == (int)GruntAttack.Swing), "휘두르기가 섞여 나온다");
            Assert.IsTrue(seen.Any(e => e.Kind == SkirmishEventKind.GruntTelegraph && e.Id == (int)GruntAttack.Slam));
            Debug.Log($"장면 2 돌격형 쪽: {s.State.elapsed:0.0}초, 내구도 {s.State.hull:0}");
        }

        [Test]
        public void 길에서는_옆으로_못_나가고_마당에서는_넓게_움직인다()
        {
            var s = Scene2(solo: true);
            for (int i = 0; i < 300; i++) { s.SetMech(new MechInput { move = Vector3.right }); s.Tick(Dt); }
            Assert.AreEqual(14f, s.State.mech.x, 0.001f, "길의 동쪽 끝");
            Assert.AreEqual(65f, s.State.mech.z, 0.001f);

            var free = new SkirmishSession(new SkirmishSettings { mechStart = new Vector3(0f, 0f, 200f), mechSpeed = 10f, rangedEnemies = false, mechZones = SkirmishSettings.Scene2().mechZones,
                // 적이 하나도 없으면 바로 끝나므로, 멀리 서 있는 적을 하나 둔다.
                grunts = new[] { new GruntWave { start = new Vector3(0f, 0f, 2000f), spec = new GruntSpec { standStill = true } } },
            });
            free.SetReady(true, true);
            for (int i = 0; i < 500; i++) { free.SetMech(new MechInput { move = Vector3.right }); free.Tick(Dt); }
            Assert.AreEqual(60f, free.State.mech.x, 0.001f, "마당의 동쪽 끝");
            for (int i = 0; i < 500; i++) { free.SetMech(new MechInput { move = Vector3.back }); free.Tick(Dt); }
            Assert.AreEqual(185f, free.State.mech.z, 0.001f, "마당 동쪽에서는 남쪽 길로 못 들어간다");
        }

        [Test]
        public void 내구도가_0이_되면_한_번은_재기동해_장면_처음으로_돌아간다()
        {
            var s = Scene2();
            var seen = new List<SkirmishEvent>();
            void Idle(System.Func<bool> until, int limit = 20000)
            {
                for (int i = 0; i < limit && !until(); i++)
                {
                    s.SetMech(new MechInput { move = s.State.gruntStep == GruntStep.Approach ? Vector3.forward : Vector3.zero });
                    s.Tick(Dt);
                    seen.AddRange(s.Events);
                }
                Assert.IsTrue(until(), "상태 " + s.State.phase);
            }
            Idle(() => s.State.phase == SkirmishPhase.Rebooting);
            Assert.AreEqual(0f, s.State.hull);
            Assert.AreEqual(0, s.State.rebootsLeft);
            int epoch = s.State.epoch;
            float at = 0f;
            for (int i = 0; i < 400 && s.State.phase == SkirmishPhase.Rebooting; i++) { s.Tick(Dt); seen.AddRange(s.Events); at += Dt; }
            Assert.AreEqual(3f, at, 0.03f, "기체 재기동 3초");
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
            Assert.AreEqual(epoch + 1, s.State.epoch);
            Assert.AreEqual(100f, s.State.hull);
            Assert.AreEqual(new Vector3(0f, 0f, 70f), s.State.mech, "로봇은 (0, 0, 70)에서 다시");
            Assert.AreEqual(-1, s.State.gruntIndex);
            Assert.AreEqual(0, s.State.gruntsDown);
            Assert.AreEqual(0, s.State.shells.Count);
            Assert.AreEqual(1, seen.Count(e => e.Kind == SkirmishEventKind.SceneRestarted));

            // 두 번째는 실패.
            Idle(() => s.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Failed, s.State.phase);
            Assert.AreEqual(1, seen.Count(e => e.Kind == SkirmishEventKind.Rebooting));

            s.Reset();
            Assert.AreEqual(1, s.State.rebootsLeft, "운영자가 다시 시작하면 재기동도 돌아온다");
        }

        [Test]
        public void 기다리는_적과_재기동이_P2에게_그대로_간다()
        {
            var s = Scene2();
            Play(s, 150f, x => x.State.backStep == GruntStep.Waiting && x.State.backShell > 0f);
            var shown = new SkirmishState();
            NetSkirmishState.From(s.State, Vector3.forward, Vector3.forward).CopyTo(shown);
            Assert.AreEqual(2, shown.backIndex);
            Assert.AreEqual(s.State.backPosition, shown.backPosition);
            Assert.AreEqual(GruntStep.Waiting, shown.backStep);
            Assert.AreEqual(s.State.backShell, shown.backShell);
            Assert.AreEqual(1, shown.rebootsLeft);

            s.State.phase = SkirmishPhase.Rebooting;
            s.State.rebootTimer = 1.5f;
            NetSkirmishState.From(s.State, Vector3.forward, Vector3.forward).CopyTo(shown);
            Assert.AreEqual(SkirmishPhase.Rebooting, shown.phase);
            Assert.AreEqual(1.5f, shown.rebootTimer);
        }
    }
}
