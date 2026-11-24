using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>교전 규칙 안에 들어간 돌격형. 장면 1("첫 상대")을 처음부터 끝까지 돌린다.</summary>
    public class SkirmishDuelTests
    {
        const float Dt = 0.02f;

        static SkirmishSession Scene1()
        {
            var s = new SkirmishSession(SkirmishSettings.Scene1());
            s.SetReady(true, true);
            return s;
        }

        /// <summary>자동 조작으로 돌리며 사건을 모은다. gunner가 false면 P2는 아무것도 하지 않는다.</summary>
        static List<SkirmishEventKind> Play(SkirmishSession s, float seconds, bool gunner = true, System.Func<SkirmishSession, bool> until = null)
        {
            var seen = new List<SkirmishEventKind>();
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                s.SetMech(SkirmishBots.Mech(s.State, s.Settings));
                s.SetGunner(gunner ? SkirmishBots.Gunner(s.State, s.Settings) : default);
                s.Tick(Dt);
                seen.AddRange(s.Events.Select(e => e.Kind));
                if (until != null && until(s)) break;
            }
            return seen;
        }

        [Test]
        public void 장면_1을_두_사람이_끝까지_깬다()
        {
            var s = Scene1();
            var seen = Play(s, 120f, until: x => x.State.phase != SkirmishPhase.Fighting);

            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.AreEqual(2, s.State.gruntsDown);
            Assert.AreEqual(100f, s.State.hull, "전부 막았으면 피해가 없다");
            CollectionAssert.Contains(seen, SkirmishEventKind.GruntTelegraph);
            CollectionAssert.Contains(seen, SkirmishEventKind.GruntBlocked);
            CollectionAssert.Contains(seen, SkirmishEventKind.GuardClosed, "둘째 적은 가드를 굳힌다");
            CollectionAssert.Contains(seen, SkirmishEventKind.GuardBroken, "P2의 포가 가드를 연다");
            Assert.AreEqual(2, seen.Count(k => k == SkirmishEventKind.GruntDown));
            Assert.Less(s.State.elapsed, 75f, "장면 1은 1분 15초 안쪽");
        }

        [Test]
        public void 둘째_적은_첫째가_쓰러지고_2초_뒤에_나온다()
        {
            var s = Scene1();
            Play(s, 60f, until: x => x.State.gruntsDown == 1);
            Assert.AreEqual(0, s.State.gruntIndex);
            float downAt = s.State.elapsed;
            Play(s, 10f, until: x => x.State.gruntIndex == 1);
            Assert.AreEqual(2f, s.State.elapsed - downAt, 0.05f);
            Assert.AreEqual(new Vector3(15f, 0f, 135f), s.Settings.grunts[1].start);
        }

        [Test]
        public void P2가_없으면_가드를_열_수_없어_더_오래_걸린다()
        {
            var together = Scene1();
            Play(together, 120f, until: x => x.State.phase != SkirmishPhase.Fighting);

            var alone = Scene1();
            var seen = Play(alone, 240f, gunner: false, until: x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, alone.State.phase, "P2가 없어도 진행은 된다");
            CollectionAssert.DoesNotContain(seen, SkirmishEventKind.GuardBroken);
            Assert.Greater(alone.State.elapsed, together.State.elapsed, "P2가 있으면 더 빨리 끝난다");
        }

        [Test]
        public void 막지_않으면_맞고_내구도가_0이_되면_실패한다()
        {
            var s = Scene1();
            for (int i = 0; i < 6000 && s.State.phase == SkirmishPhase.Fighting; i++)
            {
                s.SetMech(default);
                s.Tick(Dt);
            }
            Assert.AreEqual(SkirmishPhase.Failed, s.State.phase);
            Assert.AreEqual(0f, s.State.hull);
        }

        [Test]
        public void 주먹은_번호가_바뀐_틱에_한_번만_들어간다()
        {
            var s = Scene1();
            Play(s, 30f, until: x => x.State.gruntStep == GruntStep.Stagger);
            float before = s.State.gruntHp;
            var input = new MechInput { punchSeq = 9999, punchDamage = 25f };
            for (int i = 0; i < 5; i++) { s.SetMech(input); s.Tick(Dt); }
            Assert.AreEqual(before - 25f, s.State.gruntHp, 0.001f, "같은 번호를 다섯 틱 보내도 한 대");
        }

        [Test]
        public void 포는_돌격형_체력을_조금만_깎고_쓰러뜨리지_못한다()
        {
            var s = Scene1();
            Play(s, 30f, until: x => x.State.gruntStep == GruntStep.Idle);
            for (int i = 0; i < 3000; i++)
            {
                s.SetMech(new MechInput { guard = true, guardSide = PilotSide.Left });
                var aim = (SkirmishSession.GruntCenter(s.State, s.Settings) - s.Settings.gunnerPost).normalized;
                s.SetGunner(new GunnerInput { fire = true, aim = aim });
                s.Tick(Dt);
            }
            Assert.IsTrue(s.State.GruntAlive);
            Assert.GreaterOrEqual(s.State.gruntHp, 1f);
            Assert.Less(s.State.gruntHp, 100f, "포가 닿기는 한다");
        }

        [Test]
        public void 기존_적치장_설정에는_돌격형이_나오지_않는다()
        {
            var s = new SkirmishSession();
            s.SetReady(true, true);
            for (int i = 0; i < 500; i++) s.Tick(Dt);
            Assert.AreEqual(-1, s.State.gruntIndex);
            Assert.IsTrue(s.State.enemies[SkirmishSession.G3].Alive);
        }

        [Test]
        public void 다시_시작하면_돌격형도_처음으로_돌아간다()
        {
            var s = Scene1();
            Play(s, 60f, until: x => x.State.gruntsDown == 1);
            s.Reset();
            s.SetReady(true, true);
            Assert.AreEqual(-1, s.State.gruntIndex);
            Assert.AreEqual(0, s.State.gruntsDown);
            s.Tick(Dt);
            Assert.AreEqual(0, s.State.gruntIndex);
            Assert.AreEqual(GruntStep.Approach, s.State.gruntStep);
        }

        [Test]
        public void 네트워크로_보낸_상태에_돌격형이_그대로_실린다()
        {
            var s = Scene1();
            Play(s, 30f, until: x => x.State.gruntStep == GruntStep.Hold);
            var shown = new SkirmishState();
            NetSkirmishState.From(s.State, Vector3.forward, Vector3.forward).CopyTo(shown);
            Assert.AreEqual(s.State.gruntIndex, shown.gruntIndex);
            Assert.AreEqual(s.State.gruntStep, shown.gruntStep);
            Assert.AreEqual(s.State.gruntAttack, shown.gruntAttack);
            Assert.AreEqual(s.State.gruntPosition, shown.gruntPosition);
            Assert.AreEqual(s.State.gruntHp, shown.gruntHp);
            Assert.AreEqual(s.State.guardSide, shown.guardSide);
        }
    }
}
