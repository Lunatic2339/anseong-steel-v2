using System.Collections.Generic;
using System.Linq;
using Anseong.Pilot;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>돌격형 일반병의 싸움 규칙. 제작 명세의 "돌격형 일반병" 표와 장면 1을 그대로 돌린다.</summary>
    public class GruntDuelTests
    {
        const float Dt = 0.02f;
        static readonly Vector3 Robot = new Vector3(0f, 0f, 65f);

        /// <summary>로봇 앞 22에 이미 와서 기본 자세로 선 적.</summary>
        static GruntDuel Engaged(GruntSpec spec = null, GruntSettings settings = null)
        {
            var g = new GruntDuel(Robot + Vector3.forward * 22.01f, spec, settings);
            g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(GruntStep.Idle, g.Step, "도착하면 기본 자세");
            return g;
        }

        /// <summary>조건이 될 때까지 돌리고 걸린 초를 돌려준다. 그동안 나온 사건을 seen에 모은다.</summary>
        static float RunUntil(GruntDuel g, System.Func<GruntDuel, bool> until, List<GruntEventKind> seen = null,
            bool guarding = false, PilotSide side = PilotSide.Left, Vector3? robot = null, float limit = 30f)
        {
            float t = 0f;
            while (t < limit)
            {
                g.Tick(Dt, robot ?? Robot, guarding, side);
                t += Dt;
                seen?.AddRange(g.Events.Select(e => e.Kind));
                if (until(g)) return t;
            }
            Assert.Fail("제한 시간 안에 조건이 되지 않음. 상태: " + g.Step);
            return t;
        }

        static void RunFor(GruntDuel g, float seconds, List<GruntEventKind> seen = null, bool guarding = false, PilotSide side = PilotSide.Left)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                g.Tick(Dt, Robot, guarding, side);
                seen?.AddRange(g.Events.Select(e => e.Kind));
            }
        }

        /// <summary>막기에 성공해 튕겨난 상태로 만든다(막는 자세를 미리 들고 있어 딱 맞춰 막기는 아님).</summary>
        static GruntDuel Staggered(GruntSpec spec = null)
        {
            var g = Engaged(spec);
            RunUntil(g, x => x.Step == GruntStep.Stagger, guarding: true, side: GruntDuel.IncomingSide(GruntAttack.Slam));
            return g;
        }

        [Test]
        public void 걷다가_마지막_30은_달려들어_22_앞에_선다()
        {
            // 장면 1의 돌격형 1: (0, 0, 130)에서 등장.
            var g = new GruntDuel(new Vector3(0f, 0f, 130f));
            var seen = new List<GruntEventKind>();

            float untilSprint = RunUntil(g, x => x.Events.Any(e => e.Kind == GruntEventKind.SprintStarted), seen);
            // 설 자리(87)까지 43 중 13을 8 m/s로 걷는다.
            Assert.AreEqual(13f / 8f, untilSprint, 0.05f);
            Assert.AreEqual(GruntStep.Approach, g.Step);

            float untilArrive = RunUntil(g, x => x.Step == GruntStep.Idle, seen);
            // 나머지 30을 16 m/s로 달린다.
            Assert.AreEqual(30f / 16f, untilArrive, 0.05f);
            Assert.AreEqual(22f, Vector3.Distance(g.Position, Robot), 0.01f);
            CollectionAssert.Contains(seen, GruntEventKind.Arrived);
        }

        [Test]
        public void 다가오는_동안에는_주먹이_닿지_않는다()
        {
            var g = new GruntDuel(new Vector3(0f, 0f, 130f));
            g.Tick(Dt, Robot, false, PilotSide.Left);
            Assert.AreEqual(PunchResult.OutOfReach, g.Punch(25f));
            Assert.AreEqual(100f, g.Hp);
        }

        [Test]
        public void 기본_자세_반_초_뒤에_예고하고_1_6초_뒤에_닿는다()
        {
            var g = Engaged();
            var seen = new List<GruntEventKind>();
            float idle = RunUntil(g, x => x.Step == GruntStep.Rise, seen);
            Assert.AreEqual(0.5f, idle, 0.03f);
            CollectionAssert.Contains(seen, GruntEventKind.TelegraphStarted);

            // 올림 0.4 + 멈춤 0.9 + 타격 0.3
            float toImpact = RunUntil(g, x => x.Step == GruntStep.Recover, seen);
            Assert.AreEqual(1.6f, toImpact, 0.03f);
            CollectionAssert.Contains(seen, GruntEventKind.RobotHit);
        }

        [Test]
        public void 장면_1의_첫_적은_멈춤이_1_2초다()
        {
            var g = Engaged(new GruntSpec { holdSeconds = 1.2f });
            RunUntil(g, x => x.Step == GruntStep.Rise);
            float toImpact = RunUntil(g, x => x.Step == GruntStep.Recover);
            Assert.AreEqual(0.4f + 1.2f + 0.3f, toImpact, 0.03f);
        }

        [Test]
        public void 내려치기는_왼쪽으로_막아야_한다()
        {
            Assert.AreEqual(PilotSide.Left, GruntDuel.IncomingSide(GruntAttack.Slam));

            var right = Engaged();
            var seen = new List<GruntEventKind>();
            RunUntil(right, x => x.Step == GruntStep.Recover || x.Step == GruntStep.Stagger, seen, guarding: true, side: PilotSide.Right);
            Assert.AreEqual(GruntStep.Recover, right.Step, "오른쪽을 막고 있으면 내려치기에 맞는다");
            CollectionAssert.Contains(seen, GruntEventKind.RobotHit);

            var left = Engaged();
            seen.Clear();
            RunUntil(left, x => x.Step == GruntStep.Recover || x.Step == GruntStep.Stagger, seen, guarding: true, side: PilotSide.Left);
            Assert.AreEqual(GruntStep.Stagger, left.Step);
            CollectionAssert.Contains(seen, GruntEventKind.Blocked);
            CollectionAssert.DoesNotContain(seen, GruntEventKind.RobotHit);
        }

        [Test]
        public void 휘두르기는_오른쪽으로_막아야_한다()
        {
            var spec = new GruntSpec { attacks = new[] { GruntAttack.Swing } };
            Assert.AreEqual(PilotSide.Right, GruntDuel.IncomingSide(GruntAttack.Swing));

            var left = Engaged(spec);
            RunUntil(left, x => x.Step == GruntStep.Recover || x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Left);
            Assert.AreEqual(GruntStep.Recover, left.Step);

            var right = Engaged(spec);
            RunUntil(right, x => x.Step == GruntStep.Recover || x.Step == GruntStep.Stagger, guarding: true, side: PilotSide.Right);
            Assert.AreEqual(GruntStep.Stagger, right.Step);
        }

        [Test]
        public void 막지_못하면_15를_맞고_회복과_기본_자세를_거쳐_다시_예고한다()
        {
            var g = Engaged();
            RunUntil(g, x => x.Step == GruntStep.Recover);
            var hit = g.Events.Single(e => e.Kind == GruntEventKind.RobotHit);
            Assert.AreEqual(15f, hit.Amount);

            // 회복 0.7 + 기본 자세 0.5
            float untilNext = RunUntil(g, x => x.Step == GruntStep.Rise);
            Assert.AreEqual(1.2f, untilNext, 0.03f);
        }

        [Test]
        public void 미리_들고_있으면_그냥_막기이고_튕겨남은_2초다()
        {
            var g = Engaged();
            var seen = new List<GruntEventKind>();
            RunUntil(g, x => x.Step == GruntStep.Stagger, seen, guarding: true);
            CollectionAssert.Contains(seen, GruntEventKind.Blocked);
            CollectionAssert.DoesNotContain(seen, GruntEventKind.PerfectBlocked);
            float stagger = RunUntil(g, x => x.Step != GruntStep.Stagger, guarding: true);
            Assert.AreEqual(2f, stagger, 0.03f);
        }

        [Test]
        public void 닿기_직전에_들면_딱_맞춰_막기이고_튕겨남이_1초_길다()
        {
            var g = Engaged();
            // 내려치는 동안(남은 시간 0.2초)까지는 막지 않는다.
            RunUntil(g, x => x.Step == GruntStep.Strike && x.Timer <= 0.2f);
            var seen = new List<GruntEventKind>();
            RunUntil(g, x => x.Step == GruntStep.Stagger, seen, guarding: true);
            CollectionAssert.Contains(seen, GruntEventKind.PerfectBlocked);
            float stagger = RunUntil(g, x => x.Step != GruntStep.Stagger, guarding: true);
            Assert.AreEqual(3f, stagger, 0.03f);
        }

        [Test]
        public void 반대쪽을_들고_있다가_직전에_옮겨도_딱_맞춰_막기다()
        {
            var g = Engaged();
            RunUntil(g, x => x.Step == GruntStep.Strike && x.Timer <= 0.2f, guarding: true, side: PilotSide.Right);
            var seen = new List<GruntEventKind>();
            RunUntil(g, x => x.Step == GruntStep.Stagger, seen, guarding: true, side: PilotSide.Left);
            CollectionAssert.Contains(seen, GruntEventKind.PerfectBlocked);
        }

        [Test]
        public void 기본_자세에서는_주먹이_절반만_들어간다()
        {
            var g = Engaged();
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(100f - 12.5f, g.Hp, 0.001f);
        }

        [Test]
        public void 튕겨난_틈에_잽_네_대면_쓰러진다()
        {
            // 돌격형 1: 가드를 굳히지 않음.
            var g = Staggered(new GruntSpec { guardEveryDamage = 0f });
            for (int i = 0; i < 3; i++) Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(25f, g.Hp, 0.001f);
            Assert.AreEqual(PunchResult.Finished, g.Punch(25f));
            Assert.IsFalse(g.Alive);
            var down = g.Events.Single(e => e.Kind == GruntEventKind.Down);
            Assert.AreEqual(15f, down.Amount, "마무리 한 방에 뒤로 15 밀려난다");
            Assert.AreEqual(PunchResult.OutOfReach, g.Punch(25f), "쓰러진 뒤에는 닿지 않는다");
        }

        [Test]
        public void 돌격형_2는_한_대_맞으면_바로_가드를_굳히고_다음_주먹이_튕긴다()
        {
            var g = Staggered(new GruntSpec { firstGuardDamage = 25f, guardEveryDamage = 60f });
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(GruntStep.Guard, g.Step, "튕겨남이 끝나기를 기다리지 않는다");
            CollectionAssert.Contains(g.Events.Select(e => e.Kind).ToList(), GruntEventKind.GuardClosed);

            Assert.AreEqual(PunchResult.Bounced, g.Punch(35f));
            Assert.AreEqual(75f, g.Hp, 0.001f, "튕긴 주먹은 피해가 없다");
        }

        [Test]
        public void 가드는_포_피해_40에_깨지고_2_5초_튕겨난다()
        {
            var g = Staggered(new GruntSpec { firstGuardDamage = 25f });
            g.Punch(25f);
            g.Tick(Dt, Robot, false, PilotSide.Left);

            // 한 발 4짜리 연사 열 발.
            for (int i = 0; i < 9; i++) g.CannonHit(4f);
            Assert.AreEqual(GruntStep.Guard, g.Step, "36으로는 안 깨진다");
            g.CannonHit(4f);
            Assert.AreEqual(GruntStep.Stagger, g.Step);
            CollectionAssert.Contains(g.Events.Select(e => e.Kind).ToList(), GruntEventKind.GuardBroken);

            float stagger = RunUntil(g, x => x.Step != GruntStep.Stagger);
            Assert.AreEqual(2.5f, stagger, 0.03f);
        }

        [Test]
        public void 가드를_못_열면_4초_뒤에_스스로_풀고_공격한다()
        {
            var g = Staggered(new GruntSpec { firstGuardDamage = 25f });
            g.Punch(25f);
            var seen = new List<GruntEventKind>();
            float held = RunUntil(g, x => x.Step != GruntStep.Guard, seen);
            Assert.AreEqual(4f, held, 0.03f);
            Assert.AreEqual(GruntStep.Rise, g.Step, "기본 자세를 거치지 않고 바로 예고");
            CollectionAssert.Contains(seen, GruntEventKind.GuardReleased);
        }

        [Test]
        public void 혼자_할_때는_가드가_3초_뒤에_풀린다()
        {
            var g = Staggered(new GruntSpec { firstGuardDamage = 25f });
            g.Solo = true;
            g.Punch(25f);
            float held = RunUntil(g, x => x.Step != GruntStep.Guard);
            Assert.AreEqual(3f, held, 0.03f);
        }

        [Test]
        public void 두_번째_가드부터는_60이_쌓여야_한다()
        {
            var g = Staggered(new GruntSpec { hp = 1000f, firstGuardDamage = 25f, guardEveryDamage = 60f });
            g.Punch(25f);
            for (int i = 0; i < 10; i++) g.CannonHit(4f);
            Assert.AreEqual(GruntStep.Stagger, g.Step);

            g.Punch(25f);
            g.Punch(25f);
            Assert.AreEqual(GruntStep.Stagger, g.Step, "50으로는 아직");
            g.Punch(25f);
            Assert.AreEqual(GruntStep.Guard, g.Step);
        }

        [Test]
        public void 다른_적을_쏴도_가드가_열리지_않고_포로는_쓰러지지_않는다()
        {
            var g = Engaged(new GruntSpec { hp = 10f });
            for (int i = 0; i < 100; i++) g.CannonHit(4f);
            Assert.IsTrue(g.Alive, "쓰러뜨리는 것은 P1의 몫");
            Assert.AreEqual(1f, g.Hp, 0.001f);
            Assert.AreEqual(GruntStep.Idle, g.Step, "가드 중이 아니면 포에 튕겨나지 않는다");
        }

        [Test]
        public void 포는_체력을_10분의_1만_깎는다()
        {
            var g = Engaged();
            g.CannonHit(40f);
            Assert.AreEqual(96f, g.Hp, 0.001f);
        }

        [Test]
        public void 공격_순서를_차례로_돈다()
        {
            // 돌격형 4: 내려치기, 내려치기, 휘두르기 반복.
            var g = Engaged(new GruntSpec { attacks = new[] { GruntAttack.Slam, GruntAttack.Slam, GruntAttack.Swing }, guardEveryDamage = 0f });
            var order = new List<GruntAttack>();
            for (int i = 0; i < 4; i++)
            {
                RunUntil(g, x => x.Events.Any(e => e.Kind == GruntEventKind.TelegraphStarted));
                order.Add(g.Attack);
            }
            CollectionAssert.AreEqual(new[] { GruntAttack.Slam, GruntAttack.Slam, GruntAttack.Swing, GruntAttack.Slam }, order);
        }

        [Test]
        public void 드론에_붙들리면_6초_멈추고_주먹은_절반만_들어간다()
        {
            var g = Engaged(new GruntSpec { guardEveryDamage = 0f });
            RunUntil(g, x => x.Step == GruntStep.Hold);
            g.Freeze(6f);
            Assert.AreEqual(GruntStep.Frozen, g.Step);
            Assert.AreEqual(PunchResult.Hit, g.Punch(25f));
            Assert.AreEqual(87.5f, g.Hp, 0.001f);

            var seen = new List<GruntEventKind>();
            float frozen = RunUntil(g, x => x.Step != GruntStep.Frozen, seen);
            Assert.AreEqual(6f, frozen, 0.03f);
            Assert.AreEqual(GruntStep.Idle, g.Step, "풀리면 기본 자세로");
            CollectionAssert.DoesNotContain(seen, GruntEventKind.RobotHit, "멈춘 동안에는 때리지 못한다");
        }

        [Test]
        public void 로봇이_물러나면_따라붙는다()
        {
            var g = Engaged(new GruntSpec { guardEveryDamage = 0f });
            var back = Robot + Vector3.back * 10f;
            for (int i = 0; i < 100; i++) g.Tick(Dt, back, false, PilotSide.Left);
            Assert.AreEqual(22f, Vector3.Distance(g.Position, back), 0.05f);
        }

        [Test]
        public void 같은_입력이면_같은_결과가_나온다()
        {
            string Play()
            {
                var g = new GruntDuel(new Vector3(0f, 0f, 130f), new GruntSpec { firstGuardDamage = 25f, attacks = new[] { GruntAttack.Slam, GruntAttack.Swing } });
                var log = new System.Text.StringBuilder();
                for (int i = 0; i < 1500; i++)
                {
                    bool guarding = i % 90 > 30;
                    g.Tick(Dt, Robot, guarding, i % 180 > 90 ? PilotSide.Right : PilotSide.Left);
                    if (i % 37 == 0) g.Punch(25f);
                    if (i % 11 == 0) g.CannonHit(4f);
                    foreach (var e in g.Events) log.Append(i).Append(':').Append(e.Kind).Append(' ');
                }
                return log.Append(g.Step).Append(g.Hp).Append(g.Position).ToString();
            }
            Assert.AreEqual(Play(), Play());
        }

        [Test]
        public void 장면_1_돌격형_1을_막고_쳐서_쓰러뜨린다()
        {
            // 처음부터 끝까지: 다가옴 → 예고 → 막기 → 튕겨난 틈에 잽 → 쓰러짐.
            var g = new GruntDuel(new Vector3(0f, 0f, 130f), new GruntSpec { holdSeconds = 1.2f, guardEveryDamage = 0f });
            var seen = new List<GruntEventKind>();
            float robotHull = 100f;
            float t = 0f, sincePunch = 0f;
            while (g.Alive && t < 60f)
            {
                // 예고가 보이면 왼쪽으로 막는다.
                bool guarding = g.Step == GruntStep.Rise || g.Step == GruntStep.Hold || g.Step == GruntStep.Strike;
                g.Tick(Dt, Robot, guarding, PilotSide.Left);
                t += Dt; sincePunch += Dt;
                // 튕겨난 동안 0.6초에 한 번 잽.
                if (g.Step == GruntStep.Stagger && sincePunch >= 0.6f) { g.Punch(25f); sincePunch = 0f; }
                foreach (var e in g.Events)
                {
                    seen.Add(e.Kind);
                    if (e.Kind == GruntEventKind.RobotHit) robotHull -= e.Amount;
                }
            }
            Assert.IsFalse(g.Alive, "60초 안에 쓰러뜨린다");
            Assert.AreEqual(100f, robotHull, "전부 막았으면 피해가 없다");
            CollectionAssert.Contains(seen, GruntEventKind.Down);
            Assert.Less(t, 20f, "한 명과의 싸움은 20초 안쪽");
        }
    }
}
