using System.Collections.Generic;
using System.Linq;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>보스전 앞의 장면들(기획안 5절): 준비(P2 이륙, 조작을 한 번씩) → 자폭 드론 여섯 → 등장 영상 → 보스전.</summary>
    public class BossIntroTests
    {
        const float Dt = 0.02f;

        static SkirmishSession Start(bool solo = false)
        {
            var settings = SkirmishSettings.BossAct1();
            settings.intro = true;
            var s = new SkirmishSession(settings) { Solo = solo };
            s.SetReady(true, true);
            return s;
        }

        /// <summary>자동 조작 둘(또는 가만히 있는 둘)로 돌린다. 그동안 나온 사건을 모은다.</summary>
        static List<SkirmishEvent> Run(SkirmishSession s, float seconds, System.Func<SkirmishState, bool> until, bool bots = true)
        {
            var seen = new List<SkirmishEvent>();
            for (float t = 0f; t < seconds; t += Dt)
            {
                s.SetMech(bots ? SkirmishBots.Mech(s.State, s.Settings) : default);
                s.SetGunner(bots ? SkirmishBots.Gunner(s.State, s.Settings) : default);
                s.Tick(Dt);
                seen.AddRange(s.Events);
                if (until != null && until(s.State)) break;
            }
            return seen;
        }

        [Test]
        public void 앞_장면을_켜지_않으면_보스가_바로_나온다()
        {
            var s = new SkirmishSession(SkirmishSettings.BossAct1());
            s.SetReady(true, true);
            s.Tick(Dt); s.Tick(Dt);
            Assert.AreEqual(SceneStage.None, s.State.stage);
            Assert.AreEqual(0, s.State.gruntIndex);
            Assert.AreEqual(1f, s.State.lift);
        }

        [Test]
        public void 준비_장면에서는_보스가_없고_헬기가_15초에_걸쳐_떠오른다()
        {
            var s = Start();
            Run(s, 7.5f, null, bots: false);
            Assert.AreEqual(SceneStage.Setup, s.State.stage);
            Assert.AreEqual(-1, s.State.gruntIndex, "보스는 아직 없다");
            Assert.AreEqual(0.5f, s.State.lift, 0.01f);
            var at = SkirmishSession.GunnerAt(s.State, s.Settings);
            Assert.AreEqual((s.Settings.gunnerStart.y + s.Settings.gunnerPost.y) * 0.5f, at.y, 0.5f);
            Run(s, 8f, null, bots: false);
            Assert.AreEqual(1f, s.State.lift);
            Assert.AreEqual(s.Settings.gunnerPost, SkirmishSession.GunnerAt(s.State, s.Settings));
        }

        [Test]
        public void 떠오르는_동안에는_포가_나가지_않는다()
        {
            var s = Start();
            var aim = (s.Settings.wrecks[0] + Vector3.up * 2f - s.Settings.gunnerPost).normalized;
            bool shot = false;
            for (float t = 0f; t < 10f; t += Dt)
            {
                s.SetGunner(new GunnerInput { fire = true, aim = aim, pointer = aim });
                s.Tick(Dt);
                shot |= s.Events.Any(e => e.Kind == SkirmishEventKind.CannonShot);
            }
            Assert.IsFalse(shot);
            Assert.AreEqual(0, s.State.tried & SkirmishState.TryWreck);
        }

        [Test]
        public void 둘_다_해_보면_45초가_되기_전에_넘어간다()
        {
            var s = Start();
            var seen = Run(s, 60f, st => st.stage == SceneStage.Drones);
            Assert.AreEqual(SceneStage.Drones, s.State.stage);
            Assert.Less(s.State.statSeconds, 30f);
            Assert.AreEqual(3, seen.Count(e => e.Kind == SkirmishEventKind.WreckHit), "폐차 셋");
        }

        [Test]
        public void 아무것도_안_해도_45초에_넘어간다()
        {
            var s = Start();
            Run(s, 44f, null, bots: false);
            Assert.AreEqual(SceneStage.Setup, s.State.stage);
            Run(s, 2f, null, bots: false);
            Assert.AreEqual(SceneStage.Drones, s.State.stage);
        }

        [Test]
        public void 혼자_할_때는_P1만_해_보면_된다()
        {
            var s = Start(solo: true);
            s.Tick(Dt);
            for (float t = 0f; t < 20f && s.State.stage == SceneStage.Setup; t += Dt)
            {
                s.SetMech(SkirmishBots.Mech(s.State, s.Settings));
                s.Tick(Dt);
            }
            Assert.AreEqual(SceneStage.Drones, s.State.stage);
        }

        [Test]
        public void 자폭_드론_여섯이_오고_넷째는_빠르다_그다음은_등장_영상과_보스전()
        {
            var s = Start();
            Run(s, 60f, st => st.stage == SceneStage.Drones);
            var speeds = new List<float>();
            var known = new HashSet<int>();
            for (float t = 0f; t < 120f && s.State.stage == SceneStage.Drones; t += Dt)
            {
                s.SetMech(SkirmishBots.Mech(s.State, s.Settings));
                s.SetGunner(SkirmishBots.Gunner(s.State, s.Settings));
                s.Tick(Dt);
                foreach (var shell in s.State.shells)
                    if (shell.kind == ShellKind.BigShell && known.Add(shell.id)) speeds.Add(shell.velocity.magnitude);
            }
            Assert.AreEqual(6, speeds.Count);
            Assert.Greater(speeds[3], speeds[0] * 2f, "넷째는 빠르다");
            Assert.AreEqual(speeds[0], speeds[4], 0.01f);
            Assert.AreEqual(SceneStage.Entrance, s.State.stage);
            Assert.AreEqual(-1, s.State.gruntIndex);
            Run(s, 19f, null);
            Assert.AreEqual(SceneStage.Entrance, s.State.stage);
            Run(s, 2f, st => st.gruntIndex >= 0);
            Assert.AreEqual(SceneStage.Fight, s.State.stage);
            Assert.AreEqual(0, s.State.gruntIndex, "보스가 나온다");
        }

        [Test]
        public void 운영자가_장면을_넘길_수_있다()
        {
            var s = Start();
            s.Tick(Dt);
            s.SkipStage();
            Assert.AreEqual(SceneStage.Drones, s.State.stage);
            Assert.AreEqual(1f, s.State.lift);
            s.SkipStage();
            Assert.AreEqual(SceneStage.Entrance, s.State.stage);
            s.SkipStage();
            Assert.AreEqual(SceneStage.Fight, s.State.stage);
            s.SkipStage();
            Assert.AreEqual(SceneStage.Fight, s.State.stage);
        }

        [Test]
        public void 자폭_드론_장면에서는_지지_않는다()
        {
            var s = Start();
            s.Tick(Dt);
            s.SkipStage();
            Run(s, 200f, st => st.stage == SceneStage.Entrance, bots: false);
            Assert.AreEqual(SceneStage.Entrance, s.State.stage, "한 번 다시 한 뒤에는 그냥 넘어간다");
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
            Assert.AreEqual(s.Settings.mechHull, s.State.hull);
            Assert.AreEqual(s.Settings.reboots, s.State.rebootsLeft, "재기동 횟수를 쓰지 않는다");
        }

        [Test]
        public void 자동_조작_둘이_처음부터_끝까지_간다()
        {
            var s = Start();
            Run(s, 900f, st => st.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
        }
    }
}
