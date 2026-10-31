using System.Collections.Generic;
using System.Linq;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M2-04: 기술 비율(시드 고정 1,000회), 연속 금지, 첫 공격, 경계 멈춤, 회복, 무릎 진입.</summary>
    public class BossPhaseTests
    {
        static List<BossAttackKind> Run(int phase, int count, int seed = 1234)
        {
            var picker = new BossPatternPicker(BossPhaseTable.DefaultPhases(), seed);
            picker.BeginPhase(phase);
            var list = new List<BossAttackKind>();
            for (var i = 0; i < count; i++) list.Add(picker.Next());
            return list;
        }

        static float Share(List<BossAttackKind> list, params BossAttackKind[] kinds) => list.Skip(1).Count(kinds.Contains) / (float)(list.Count - 1);

        [Test]
        public void FirstAttack_IsNewMoveOfPhase()
        {
            Assert.AreEqual(BossAttackKind.PunchRight, Run(1, 1)[0]);
            Assert.AreEqual(BossAttackKind.SlamBoth, Run(2, 1)[0]);
            Assert.AreEqual(BossAttackKind.Charge, Run(3, 1)[0]);
        }

        [Test]
        public void Phase1_HalfLeftHalfRight()
        {
            var list = Run(1, 1000);
            Assert.AreEqual(0.5f, Share(list, BossAttackKind.PunchLeft), 0.05f);
            Assert.IsFalse(list.Any(k => k != BossAttackKind.PunchLeft && k != BossAttackKind.PunchRight));
        }

        [Test]
        public void Phase2_Ratios_RoughlyMatchTable()
        {
            var list = Run(2, 1000);
            // 내려찍기 연속 금지 때문에 내려찍기는 표(25%)보다 조금 적다.
            Assert.AreEqual(0.5f, Share(list, BossAttackKind.PunchLeft, BossAttackKind.PunchRight), 0.08f);
            Assert.AreEqual(0.25f, Share(list, BossAttackKind.AlternatingCombo), 0.06f);
            Assert.AreEqual(0.22f, Share(list, BossAttackKind.SlamBoth), 0.06f);
            Assert.IsFalse(list.Contains(BossAttackKind.Charge), "돌진은 3페이즈부터");
        }

        [Test]
        public void Phase3_Ratios_AndChargeAtMostOneInThree()
        {
            var list = Run(3, 1000);
            // 강공격 연속 금지·돌진 3번에 1번 때문에 단발 펀치가 표(35%)보다 늘고 돌진·내려찍기는 준다(시뮬레이션 약 43%·15%·15%).
            Assert.AreEqual(0.43f, Share(list, BossAttackKind.PunchLeft, BossAttackKind.PunchRight), 0.08f);
            Assert.Greater(Share(list, BossAttackKind.Charge), 0.1f);
            Assert.LessOrEqual(Share(list, BossAttackKind.Charge), 1f / 3f + 0.01f);
        }

        [Test]
        public void NoForbiddenSequences()
        {
            foreach (var phase in new[] { 1, 2, 3 })
            foreach (var seed in new[] { 1, 7, 99 })
            {
                var list = Run(phase, 600, seed);
                for (var i = 1; i < list.Count; i++)
                {
                    var a = list[i - 1];
                    var b = list[i];
                    if (i >= 2 && (b == BossAttackKind.PunchLeft || b == BossAttackKind.PunchRight))
                        Assert.IsFalse(list[i - 2] == b && a == b, $"같은 손 3연속 (페이즈 {phase}, {i})");
                    Assert.IsFalse(a == BossAttackKind.SlamBoth && b == BossAttackKind.SlamBoth, "내려찍기 연속");
                    if (phase == 3 && i > 1)
                        Assert.IsFalse(IsHeavy(a) && IsHeavy(b), $"강공격 연속 ({i})");
                    if (b == BossAttackKind.Charge && i >= 2)
                        Assert.IsFalse(a == BossAttackKind.Charge || list[i - 2] == BossAttackKind.Charge, "돌진은 3번에 1번");
                }
            }
        }

        static bool IsHeavy(BossAttackKind k) => k == BossAttackKind.SlamBoth || k == BossAttackKind.Charge;

        [Test]
        public void Phase2_ShowsComboWithinThree()
        {
            for (var seed = 1; seed <= 50; seed++)
                CollectionAssert.Contains(Run(2, 3, seed), BossAttackKind.AlternatingCombo, $"시드 {seed}");
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            CollectionAssert.AreEqual(Run(3, 50, 42), Run(3, 50, 42));
        }

        [Test]
        public void BigCombo_StopsAtBoundary_ThenPhaseAfterAttack()
        {
            var health = new BossHealth();
            var phases = new BossPhaseRules();
            health.Floor = phases.FloorPercent;
            health.Apply(30f); // 70
            health.Apply(30f); // 60에서 멈춤(넘친 10 버림)
            Assert.AreEqual(60f, health.Current, 1e-4f);
            Assert.AreEqual(PhaseStep.Pending, phases.Update(1.0, 60f, busy: true), "공격 중이면 기다림");
            Assert.AreEqual(1, phases.Phase);
            Assert.AreEqual(PhaseStep.PhaseChanged, phases.Update(2.0, 60f, busy: false));
            Assert.AreEqual(2, phases.Phase);
            Assert.IsTrue(phases.IsRoaring(3.9), "포효 2초 무적");
            Assert.IsFalse(phases.IsRoaring(4.1));
            health.Floor = phases.FloorPercent;
            health.Apply(50f);
            Assert.AreEqual(25f, health.Current, 1e-4f, "다음 경계 25%에서 멈춤");
        }

        [Test]
        public void LastBoundary_Kneels_FloorStaysUntilReleased()
        {
            var phases = new BossPhaseRules();
            phases.Update(0, 60f, false);
            phases.Update(0, 25f, false);
            Assert.AreEqual(3, phases.Phase);
            Assert.AreEqual(PhaseStep.Kneel, phases.Update(10, 10f, false));
            Assert.IsTrue(phases.Kneeling);
            Assert.AreEqual(10f, phases.FloorPercent, "무릎 뒤에도 펀치로는 못 끝냄");
            Assert.AreEqual(PhaseStep.None, phases.Update(11, 10f, false));
            phases.ReleaseFloor();
            Assert.AreEqual(0f, phases.FloorPercent);
            phases.Reset();
            Assert.AreEqual(1, phases.Phase);
            Assert.IsFalse(phases.Kneeling);
        }

        [Test]
        public void PhaseStart_RestoresPilotTo60()
        {
            var server = new CombatAuthority(new CombatResolver());
            server.ResolveBossStrike(BossCounter.Dodge, PilotSide.Right, 15f, 0, 0, null);
            server.ResolveBossStrike(BossCounter.Dodge, PilotSide.Right, 15f, 0, 1, null);
            server.ResolveBossStrike(BossCounter.Dodge, PilotSide.Right, 15f, 0, 2, null);
            Assert.Less(server.Pilot.Percent, 60f);
            Assert.IsTrue(server.Pilot.RestoreTo(60f));
            Assert.AreEqual(60f, server.Snapshot.PilotHealth, 1e-4f);
        }

        [Test]
        public void HoldStates_ReturnToRest_AndRoarIsNotDamageable()
        {
            var m = new BossAttackStateMachine();
            m.Hold(BossState.Roar, 0, 2);
            Assert.IsFalse(m.CanBeDamaged);
            Assert.IsFalse(m.Begin(BossAttackSpec.Default(BossAttackKind.PunchLeft), 1, null), "포효 중엔 공격 안 함");
            m.Update(2.01, false, null);
            Assert.AreEqual(BossState.Rest, m.State);
            m.Hold(BossState.Kneel, 3);
            m.Update(1000, false, null);
            Assert.AreEqual(BossState.Kneel, m.State, "무릎은 계속");
            Assert.IsTrue(m.CanBeDamaged);
        }
    }
}
