using System.Linq;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>보스전 마무리(기획안 2026-11-28): 느린 시간 속에서 P1이 받아 내고, P2가 가슴을 터뜨리고, 둘이 같은 때에 넣어야 끝난다. 여기서는 지지 않는다.</summary>
    public class BossFinaleTests
    {
        const float Dt = 0.02f;

        /// <summary>맞붙은 지 1초 뒤에 마무리가 시작되는 보스전. 저절로 넘어가는 것은 끈다.</summary>
        static SkirmishSession Start(float auto = 0f, bool solo = false)
        {
            var settings = SkirmishSettings.BossAct1();
            settings.finaleAfterSeconds = 1f;
            settings.finaleAutoSeconds = auto;
            settings.droneEvery = 0f;
            var s = new SkirmishSession(settings) { Solo = solo };
            s.SetReady(true, true);
            return s;
        }

        static bool Run(SkirmishSession s, float seconds, System.Func<SkirmishState, bool> until, MechInput mech = default, GunnerInput gunner = default)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                s.SetMech(mech);
                s.SetGunner(gunner);
                s.Tick(Dt);
                if (until != null && until(s.State)) return true;
            }
            return until == null;
        }

        static GunnerInput AtChest(SkirmishSession s, bool fire = false, bool hold = false)
        {
            var aim = (s.State.weakPoint - s.Settings.gunnerPost).normalized;
            return new GunnerInput { aim = aim, pointer = aim, fire = fire, lockHeld = hold };
        }

        static SkirmishSession AtCatch()
        {
            var s = Start();
            Assert.IsTrue(Run(s, 20f, st => st.finale == FinaleStep.Catch), "보스가 칼을 든 뒤 P1의 차례가 온다");
            return s;
        }

        static SkirmishSession AtOpen()
        {
            var s = AtCatch();
            Assert.IsTrue(Run(s, 2f, st => st.finale == FinaleStep.Open, new MechInput { guard = true }), "막으면 받아 낸다");
            return s;
        }

        static SkirmishSession AtTogether()
        {
            var s = AtOpen();
            Assert.IsTrue(Run(s, 3f, st => st.finale == FinaleStep.Together, default, AtChest(s, fire: true)), "가슴을 맞히면 터진다");
            return s;
        }

        [Test]
        public void 시간이_다_되면_체력과_상관없이_마무리가_시작되고_보스는_더_공격하지_않는다()
        {
            var s = Start();
            Assert.IsTrue(Run(s, 20f, st => st.finale == FinaleStep.Raise));
            Assert.Greater(s.State.gruntHp, 1000f, "체력이 남아 있어도 시작한다");
            Assert.IsTrue(s.Events.Any(e => e.Kind == SkirmishEventKind.FinaleStarted));
            float hull = s.State.hull;
            Run(s, 10f, null);
            Assert.AreEqual(FinaleStep.Catch, s.State.finale, "아무도 안 하면 그 단계에서 기다린다");
            Assert.AreEqual(hull, s.State.hull, "마무리에서는 맞지 않는다");
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
            Assert.Greater(s.State.slowMotion, 0f, "마무리 내내 시간이 느리다");
            Assert.IsFalse(s.State.shells.Any(x => x.kind == ShellKind.BigShell), "자폭 드론은 오지 않는다");
        }

        [Test]
        public void P1이_막고_있어야_받아_낸다()
        {
            var s = AtCatch();
            Run(s, 0.2f, null, new MechInput { guard = true });
            Assert.AreEqual(FinaleStep.Catch, s.State.finale, "0.3초를 채우기 전");
            Run(s, 0.1f, null);
            Assert.AreEqual(0f, s.State.finaleProgress, "팔을 내리면 처음부터");
            Assert.IsTrue(Run(s, 1f, st => st.finale == FinaleStep.Open, new MechInput { guard = true }));
            Assert.IsTrue(s.Events.Any(e => e.Kind == SkirmishEventKind.FinaleCaught));
        }

        [Test]
        public void P2가_가슴의_표시를_쏴야_터진다()
        {
            var s = AtOpen();
            // 다른 곳을 쏘거나, 겨누기만 하고 안 쏘면 차지 않는다.
            Run(s, 2f, null, default, new GunnerInput { aim = Vector3.up, fire = true });
            Assert.AreEqual(0f, s.State.finaleProgress);
            Run(s, 2f, null, default, AtChest(s));
            Assert.AreEqual(0f, s.State.finaleProgress);
            Run(s, 1f, null, default, AtChest(s, fire: true));
            Assert.AreEqual(FinaleStep.Open, s.State.finale);
            Assert.Greater(s.State.finaleProgress, 0.5f);
            Assert.IsTrue(Run(s, 1f, st => st.finale == FinaleStep.Together, default, AtChest(s, fire: true)));
            Assert.IsTrue(s.Events.Any(e => e.Kind == SkirmishEventKind.FinaleOpened));
        }

        [Test]
        public void 한_사람만으로는_끝나지_않는다()
        {
            var s = AtTogether();
            // P1만: 주먹은 잠깐 유효하다가 사라진다.
            Run(s, Dt, null, new MechInput { punchSeq = 1, punchDamage = 25f });
            Assert.IsTrue(s.State.finaleMech);
            Run(s, 3f, null, new MechInput { punchSeq = 1, punchDamage = 25f });
            Assert.IsFalse(s.State.finaleMech, "1.5초가 지나면 다시 내야 한다");
            Assert.AreEqual(FinaleStep.Together, s.State.finale);
            // P2만.
            Run(s, 3f, null, new MechInput { punchSeq = 1, punchDamage = 25f }, AtChest(s, hold: true));
            Assert.IsTrue(s.State.finaleGunner);
            Assert.AreEqual(FinaleStep.Together, s.State.finale);
            Assert.AreEqual(-1, s.State.lockTarget, "마무리에서는 스캔이 돌지 않는다");
        }

        [Test]
        public void 둘이_같은_때에_하면_보스가_쓰러지고_끝난다()
        {
            var s = AtTogether();
            // P1이 먼저 주먹을 내고, 1초 뒤에 P2가 누른다.
            Run(s, 1f, null, new MechInput { punchSeq = 1, punchDamage = 25f });
            Assert.IsTrue(Run(s, 0.1f, st => st.finale == FinaleStep.Blow, new MechInput { punchSeq = 1, punchDamage = 25f }, AtChest(s, hold: true)));
            Assert.IsTrue(s.Events.Any(e => e.Kind == SkirmishEventKind.FinaleBlow));
            Assert.IsTrue(Run(s, 4f, st => st.phase == SkirmishPhase.Cleared));
            Assert.AreEqual(0f, s.State.gruntHp);
            Assert.AreEqual(GruntStep.Dead, s.State.gruntStep);
            Assert.AreEqual(1, s.State.gruntsDown);
        }

        [Test]
        public void 아무도_안_하면_15초마다_저절로_넘어가서_멈춰_있지_않는다()
        {
            var s = Start(auto: 15f);
            Assert.IsTrue(Run(s, 60f, st => st.phase == SkirmishPhase.Cleared), "1 + 1.2 + 15 × 3 + 2초 안에 끝난다");
        }

        [Test]
        public void 혼자_할_때는_P2의_몫을_자동이_한다()
        {
            var s = Start(solo: true);
            Assert.IsTrue(Run(s, 20f, st => st.finale == FinaleStep.Catch));
            Assert.IsTrue(Run(s, 1f, st => st.finale == FinaleStep.Open, new MechInput { guard = true }));
            Assert.IsTrue(Run(s, 3f, st => st.finale == FinaleStep.Together), "가슴은 저절로 터진다");
            Run(s, 1f, null);
            Assert.AreEqual(FinaleStep.Together, s.State.finale, "주먹은 P1이 내야 한다");
            Assert.IsTrue(Run(s, 0.1f, st => st.finale == FinaleStep.Blow, new MechInput { punchSeq = 1, punchDamage = 25f }));
        }

        [Test]
        public void 자동_조작_둘이_마무리를_끝낸다()
        {
            var settings = SkirmishSettings.BossAct1();
            settings.finaleAfterSeconds = 1f;
            settings.finaleAutoSeconds = 0f;
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            bool done = false;
            for (float t = 0f; t < 30f && !done; t += Dt)
            {
                s.SetMech(SkirmishBots.Mech(s.State, settings));
                s.SetGunner(SkirmishBots.Gunner(s.State, settings));
                s.Tick(Dt);
                done = s.State.phase == SkirmishPhase.Cleared;
            }
            Assert.IsTrue(done);
        }
    }
}
