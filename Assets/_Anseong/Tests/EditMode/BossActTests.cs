using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>보스전 1막(기획안 2026-11-26 4절·5절): 대검 왼쪽 베기와 오른쪽 베기(방패 없음), 튕겨난 동안에만 피해, 스캔과 약점 사격, 자폭 드론. 체력 800에서 2막, 400에서 3막(검과 방패를 잃음), 60에서 마무리(BossFinaleTests).</summary>
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
        public void 두_사람이_세_막을_끝까지_한다()
        {
            var s = Act1();
            var seen = Play(s, 900f, until: x => x.State.phase != SkirmishPhase.Fighting);

            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.AreEqual(0f, s.State.gruntHp, "체력 60에서 마무리로 넘어가고, 마지막 일격에 쓰러진다");
            Assert.AreEqual(FinaleStep.Blow, s.State.finale);
            Assert.AreEqual(2, s.State.act, "3막까지 갔다");
            Assert.AreEqual(GruntStep.Dead, s.State.gruntStep, "마무리의 일격에 쓰러진다");
            Assert.AreEqual(2, seen.Count(k => k == SkirmishEventKind.ActChanged));
            Assert.AreEqual(1, seen.Count(k => k == SkirmishEventKind.SwordBroken));
            CollectionAssert.Contains(seen, SkirmishEventKind.GruntBlocked);
            CollectionAssert.Contains(seen, SkirmishEventKind.PunchHit);
            Debug.Log($"[BossActTests] 자동 조작 둘이서 세 막: {s.State.elapsed:0.0}초, 남은 내구도 {s.State.hull:0}");
        }

        // ── 막 전환 (기획안 2026-11-26 4절, 넘어가는 순간은 2026-11-27 선우 확인) ──

        static void PlayUntil(SkirmishSession s, System.Func<SkirmishState, bool> until, string what, bool mech = true)
        {
            Play(s, 900f, mech: mech, until: x => until(x.State));
            Assert.IsTrue(until(s.State), "도달하지 못함: " + what);
        }

        [Test]
        public void 한_방에_선을_넘겨도_체력은_그_선에서_멈춘다()
        {
            var g = Boss();
            g.HpFloor = 800f;
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true);
            g.Punch(1000f);
            Assert.AreEqual(800f, g.Hp);
            Assert.IsTrue(g.Alive);
        }

        [Test]
        public void 체력_800에서_2막이_되고_내구도가_100으로_돌아온다()
        {
            var s = Act1();
            // 처음 몇 초는 막지 않아 내구도를 깎아 둔다.
            Play(s, 6f, mech: false, gunner: false);
            Assert.Less(s.State.hull, 100f);
            PlayUntil(s, st => st.act == 1, "2막");

            Assert.AreEqual(800f, s.State.gruntHp, "넘치는 피해는 버린다");
            Assert.AreEqual(100f, s.State.hull);
            Assert.AreEqual(GruntStep.Regroup, s.State.gruntStep);
            Assert.AreEqual(2f, s.State.gruntTimer, 0.05f, "2초 물러선다");
            Assert.IsFalse(s.State.shells.Any(x => x.kind == ShellKind.BigShell), "날아오던 드론은 치운다");
            Assert.IsFalse(s.State.swordBroken);
        }

        [Test]
        public void 물러서는_동안에는_서로_피해가_없고_끝나면_다시_싸운다()
        {
            var s = Act1();
            PlayUntil(s, st => st.act == 1, "2막");
            var seen = new List<SkirmishEventKind>();
            float far = 0f;
            int seq = 1000;
            for (int i = 0; i < Mathf.RoundToInt(1.9f / Dt); i++)
            {
                // 막지 않고 계속 벤다.
                s.SetMech(new MechInput { punchSeq = ++seq, punchDamage = 25f });
                var aim = (SkirmishSession.GruntCenter(s.State, s.Settings) - s.Settings.gunnerPost).normalized;
                s.SetGunner(new GunnerInput { fire = true, aim = aim, pointer = aim });
                s.Tick(Dt);
                seen.AddRange(s.Events.Select(e => e.Kind));
                far = Mathf.Max(far, Vector3.Distance(s.State.mech, s.State.gruntPosition));
            }
            Assert.AreEqual(GruntStep.Regroup, s.State.gruntStep);
            Assert.AreEqual(800f, s.State.gruntHp);
            Assert.AreEqual(100f, s.State.hull);
            CollectionAssert.DoesNotContain(seen, SkirmishEventKind.PunchHit);
            CollectionAssert.DoesNotContain(seen, SkirmishEventKind.ShellFired);
            Assert.AreEqual(28f, far, 0.1f, "22에서 6 물러선다");

            PlayUntil(s, st => st.gruntStep == GruntStep.Rise, "다시 공격");
            Assert.AreEqual(1, s.State.act);
        }

        [Test]
        public void 막마다_자폭_드론이_잦아진다()
        {
            var settings = SkirmishSettings.BossAct1();
            // 말이 필요한 예고 중에는 드론이 뜨지 않으니 기본 공격만 남기고 본다.
            settings.grunts[0].acts[0].attacks = new[] { GruntAttack.Slam, GruntAttack.Swing };
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            PlayUntil(s, st => st.act == 1, "2막");
            // 막기만 하고 치지 않아 2막에 머문다.
            float waited = 0f;
            bool fired = false;
            while (waited < 60f && !fired)
            {
                var input = SkirmishBots.Mech(s.State, s.Settings);
                input.punchDamage = 0f;
                s.SetMech(input);
                s.SetGunner(default);
                s.Tick(Dt);
                waited += Dt;
                fired = s.Events.Any(e => e.Kind == SkirmishEventKind.ShellFired);
            }
            Assert.IsTrue(fired);
            Assert.AreEqual(1, s.State.act);
            Assert.AreEqual(10f, waited, 0.5f, "2막은 10초마다");
        }

        [Test]
        public void 체력_400에서_3막이_되고_일격에_검과_방패를_잃는다()
        {
            var s = Act1();
            PlayUntil(s, st => st.act == 2, "3막");
            Assert.AreEqual(400f, s.State.gruntHp);
            Assert.AreEqual(100f, s.State.hull);
            Assert.AreEqual(GruntStep.Rise, s.State.gruntStep, "물러서지 않고 바로 크게 든다");
            Assert.AreEqual(GruntAttack.Slam, s.State.gruntAttack);
            Assert.IsFalse(s.State.swordBroken, "일격이 닿기 전에는 아직 들고 있다");

            // 막지 않아도 피해가 없다.
            var seen = Play(s, 5f, mech: false, gunner: false, until: x => x.State.swordBroken);
            Assert.IsTrue(s.State.swordBroken);
            Assert.IsTrue(s.State.shieldBroken);
            Assert.IsFalse(s.State.shieldOpen);
            Assert.AreEqual(100f, s.State.hull);
            Assert.AreEqual(400f, s.State.gruntHp);
            Assert.AreEqual(s.Settings.breakSlowSeconds, s.State.slowMotion, 1e-4f, "부서지는 순간 느려진다");
            CollectionAssert.Contains(seen, SkirmishEventKind.ShieldBroken);
            CollectionAssert.Contains(seen, SkirmishEventKind.SwordBroken);
            CollectionAssert.DoesNotContain(seen, SkirmishEventKind.MechHit);
        }

        [Test]
        public void 검을_잃은_뒤에는_주먹_피해가_들어간다()
        {
            var s = Act1();
            PlayUntil(s, st => st.swordBroken, "검을 잃음");
            int seq = 5000;
            for (int i = 0; i < 3000 && s.State.gruntHp >= 400f; i++)
            {
                var input = SkirmishBots.Mech(s.State, s.Settings);
                if (s.State.gruntStep == GruntStep.Stagger) seq = 5001;
                input.punchSeq = seq; input.punchDamage = 35f;
                s.SetMech(input);
                s.SetGunner(default);
                s.Tick(Dt);
            }
            Assert.AreEqual(365f, s.State.gruntHp, "검 40이 아니라 훅 35");
        }

        [Test]
        public void 강한_탄은_물러서는_보스와_부수는_일격을_끊지_못한다()
        {
            var g = Boss();
            g.Regroup(2f);
            Assert.IsFalse(g.StrongHit(90f, 4f));
            Assert.AreEqual(GruntStep.Regroup, g.Step);
            g.BeginBreaker();
            Assert.IsFalse(g.StrongHit(90f, 4f));
            Assert.IsTrue(g.Breaker);
            Assert.AreEqual(1200f, g.Hp);
        }

        [Test]
        public void 삼막에서_재기동하면_3막의_처음부터_맨손으로_다시_한다()
        {
            var s = Act1();
            PlayUntil(s, st => st.swordBroken, "검을 잃음");
            PlayUntil(s, st => st.gruntHp < 400f, "3막에서 조금 깎음");
            // 아무것도 하지 않아 내구도가 0이 된다.
            var seen = Play(s, 300f, mech: false, gunner: false, until: x => x.Events.Any(e => e.Kind == SkirmishEventKind.SceneRestarted));
            CollectionAssert.Contains(seen, SkirmishEventKind.Rebooting);
            Play(s, 1f, mech: false, gunner: false);

            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
            Assert.AreEqual(2, s.State.act);
            Assert.AreEqual(400f, s.State.gruntHp);
            Assert.AreEqual(100f, s.State.hull);
            Assert.IsTrue(s.State.swordBroken && s.State.shieldBroken, "다시 해도 맨손이다");
            Assert.AreEqual(0, s.State.rebootsLeft);
        }

        [Test]
        public void 혼자_할_때는_막의_선도_같은_비율로_낮다()
        {
            var s = Act1(solo: true);
            PlayUntil(s, st => st.act == 1, "2막");
            Assert.AreEqual(600f, s.State.gruntHp);
            PlayUntil(s, st => st.act == 2, "3막");
            Assert.AreEqual(300f, s.State.gruntHp);
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
        public void P1의_검은_틈에_한_번_40이_들어간다()
        {
            var s = Act1();
            Assert.AreEqual(40f, s.Settings.swordDamage);
            // 막기만 하다가 튕겨난 틈에 한 번 벤다(입력에 실린 주먹 피해 25는 쓰이지 않는다).
            int seq = 0;
            for (int i = 0; i < 2000 && !(s.State.gruntHp > 0f && s.State.gruntHp < 1200f); i++)
            {
                var input = SkirmishBots.Mech(s.State, s.Settings);
                if (s.State.gruntStep == GruntStep.Stagger) seq = 1;
                input.punchSeq = seq; input.punchDamage = 25f;
                s.SetMech(input);
                s.SetGunner(default);
                s.Tick(Dt);
            }
            Assert.AreEqual(1160f, s.State.gruntHp);
        }

        [Test]
        public void 맵_위_자리는_기획안_2절의_좌표와_같다()
        {
            var s = SkirmishSettings.BossAct1OnMap();
            Assert.AreEqual(90f, s.mechYaw, "동쪽을 본다");
            Assert.Less(Vector3.Distance(new Vector3(100f, 0f, 632f), s.mechStart), 0.01f);
            Assert.Less(Vector3.Distance(new Vector3(122f, 0f, 632f), s.grunts[0].start), 0.01f, "보스는 로봇 앞 22");
            Assert.Less(Vector3.Distance(new Vector3(111f, 40f, 595f), s.gunnerPost), 0.01f, "P2 헬기 자리");
            Assert.AreEqual(15f, s.gunnerPitch, "기수를 15도 숙인다");
            Assert.AreEqual(0f, s.gunnerYaw, 0.01f, "P2는 북쪽을 본다");
            Assert.Greater(s.droneFrom.x, 200f, "자폭 드론은 동쪽에서 온다");
        }

        [Test]
        public void 맵_위_자리에서도_끝까지_간다()
        {
            var s = new SkirmishSession(SkirmishSettings.BossAct1OnMap());
            s.SetReady(true, true);
            var seen = Play(s, 900f, until: x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            CollectionAssert.Contains(seen, SkirmishEventKind.GruntBlocked);
            Assert.Less(Vector3.Distance(new Vector3(122f, 0f, 632f), s.State.gruntPosition), 16f, "보스는 싸우던 자리에서 뒤로 밀려나 쓰러진다");
        }

        // ── 특별한 공격 다섯 (기획안 4절의 표) ──

        /// <summary>그 공격만 되풀이하는 보스. 처음 나올 때 예고가 길어지는 것은 끈다.</summary>
        static GruntDuel Only(params GruntAttack[] attacks)
        {
            var spec = BossSpec();
            spec.attacks = attacks;
            spec.firstTimeScale = 1f;
            return Boss(spec);
        }

        static bool Has(GruntDuel g, GruntEventKind kind) => g.Events.Any(e => e.Kind == kind);

        [Test]
        public void 속임_베기는_2초2_뒤에_오고_숨겨진_쪽으로_막아야_튕겨난다()
        {
            var g = Only(GruntAttack.Feint);
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(2.2f, g.ImpactIn, 0.03f);
            Assert.AreEqual(PilotSide.Right, g.Side, "첫 속임 베기는 오른쪽");
            // 틀린 쪽으로 막으면 맞는다.
            RunUntil(g, x => x.Step == GruntStep.Recover, guarding: true, side: PilotSide.Left);
            Assert.IsTrue(Has(g, GruntEventKind.RobotHit));
            Assert.AreEqual(15f, g.Events.First(e => e.Kind == GruntEventKind.RobotHit).Amount);
            // 둘째는 왼쪽. 맞는 쪽으로 막으면 튕겨난다.
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(PilotSide.Left, g.Side);
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Left);
            Assert.AreEqual(2f, g.Timer, 0.05f);
        }

        [Test]
        public void 큰_내려베기는_딱_맞춰_막아야만_4초_틈이_난다()
        {
            var g = Only(GruntAttack.BigSlam);
            // 첫째(3.0초): 처음부터 막고 있으면 피해도 틈도 없다.
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(3.0f, g.ImpactIn, 0.03f);
            RunUntil(g, x => x.Step == GruntStep.Recover, guarding: true);
            Assert.IsTrue(Has(g, GruntEventKind.Blocked));
            Assert.IsFalse(Has(g, GruntEventKind.RobotHit));
            // 둘째(2.4초): 못 막으면 40.
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(2.4f, g.ImpactIn, 0.03f);
            RunUntil(g, x => x.Step == GruntStep.Recover);
            Assert.AreEqual(40f, g.Events.First(e => e.Kind == GruntEventKind.RobotHit).Amount);
            // 셋째(3.6초): 닿기 0.2초 전에 막으면 4초 튕겨난다.
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(3.6f, g.ImpactIn, 0.03f);
            RunUntil(g, x => x.ImpactIn <= 0.2f);
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Right);
            Assert.IsTrue(Has(g, GruntEventKind.PerfectBlocked));
            Assert.AreEqual(4f, g.Timer, 0.05f);
        }

        [Test]
        public void 돌진은_막아도_30을_맞고_옆으로_비켜_있으면_지나쳐서_3초_멈춘다()
        {
            var g = Only(GruntAttack.Thrust);
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(1.6f, g.ImpactIn, 0.03f);
            RunUntil(g, x => x.Step == GruntStep.Recover, guarding: true);
            Assert.AreEqual(30f, g.Events.First(e => e.Kind == GruntEventKind.RobotHit).Amount);

            g.Dodge = 8f;
            RunUntil(g, x => x.Step == GruntStep.Stagger);
            Assert.IsTrue(Has(g, GruntEventKind.ThrustPassed));
            Assert.IsTrue(g.Passed);
            Assert.AreEqual(3f, g.Timer, 0.05f);
            Assert.AreEqual(8f, Vector3.Distance(Robot, g.Position), 0.1f, "22에서 14 나아가 로봇 자리 옆에 선다");
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f), "등을 보인 동안은 틈이다");
            // 끝나면 제자리로 돌아온다.
            g.Dodge = 0f;
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(22f, Vector3.Distance(Robot, g.Position), 0.5f);
        }

        [Test]
        public void 헬기를_노림은_오른쪽으로_막아_주면_튕겨나고_한_번만_나온다()
        {
            var g = Only(GruntAttack.AimHeli, GruntAttack.Slam);
            RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(GruntAttack.AimHeli, g.Attack);
            Assert.AreEqual(2.5f, g.ImpactIn, 0.03f);
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Right);
            // 그 뒤로는 내려베기만 나온다.
            for (int i = 0; i < 3; i++)
            {
                RunUntil(g, x => x.Step == GruntStep.Rise);
                Assert.AreEqual(GruntAttack.Slam, g.Attack);
                RunUntil(g, x => x.Step == GruntStep.Recover);
            }

            var h = Only(GruntAttack.AimHeli, GruntAttack.Slam);
            RunUntil(h, x => x.Step == GruntStep.Recover, guarding: true, side: PilotSide.Left);
            Assert.IsTrue(Has(h, GruntEventKind.HeliShot), "왼쪽으로 막으면 헬기로 나간다");
            Assert.IsFalse(Has(h, GruntEventKind.RobotHit));
        }

        static SkirmishSession Fight(GruntAttack[] attacks, bool solo = false)
        {
            var settings = SkirmishSettings.BossAct1();
            settings.grunts[0].spec.attacks = attacks;
            settings.grunts[0].spec.firstTimeScale = 1f;
            settings.droneEvery = 0f;
            var s = new SkirmishSession(settings) { Solo = solo };
            s.SetReady(true, true);
            return s;
        }

        [Test]
        public void 헬기를_노린_일격은_숨지_않으면_포를_5초_멈춘다()
        {
            var s = Fight(new[] { GruntAttack.AimHeli, GruntAttack.Slam });
            Play(s, 10f, mech: false, gunner: false, until: x => x.Events.Any(e => e.Kind == SkirmishEventKind.HeliShot));
            Assert.AreEqual(5f, s.State.cannonJam, 0.05f);
            Assert.AreEqual(100f, s.State.hull, "로봇은 피해가 없다");
            // 멈춘 동안에는 포가 나가지 않는다.
            var shots = Gun(s, 1f, x => new GunnerInput { fire = true, aim = AtBoss(x), pointer = AtBoss(x) });
            CollectionAssert.DoesNotContain(shots, SkirmishEventKind.CannonShot);

            var hidden = Fight(new[] { GruntAttack.AimHeli, GruntAttack.Slam });
            Gun(hidden, 10f, x => new GunnerInput { hide = true }, x => x.Events.Any(e => e.Kind == SkirmishEventKind.HeliShot));
            Assert.IsTrue(hidden.Events.Any(e => e.Kind == SkirmishEventKind.HeliShot));
            Assert.AreEqual(0f, hidden.State.cannonJam, "숨으면 피한다");
        }

        [Test]
        public void 자폭_떼는_3초_뒤에_넷이_한꺼번에_오고_혼자_할_때는_둘이다()
        {
            var s = Fight(new[] { GruntAttack.CallSwarm, GruntAttack.Slam });
            var seen = Play(s, 10f, mech: false, gunner: false, until: x => x.Events.Any(e => e.Kind == SkirmishEventKind.SwarmCalled));
            Assert.AreEqual(4, seen.Count(k => k == SkirmishEventKind.ShellFired));
            Assert.AreEqual(4, s.State.shells.Count(x => x.kind == ShellKind.BigShell));
            Assert.AreEqual(3.5f, s.State.elapsed, 0.2f, "자세 0.5초 뒤 예고 3초");

            var solo = Fight(new[] { GruntAttack.CallSwarm, GruntAttack.Slam }, solo: true);
            var alone = Play(solo, 10f, mech: false, gunner: false, until: x => x.Events.Any(e => e.Kind == SkirmishEventKind.SwarmCalled));
            Assert.AreEqual(2, alone.Count(k => k == SkirmishEventKind.ShellFired));
        }

        [Test]
        public void 스틱을_옆으로_튕기면_0초4에_8_비켰다가_1초5_뒤에_돌아온다()
        {
            var s = Fight(new[] { GruntAttack.Slam });
            Play(s, 1f, mech: false, gunner: false);
            var start = s.State.mech;
            var boss = s.State.gruntPosition;
            var right = Quaternion.Euler(0f, s.Settings.mechYaw, 0f) * Vector3.right;
            float t = 0f;
            float At(float until)
            {
                // 스틱은 계속 넘어가 있어도 한 걸음만 나온다.
                for (; t < until - 1e-4f; t += Dt) { s.SetMech(new MechInput { move = right }); s.SetGunner(default); s.Tick(Dt); }
                return Vector3.Dot(s.State.mech - start, right);
            }
            Assert.AreEqual(4f, At(0.2f), 0.3f, "절반쯤");
            Assert.AreEqual(8f, At(0.4f), 0.05f);
            Assert.AreEqual(8f, At(1.9f), 0.05f);
            Assert.AreEqual(0f, At(2.3f), 0.05f);
            Assert.AreEqual(0f, At(4f), 0.05f, "넘어간 채로는 다시 비키지 않는다");
            Assert.Less(Vector3.Distance(boss, s.State.gruntPosition), 0.01f, "보스는 따라오지 않는다");
        }

        [Test]
        public void 말이_필요한_예고_중에는_자폭_드론이_뜨지_않는다()
        {
            var settings = SkirmishSettings.BossAct1();
            settings.grunts[0].spec.attacks = new[] { GruntAttack.Feint };
            settings.droneEvery = 1f;
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            int fired = 0;
            for (int i = 0; i < Mathf.RoundToInt(20f / Dt); i++)
            {
                s.SetMech(default); s.SetGunner(default);
                bool calling = s.Grunt != null && s.Grunt.NeedsCall;
                s.Tick(Dt);
                bool shot = s.Events.Any(e => e.Kind == SkirmishEventKind.ShellFired);
                if (shot) fired++;
                if (calling && s.Grunt.NeedsCall) Assert.IsFalse(shot, "속임 베기 예고 중에 드론이 떴다");
            }
            Assert.Greater(fired, 0, "예고 사이에는 뜬다");
        }

        // ── 스캔과 약점 사격 (기획안 2026-11-27) ──

        static Vector3 AtBoss(SkirmishSession s) => (SkirmishSession.GruntCenter(s.State, s.Settings) - s.Settings.gunnerPost).normalized;
        static Vector3 AtWeakPoint(SkirmishSession s) => (s.State.weakPoint - s.Settings.gunnerPost).normalized;

        /// <summary>P2 입력을 정해 주며 돌린다. P1은 아무것도 하지 않는다.</summary>
        static List<SkirmishEventKind> Gun(SkirmishSession s, float seconds, System.Func<SkirmishSession, GunnerInput> input, System.Func<SkirmishSession, bool> until = null)
        {
            var seen = new List<SkirmishEventKind>();
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                s.SetMech(default);
                s.SetGunner(input(s));
                s.Tick(Dt);
                seen.AddRange(s.Events.Select(e => e.Kind));
                if (until != null && until(s)) break;
            }
            return seen;
        }

        /// <summary>보스가 선 뒤 버튼을 누른 채 보스를 겨눠 약점이 뜬 상태.</summary>
        static SkirmishSession Scanned()
        {
            var s = Act1();
            Gun(s, 0.1f, _ => default);
            Gun(s, 3f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true }, x => x.State.weakTimer > 0f);
            Assert.Greater(s.State.weakTimer, 0f, "스캔이 끝나면 약점이 뜬다");
            return s;
        }

        [Test]
        public void 버튼을_누른_채_2초_겨누면_약점이_4초_뜬다()
        {
            var s = Act1();
            Gun(s, 0.1f, _ => default);
            var seen = Gun(s, 1.9f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true });
            Assert.AreEqual(0f, s.State.weakTimer, "2초가 되기 전에는 안 뜬다");
            Assert.AreEqual(SkirmishSession.GruntTarget, s.State.lockTarget);
            Assert.Greater(s.State.lockProgress, 0.9f);
            seen = Gun(s, 0.2f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true });
            CollectionAssert.Contains(seen, SkirmishEventKind.WeakPointShown);
            Assert.AreEqual(4f, s.State.weakTimer, 0.15f);

            // 누른 채 4초가 지나면 꺼지고, 놓았다가 다시 눌러야 스캔이 된다.
            Gun(s, 4.2f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true });
            Assert.AreEqual(0f, s.State.weakTimer);
            Assert.AreEqual(0f, s.State.lockProgress);
            Assert.AreEqual(3, s.State.missiles, "쏘지 않았으면 줄지 않는다");
        }

        [Test]
        public void 조준이_벗어나면_스캔은_처음부터다()
        {
            var s = Act1();
            Gun(s, 0.1f, _ => default);
            Gun(s, 1.5f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true });
            Assert.Greater(s.State.lockProgress, 0.7f);
            Gun(s, 0.1f, x => new GunnerInput { aim = Vector3.up, lockHeld = true });
            Assert.AreEqual(0f, s.State.lockProgress);
            Gun(s, 1.5f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true });
            Assert.AreEqual(0f, s.State.weakTimer, "다시 2초를 채워야 한다");
        }

        [Test]
        public void 스캔하는_동안에는_포가_나가지_않는다()
        {
            var s = Act1();
            Gun(s, 0.1f, _ => default);
            var seen = Gun(s, 1f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true, fire = true });
            CollectionAssert.DoesNotContain(seen, SkirmishEventKind.CannonShot);
            seen = Gun(s, 0.5f, x => new GunnerInput { aim = AtBoss(x), fire = true });
            CollectionAssert.Contains(seen, SkirmishEventKind.CannonShot);
        }

        [Test]
        public void 약점을_겨누고_놓으면_강한_탄이_맞아_예고를_끊고_4초_튕겨난다()
        {
            var s = Scanned();
            float before = s.State.gruntHp;
            // 약점으로 조준을 옮긴 뒤 놓는다.
            Gun(s, 0.2f, x => new GunnerInput { aim = AtWeakPoint(x), lockHeld = true });
            var seen = Gun(s, 3f, x => new GunnerInput { aim = AtWeakPoint(x) }, x => x.State.gruntStep == GruntStep.Stagger);

            Assert.AreEqual(2, s.State.missiles);
            CollectionAssert.Contains(seen, SkirmishEventKind.MissileFired);
            CollectionAssert.Contains(seen, SkirmishEventKind.MissileHit);
            CollectionAssert.Contains(seen, SkirmishEventKind.EnemyStaggered);
            Assert.AreEqual(GruntStep.Stagger, s.State.gruntStep);
            Assert.AreEqual(4f, s.State.gruntTimer, 0.05f);
            Assert.AreEqual(before - 90f, s.State.gruntHp, 0.001f);
            Assert.AreEqual(0f, s.State.weakTimer, "쏘면 표시는 꺼진다");
        }

        [Test]
        public void 약점을_벗어나_놓으면_한_발만_줄어든다()
        {
            var s = Scanned();
            float before = s.State.gruntHp;
            var away = Quaternion.Euler(0f, 25f, 0f) * AtWeakPoint(s);
            Gun(s, 0.1f, x => new GunnerInput { aim = away, lockHeld = true });
            var seen = Gun(s, 4f, x => new GunnerInput { aim = away });

            Assert.AreEqual(2, s.State.missiles);
            CollectionAssert.Contains(seen, SkirmishEventKind.MissileFired);
            CollectionAssert.Contains(seen, SkirmishEventKind.MissileLost);
            CollectionAssert.DoesNotContain(seen, SkirmishEventKind.MissileHit);
            Assert.AreEqual(before, s.State.gruntHp);
        }

        [Test]
        public void 약점은_등_어깨_옆구리를_차례로_돌고_셋_다_허리_위다()
        {
            var s = Act1();
            Gun(s, 0.1f, _ => default);
            var heights = new List<float>();
            for (int i = 0; i < 3; i++)
            {
                Gun(s, 3f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true }, x => x.State.weakTimer > 0f);
                heights.Add(s.State.weakPoint.y);
                Assert.Less(Vector3.Distance(s.State.weakPoint, SkirmishSession.GruntCenter(s.State, s.Settings)), 20f, "약점은 보스 몸에 있다");
                // 취소하고 놓으면 쏘지 않고 다음 스캔으로.
                Gun(s, 0.1f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true, cancel = true });
                Gun(s, 0.1f, _ => default);
            }
            Assert.AreEqual(3, s.State.missiles, "취소하면 쓰지 않는다");
            Assert.Greater(heights[0], 20f, "등");
            Assert.Greater(heights[1], 20f, "어깨");
            Assert.Greater(heights[2], 17.5f, "옆구리도 허리(키의 절반) 위");
            Assert.Less(heights[2], heights[1], "옆구리는 어깨보다 낮다");
        }

        [Test]
        public void 혼자_할_때는_스캔이_없다()
        {
            var s = Act1(solo: true);
            Gun(s, 0.1f, _ => default);
            Gun(s, 3f, x => new GunnerInput { aim = AtBoss(x), lockHeld = true });
            Assert.AreEqual(0f, s.State.weakTimer);
            Assert.AreEqual(0f, s.State.lockProgress);
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
        public void 혼자_할_때는_체력_900에서_시작해_45에서_마무리로_넘어간다()
        {
            var s = Act1(solo: true);
            Play(s, 1f);
            Assert.AreEqual(900f, s.State.gruntHp);
            Play(s, 900f, until: x => x.State.finale != FinaleStep.None);
            Assert.AreEqual(45f, s.State.gruntHp);
            Play(s, 60f, until: x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.AreEqual(0f, s.State.gruntHp);
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
            Play(s, 900f, until: x => x.State.phase != SkirmishPhase.Fighting);
            s.Reset();
            s.SetReady(true, true);
            Play(s, 1f, mech: false, gunner: false);
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
            Assert.AreEqual(1200f, s.State.gruntHp);
            Assert.AreEqual(0, s.State.act);
            Assert.IsFalse(s.State.swordBroken || s.State.shieldBroken, "검과 방패도 돌아온다");
            Assert.AreEqual(0, s.State.gruntsDown);
        }
    }
}
