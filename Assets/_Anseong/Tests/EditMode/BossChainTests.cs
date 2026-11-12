using System.Collections.Generic;
using System.Linq;
using Anseong.Boss;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>11/12 연속기: 기술을 빈틈 없이 이어 치고, 빈틈은 마지막 뒤에만. 금이 갈수록 연속기가 늘어난다.</summary>
    public class BossChainTests
    {
        static BossAttackSpec L() => BossAttackSpec.Default(BossAttackKind.PunchLeft);
        static BossAttackSpec R() => BossAttackSpec.Default(BossAttackKind.PunchRight);
        static BossAttackSpec Slam() => BossAttackSpec.Default(BossAttackKind.SlamBoth);

        [Test]
        public void Chain_RunsLinksBackToBack_OpeningOnlyAtEnd()
        {
            var m = new BossAttackStateMachine { ChainLinkRecovery = 0.3f, ChainOpeningBonus = 0.5f };
            var events = new List<BossEvent>();
            m.Immediate = e => { if (e.Type == BossEventType.Impact) m.ReportOutcome(true); };
            Assert.IsTrue(m.BeginChain(new[] { L(), R(), Slam() }, 0.0, events));
            Assert.AreEqual(3, m.ChainLength);
            // 첫 기술: 예고 1.2 → 명중 → 0.3초 뒤 둘째 예고
            m.Update(1.49, false, events);
            Assert.AreEqual(BossState.Recovery, m.State);
            m.Update(1.51, false, events);
            Assert.AreEqual(BossState.Telegraph, m.State);
            Assert.AreEqual(BossAttackKind.PunchRight, m.Attack.kind);
            Assert.AreEqual(1, m.ChainLink);
            Assert.AreEqual(0, events.Count(e => e.Type == BossEventType.OpeningStarted), "기술 사이에는 빈틈 없음");
            // 둘째 1.5 + 1.2 = 2.7 명중, 3.0에 셋째(내려찍기 예고 1.8 → 4.8 명중, 회복 1.2 → 6.0)
            m.Update(3.01, false, events);
            Assert.AreEqual(BossAttackKind.SlamBoth, m.Attack.kind);
            m.Update(6.01, false, events);
            Assert.AreEqual(BossState.Staggered, m.State, "마지막 기술의 성공 경직");
            m.Update(6.81, false, events);
            Assert.AreEqual(BossState.Opening, m.State);
            var opening = events.Last(e => e.Type == BossEventType.OpeningStarted);
            Assert.AreEqual(Slam().openingOnSuccess + 0.5f * 2, opening.Seconds, 1e-5f, "전부 막으면 기술 수만큼 빈틈 보너스");
            Assert.AreEqual(3, events.Count(e => e.Type == BossEventType.AttackStarted));
            Assert.AreEqual(3, events.Count(e => e.Type == BossEventType.Impact));
        }

        [Test]
        public void Chain_OneMiss_GivesFailOpening()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            var n = 0;
            // 첫 타만 맞고 나머지는 막음
            m.Immediate = e => { if (e.Type == BossEventType.Impact) m.ReportOutcome(n++ > 0); };
            m.BeginChain(new[] { L(), R() }, 0.0, events);
            m.Update(100.0, false, events);
            var opening = events.Last(e => e.Type == BossEventType.OpeningStarted);
            Assert.AreEqual(R().openingOnFail, opening.Seconds, 1e-5f);
        }

        [Test]
        public void SingleBegin_StillBehavesAsBefore()
        {
            var m = new BossAttackStateMachine();
            var events = new List<BossEvent>();
            m.Begin(R(), 0.0, events);
            Assert.AreEqual(1, m.ChainLength);
            m.Update(2.21, false, events);
            Assert.AreEqual(BossState.Opening, m.State);
        }

        [Test]
        public void Reset_ClearsChain()
        {
            var m = new BossAttackStateMachine();
            m.BeginChain(new[] { L(), R() }, 0.0, null);
            m.Reset();
            Assert.AreEqual(0, m.ChainLength);
            Assert.IsTrue(m.Begin(R(), 1.0, null));
        }

        [Test]
        public void Picker_FirstAttacksAreSingles_ThenChainsAppear()
        {
            var picker = new BossPatternPicker(BossPhaseTable.DefaultPhases(), 7);
            picker.BeginPhase(1);
            for (var i = 0; i < 3; i++) Assert.AreEqual(1, picker.NextPattern(0).Count, "페이즈 처음 3번은 단발");
            var chains = 0;
            for (var i = 0; i < 200; i++)
                if (picker.NextPattern(0).Count > 1) chains++;
            Assert.Greater(chains, 30);
            Assert.Less(chains, 100);
        }

        [Test]
        public void Picker_MoreCracks_MoreAndLongerChains()
        {
            int Count(int cracks, out int longest)
            {
                var picker = new BossPatternPicker(BossPhaseTable.DefaultPhases(), 11);
                picker.BeginPhase(2);
                var n = 0;
                longest = 0;
                for (var i = 0; i < 300; i++)
                {
                    var p = picker.NextPattern(cracks);
                    if (p.Count > 1) n++;
                    if (p.Count > longest) longest = p.Count;
                }
                return n;
            }
            var calm = Count(0, out _);
            var fierce = Count(2, out var longest);
            Assert.Greater(fierce, calm);
            Assert.GreaterOrEqual(longest, 3);
        }

        [Test]
        public void Picker_NeverRepeatsSameChainTwice_AndRestShrinksWithCracks()
        {
            var picker = new BossPatternPicker(BossPhaseTable.DefaultPhases(), 3);
            picker.BeginPhase(3);
            string last = null;
            for (var i = 0; i < 300; i++)
            {
                var p = picker.NextPattern(2);
                var key = p.Count > 1 ? string.Join(",", p) : null;
                if (key != null && last != null) Assert.AreNotEqual(last, key);
                last = key;
            }
            var entry = BossPhaseTable.DefaultPhases()[2];
            for (var i = 0; i < 20; i++)
            {
                Assert.LessOrEqual(picker.RestSeconds(2), entry.restMax * (1f - entry.restCutPerCrack * 2) + 1e-4f);
            }
        }

        [Test]
        public void DefaultChains_AreTwoToFiveLong_AndEveryPhaseHasThem()
        {
            foreach (var phase in BossPhaseTable.DefaultPhases())
            {
                Assert.GreaterOrEqual(phase.chains.Length, 8);
                foreach (var c in phase.chains)
                {
                    Assert.GreaterOrEqual(c.attacks.Length, 2);
                    Assert.LessOrEqual(c.attacks.Length, 5);
                }
            }
            // 1페이즈(막기를 배우는 구간)에는 돌진이 없다.
            Assert.IsFalse(BossPhaseTable.DefaultPhases()[0].chains.Any(c => c.attacks.Contains(BossAttackKind.Charge)));
        }
    }
}
