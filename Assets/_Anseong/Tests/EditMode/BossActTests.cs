using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>보스전 1막(기획안 2026-11-26 4절·5절): 대검 왼쪽 베기와 오른쪽 베기(방패 없음), 튕겨난 동안에만 피해, 강한 탄, 자폭 드론, 체력 800에서 끝.</summary>
    public class BossActTests
    {
        const float Dt = 0.02f;
        static readonly Vector3 Robot = new Vector3(0f, 0f, 65f);

        static GruntSpec BossSpec() => SkirmishSettings.BossAct1().grunts[0].spec;

        /// <summary>로봇 앞 22에 서서 기본 자세가 된 보스.</summary>
        static GruntDuel Boss(GruntSpec spec = null, bool solo = false)
        {
            var g = new GruntDuel(Robot + Vector3.forward * 22f, spec ?? BossSpec(), null, solo);
            g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(GruntStep.Idle, g.Step, "22 앞에서 시작하면 바로 기본 자세");
            return g;
        }

        static float RunUntil(GruntDuel g, System.Func<GruntDuel, bool> until, bool guarding = false, PilotSide side = PilotSide.Left, float limit = 30f)
        {
            float t = 0f;
            while (t < limit)
            {
                g.Tick(Dt, Robot, guarding, side);
                t += Dt;
                if (until(g)) return t;
            }
            Assert.Fail("제한 시간 안에 조건이 되지 않음. 상태: " + g.Step);
            return t;
        }

        static SkirmishSession Act1(bool solo = false)
        {
            var s = new SkirmishSession(SkirmishSettings.BossAct1()) { Solo = solo };
            s.SetReady(true, true);
            return s;
        }

        static List<SkirmishEventKind> Play(SkirmishSession s, float seconds, bool mech = true, bool gunner = true, System.Func<SkirmishSession, bool> until = null)
        {
            var seen = new List<SkirmishEventKind>();
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                s.SetMech(mech ? SkirmishBots.Mech(s.State, s.Settings) : default);
                s.SetGunner(gunner ? SkirmishBots.Gunner(s.State, s.Settings) : default);
                s.Tick(Dt);
                seen.AddRange(s.Events.Select(e => e.Kind));
                if (until != null && until(s)) break;
            }
            return seen;
        }

        [Test]
        public void 체력은_1200이고_혼자_할_때는_900이다()
        {
            Assert.AreEqual(1200f, Boss().Hp);
            Assert.AreEqual(900f, Boss(solo: true).Hp);
        }

        [Test]
        public void 튕겨난_동안에만_주먹이_들어간다()
        {
            var g = Boss();
            Assert.AreEqual(PunchResult.Bounced, g.Punch(25f), "기본 자세에서는 튕긴다");
            RunUntil(g, x => x.Step == GruntStep.Hold);
            Assert.AreEqual(PunchResult.Bounced, g.Punch(25f), "예고 중에도 튕긴다");
            Assert.AreEqual(1200f, g.Hp);

            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Left);
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(1175f, g.Hp);

            RunUntil(g, x => x.Step == GruntStep.Idle);
            Assert.AreEqual(PunchResult.Bounced, g.Punch(35f), "틈이 닫히면 다시 튕긴다");
            Assert.AreEqual(1175f, g.Hp);
        }

        [Test]
        public void 포도_튕겨난_동안에만_들어가고_그때는_온전히_들어간다()
        {
            var g = Boss();
            g.CannonHit(4f);
            Assert.AreEqual(1200f, g.Hp, "틈 밖에서는 안 박힌다");
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Left);
            g.CannonHit(4f);
            Assert.AreEqual(1196f, g.Hp, "틈에는 한 발 4가 그대로");
        }

        [Test]
        public void 예고는_1초2이고_처음_나올_때만_1점5배다()
        {
            var g = Boss();
            // 순서: 왼쪽, 오른쪽, 오른쪽, 왼쪽 … 첫 왼쪽과 첫 오른쪽은 1.8초, 그 뒤는 1.2초.
            var lengths = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                RunUntil(g, x => x.Step == GruntStep.Rise);
                lengths.Add(RunUntil(g, x => x.Step == GruntStep.Recover));
            }
            Assert.AreEqual(1.8f, lengths[0], 0.05f, "첫 왼쪽 베기");
            Assert.AreEqual(1.8f, lengths[1], 0.05f, "첫 오른쪽 베기");
            Assert.AreEqual(1.2f, lengths[2], 0.05f, "두 번째 오른쪽 베기");
            Assert.AreEqual(1.2f, lengths[3], 0.05f, "두 번째 왼쪽 베기");
        }

        [Test]
        public void 왼쪽_베기는_왼쪽으로_오른쪽_베기는_오른쪽으로_막아야_한다()
        {
            var g = Boss();
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(GruntAttack.Slam, g.Attack);
            RunUntil(g, x => x.Step != GruntStep.Rise && x.Step != GruntStep.Hold && x.Step != GruntStep.Strike, guarding: true, side: PilotSide.Right);
            Assert.AreEqual(GruntStep.Recover, g.Step, "왼쪽 베기를 오른쪽으로 막으면 맞는다");

            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(GruntAttack.Swing, g.Attack);
            RunUntil(g, x => x.Step != GruntStep.Rise && x.Step != GruntStep.Hold && x.Step != GruntStep.Strike, guarding: true, side: PilotSide.Right);
            Assert.AreEqual(GruntStep.Stagger, g.Step, "오른쪽 베기는 오른쪽으로 막는다");
        }

        [Test]
        public void 막으면_2초_딱_맞춰_막으면_3초_튕겨난다()
        {
            var g = Boss();
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Left);
            Assert.AreEqual(2f, g.Timer, 0.03f, "미리 들고 있었으면 2초");

            // 다음은 오른쪽 베기: 닿기 0.2초 전에 막는 자세를 든다.
            RunUntil(g, x => x.Step == GruntStep.Strike && x.Timer <= 0.2f);
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Right);
            Assert.AreEqual(3f, g.Timer, 0.03f, "딱 맞춰 막으면 3초");
        }

        [Test]
        public void 못_막으면_15를_맞고_1초2_뒤에_다시_공격한다()
        {
            var g = Boss();
            float damage = 0f;
            RunUntil(g, x =>
            {
                foreach (var e in x.Events) if (e.Kind == GruntEventKind.RobotHit) damage += e.Amount;
                return x.Step == GruntStep.Recover;
            });
            Assert.AreEqual(15f, damage);
            Assert.AreEqual(1.2f, RunUntil(g, x => x.Step == GruntStep.Rise), 0.05f, "회복 0.7 + 자세 0.5");
        }

        [Test]
        public void 강한_탄은_예고를_끊고_2초_튕겨나게_한다()
        {
            var g = Boss();
            RunUntil(g, x => x.Step == GruntStep.Hold);
            Assert.IsTrue(g.StrongHit(90f, 2f));
            Assert.AreEqual(GruntStep.Stagger, g.Step);
            Assert.AreEqual(2f, g.Timer, 0.001f);
            Assert.AreEqual(1110f, g.Hp, "강한 탄의 피해 90은 언제나 들어간다");
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f), "끊긴 틈에 주먹이 들어간다");

            bool hit = false;
            RunUntil(g, x => { hit |= x.Events.Any(e => e.Kind == GruntEventKind.RobotHit); return x.Step == GruntStep.Idle; });
            Assert.IsFalse(hit, "끊긴 공격은 들어오지 않는다");
        }

        [Test]
        public void 내구도가_낮으면_예고가_1점3배로_길어진다()
        {
            var spec = BossSpec();
            spec.firstTimeScale = 1f;
            var g = Boss(spec);
            g.TelegraphScale = 1.3f;
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(1.2f * 1.3f, RunUntil(g, x => x.Step == GruntStep.Recover), 0.05f);
        }

        [Test]
        public void 돌격형은_예전_그대로다()
        {
            var g = new GruntDuel(Robot + Vector3.forward * 22f);
            g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(PunchResult.Hit, g.Punch(20f), "돌격형은 기본 자세에서 절반이 들어간다");
            Assert.AreEqual(90f, g.Hp);
            g.CannonHit(10f);
            Assert.AreEqual(89f, g.Hp, 0.001f, "포는 10분의 1");
        }

        [Test]
        public void 두_사람이_1막을_끝까지_한다()
        {
            var s = Act1();
            var seen = Play(s, 240f, until: x => x.State.phase != SkirmishPhase.Fighting);

            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.LessOrEqual(s.State.gruntHp, 800f, "체력 800에서 1막이 끝난다");
            Assert.Greater(s.State.gruntHp, 700f);
            Assert.AreNotEqual(GruntStep.Dead, s.State.gruntStep, "보스는 쓰러지지 않고 남아 있다");
            Assert.AreEqual(100f, s.State.hull, "전부 막았으면 피해가 없다");
            CollectionAssert.Contains(seen, SkirmishEventKind.GruntBlocked);
            CollectionAssert.Contains(seen, SkirmishEventKind.PunchHit);
            CollectionAssert.Contains(seen, SkirmishEventKind.ShellFired, "자폭 드론이 온다");
            Debug.Log($"[BossActTests] 자동 조작 둘이서 1막: {s.State.elapsed:0.0}초");
        }

        [Test]
        public void 막기만_하고_치지_않으면_체력이_깎이지_않는다()
        {
            var s = Act1();
            for (int i = 0; i < Mathf.RoundToInt(40f / Dt); i++)
            {
                var input = SkirmishBots.Mech(s.State, s.Settings);
                input.punchDamage = 0f;
                s.SetMech(input);
                s.SetGunner(default);
                s.Tick(Dt);
            }
            Assert.AreEqual(1200f, s.State.gruntHp);
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
        }

        [Test]
        public void 틈_밖에서는_P2의_포가_보스를_깎지_못한다()
        {
            var s = Act1();
            // P1은 아무것도 하지 않아 틈이 생기지 않는다. 재기동 전까지만 본다.
            for (int i = 0; i < Mathf.RoundToInt(8f / Dt); i++)
            {
                s.SetMech(default);
                var aim = (SkirmishSession.GruntCenter(s.State, s.Settings) - s.Settings.gunnerPost).normalized;
                s.SetGunner(new GunnerInput { fire = true, aim = aim, pointer = aim });
                s.Tick(Dt);
            }
            Assert.AreEqual(1200f, s.State.gruntHp);
        }

        [Test]
        public void 강한_탄_세_발을_보스에게_잠가_쏠_수_있다()
        {
            var s = Act1();
            Assert.AreEqual(3, s.State.missiles);
            Play(s, 1f, mech: false, gunner: false);
            var seen = new List<SkirmishEventKind>();
            bool held = true;
            for (int i = 0; i < Mathf.RoundToInt(6f / Dt) && s.State.missiles == 3 || s.State.shells.Any(x => x.kind == ShellKind.Missile); i++)
            {
                var aim = (SkirmishSession.GruntCenter(s.State, s.Settings) - s.Settings.gunnerPost).normalized;
                if (s.State.lockProgress >= 1f) held = false;
                s.SetMech(default);
                s.SetGunner(new GunnerInput { aim = aim, pointer = aim, lockHeld = held });
                s.Tick(Dt);
                seen.AddRange(s.Events.Select(e => e.Kind));
                if (i > 2000) break;
            }
            Assert.AreEqual(2, s.State.missiles);
            CollectionAssert.Contains(seen, SkirmishEventKind.MissileHit);
            CollectionAssert.Contains(seen, SkirmishEventKind.EnemyStaggered);
            Assert.AreEqual(GruntStep.Stagger, s.State.gruntStep);
            Assert.AreEqual(1110f, s.State.gruntHp, 0.001f);
        }

        [Test]
        public void 자폭_드론은_12초마다_오고_막지_않으면_25를_맞는다()
        {
            var settings = SkirmishSettings.BossAct1();
            // 보스의 공격은 빼고 드론만 본다.
            settings.grunt.idleSeconds = 1000f;
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            var seen = Play(s, 30f, mech: false, gunner: false);
            Assert.AreEqual(2, seen.Count(k => k == SkirmishEventKind.ShellFired), "12초와 24초에 하나씩");
            Assert.AreEqual(1, seen.Count(k => k == SkirmishEventKind.MechHit), "첫 드론이 닿았다(둘째는 아직 날아오는 중)");
            Assert.AreEqual(75f, s.State.hull);
        }

        [Test]
        public void P2가_자폭_드론을_두_발로_떨어뜨린다()
        {
            var settings = SkirmishSettings.BossAct1();
            settings.grunt.idleSeconds = 1000f;
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            var seen = Play(s, 30f, mech: false, gunner: true);
            CollectionAssert.Contains(seen, SkirmishEventKind.ShellIntercepted);
            Assert.AreEqual(100f, s.State.hull);
        }

        [Test]
        public void 혼자_할_때는_체력_900에서_시작해_600에서_끝난다()
        {
            var s = Act1(solo: true);
            Play(s, 1f);
            Assert.AreEqual(900f, s.State.gruntHp);
            Play(s, 240f, until: x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.LessOrEqual(s.State.gruntHp, 600f);
            Assert.Greater(s.State.gruntHp, 500f);
        }

        [Test]
        public void 아무것도_안_하면_재기동_한_번_뒤에_실패한다()
        {
            var s = Act1();
            var seen = Play(s, 300f, mech: false, gunner: false, until: x => x.State.phase == SkirmishPhase.Failed);
            Assert.AreEqual(SkirmishPhase.Failed, s.State.phase);
            Assert.AreEqual(1, seen.Count(k => k == SkirmishEventKind.Rebooting));
            Assert.AreEqual(1200f, s.State.gruntHp);
        }

        [Test]
        public void 다시_시작하면_1막도_처음으로_돌아간다()
        {
            var s = Act1();
            Play(s, 240f, until: x => x.State.phase != SkirmishPhase.Fighting);
            s.Reset();
            s.SetReady(true, true);
            Play(s, 1f, mech: false, gunner: false);
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
            Assert.AreEqual(1200f, s.State.gruntHp);
            Assert.AreEqual(0, s.State.gruntsDown);
        }
    }
}
