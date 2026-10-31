using System.Collections.Generic;
using System.Linq;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M2-02 보스 공격 흐름: 상태 순서·시간, 연계 중 대기, 빈틈 밖 피해 0, 연타 타 수.</summary>
    public class BossAttackStateMachineTests
    {
        static BossAttackSpec Punch() => BossAttackSpec.Default(BossAttackKind.PunchRight);

        [Test]
        public void SinglePunch_RunsTelegraphStrikeRecoveryOpeningRest()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            var spec = Punch(); // 예고 1.2, 회복 1.0, 실패 빈틈 1.5
            Assert.IsTrue(m.Begin(spec, 10.0, events));
            Assert.AreEqual(BossState.Telegraph, m.State);
            Assert.IsFalse(m.Begin(spec, 10.1, events), "공격 중에는 새 공격 안 됨");

            m.Update(11.1, false, events);
            Assert.AreEqual(BossState.Telegraph, m.State);
            m.Update(11.21, false, events);
            Assert.AreEqual(BossState.Recovery, m.State, "단발은 명중 순간 뒤 바로 회복");
            var impact = events.Single(e => e.Type == BossEventType.Impact);
            Assert.AreEqual(11.2, impact.Time, 1e-5, "정해진 명중 시각(프레임 시각 아님)");
            Assert.AreEqual(PilotSide.Right, impact.Side);

            m.Update(12.21, false, events);
            Assert.AreEqual(BossState.Opening, m.State);
            Assert.AreEqual(1.5f, events.Last(e => e.Type == BossEventType.OpeningStarted).Seconds, 1e-6f, "대응 결과가 없으면 실패 빈틈");
            Assert.IsTrue(m.CanBeDamaged);
            m.Update(13.69, false, events);
            Assert.AreEqual(BossState.Opening, m.State);
            m.Update(13.72, false, events);
            Assert.AreEqual(BossState.Rest, m.State);
            Assert.IsFalse(m.CanBeDamaged);
        }

        [Test]
        public void LongFrame_PassesStatesInOrder()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            m.Begin(Punch(), 0.0, events);
            m.Update(100.0, false, events);
            var order = events.Select(e => e.Type).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                BossEventType.AttackStarted, BossEventType.Impact, BossEventType.RecoveryStarted, BossEventType.OpeningStarted, BossEventType.OpeningEnded,
            }, order);
        }

        [Test]
        public void SuccessfulDefense_GivesLongOpening_SlamStaggersFirst()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            m.Immediate = e =>
            {
                if (e.Type == BossEventType.Impact) m.ReportOutcome(true);
            };
            var slam = BossAttackSpec.Default(BossAttackKind.SlamBoth); // 예고 1.8, 회복 1.2, 경직 0.8, 빈틈 3.0
            m.Begin(slam, 0.0, events);
            m.Update(3.01, false, events);
            Assert.AreEqual(BossState.Staggered, m.State);
            Assert.IsTrue(m.CanBeDamaged, "경직 중에도 피해");
            m.Update(3.82, false, events);
            Assert.AreEqual(BossState.Opening, m.State);
            Assert.AreEqual(3f, events.Last(e => e.Type == BossEventType.OpeningStarted).Seconds, 1e-6f);
        }

        [Test]
        public void AlternatingCombo_FourImpacts_AlternatingSides_AllBlockedForLongOpening()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            var blocked = 0;
            m.Immediate = e =>
            {
                if (e.Type != BossEventType.Impact) return;
                // 마지막 타만 놓침 → 실패 빈틈
                var ok = e.Hit < 3;
                if (ok) blocked++;
                m.ReportOutcome(ok);
            };
            var combo = BossAttackSpec.Default(BossAttackKind.AlternatingCombo);
            m.Begin(combo, 0.0, events);
            m.Update(1.25, false, events);
            Assert.AreEqual(BossState.Strike, m.State, "첫 타 뒤 다음 타를 기다림");
            m.Update(10.0, false, events);
            var impacts = events.Where(e => e.Type == BossEventType.Impact).ToArray();
            Assert.AreEqual(4, impacts.Length);
            CollectionAssert.AreEqual(new[] { 1.2, 1.9, 2.6, 3.3 }, impacts.Select(e => System.Math.Round(e.Time, 3)).ToArray());
            CollectionAssert.AreEqual(new[] { PilotSide.Right, PilotSide.Left, PilotSide.Right, PilotSide.Left }, impacts.Select(e => e.Side).ToArray());
            Assert.AreEqual(3, blocked);
            Assert.AreEqual(1.5f, events.First(e => e.Type == BossEventType.OpeningStarted).Seconds, 1e-6f, "한 타라도 놓치면 짧은 빈틈");
        }

        [Test]
        public void OpeningHeldWhileComboActive_UpTo24Seconds()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            m.Begin(Punch(), 0.0, events);
            m.Update(2.25, false, events); // 빈틈 시작 2.2, 끝 3.7
            Assert.AreEqual(BossState.Opening, m.State);
            m.Update(4.0, true, events);
            Assert.AreEqual(BossState.Opening, m.State, "연계 중이면 빈틈 유지");
            m.Update(6.0, true, events);
            Assert.AreEqual(BossState.Opening, m.State);
            m.Update(6.2, true, events);
            Assert.AreEqual(BossState.Rest, m.State, "최대 2.4초까지만 미룸");

            var m2 = new BossAttackStateMachine();
            m2.Begin(Punch(), 0.0, events);
            m2.Update(4.0, true, events);
            m2.Update(4.1, false, events);
            Assert.AreEqual(BossState.Rest, m2.State, "연계가 끝나면 바로 쉬는 중");
        }

        [Test]
        public void Resolver_BlocksDamageOutsideOpening_KeepsComboTurn()
        {
            var m = new BossAttackStateMachine();
            var resolver = new CombatResolver { BossVulnerable = () => m.CanBeDamaged };
            var events = new List<CombatEvent>();
            resolver.Resolve(new HitReport { Side = PilotSide.Right, Kind = PunchKind.Jab, StrikeId = 1 }, 0.0, events);
            Assert.AreEqual(CombatCue.Blocked, events.Last().Cue);
            Assert.AreEqual(100f, resolver.Boss.Current, 1e-4f, "쉬는 중엔 피해 0");
            Assert.AreEqual(0, resolver.Combo.Step, "막힘은 연계를 올리지 않음");

            m.Begin(Punch(), 0.0, null);
            m.Update(2.3, false, null);
            Assert.IsTrue(m.CanBeDamaged);
            resolver.Resolve(new HitReport { Side = PilotSide.Right, Kind = PunchKind.Jab, StrikeId = 2 }, 2.3, events);
            Assert.AreEqual(1, resolver.Combo.Step);
            Assert.AreEqual(97f, resolver.Boss.Current, 1e-4f);

            // 연계 중 막힘은 차례를 끊지 않는다.
            m.Reset();
            resolver.Resolve(new HitReport { Side = PilotSide.Left, Kind = PunchKind.Jab, StrikeId = 1 }, 2.5, events);
            Assert.AreEqual(CombatCue.Blocked, events.Last().Cue);
            Assert.AreEqual(1, resolver.Combo.Step);
            Assert.AreEqual(PilotSide.Left, resolver.Combo.NextSide);
        }

        [Test]
        public void Defeat_StopsEverything()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            m.Begin(Punch(), 0.0, events);
            m.Defeat(0.5, events);
            m.Update(10.0, false, events);
            Assert.AreEqual(BossState.Defeated, m.State);
            Assert.IsFalse(events.Any(e => e.Type == BossEventType.Impact));
        }
    }
}
