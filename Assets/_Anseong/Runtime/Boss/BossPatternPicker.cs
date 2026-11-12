using System;
using System.Collections.Generic;

namespace Anseong.Boss
{
    /// <summary>
    /// 다음 보스 기술 고르기(05 "페이즈별 순서"). 순수 클래스, 시드로 재현.
    /// 규칙: 페이즈 첫 공격 고정, 같은 손 3번 연속 금지, 내려찍기 연속 금지, 3페이즈 강공격(내려찍기·돌진) 연속 금지,
    /// 돌진은 3번에 1번까지, 2페이즈 시작 뒤 3번 안에 좌우 연타 한 번.
    /// </summary>
    public sealed class BossPatternPicker
    {
        readonly BossPhaseEntry[] phases;
        readonly Random random;
        readonly List<BossAttackKind> history = new List<BossAttackKind>(64);
        readonly List<AttackWeight> candidates = new List<AttackWeight>(8);
        int phase = 1, inPhase;
        bool comboShown;

        public BossPatternPicker(BossPhaseEntry[] phases, int seed)
        {
            this.phases = phases != null && phases.Length > 0 ? phases : BossPhaseTable.DefaultPhases();
            random = seed != 0 ? new Random(seed) : new Random();
        }

        public int Phase => phase;
        public IReadOnlyList<BossAttackKind> History => history;
        BossPhaseEntry Entry => phases[Math.Max(0, Math.Min(phases.Length - 1, phase - 1))];

        /// <summary>새 페이즈 시작(1부터). 다음 고르기는 그 페이즈 첫 공격.</summary>
        public void BeginPhase(int value)
        {
            phase = Math.Max(1, value);
            inPhase = 0;
            comboShown = false;
            lastChain = null;
        }

        public void Reset()
        {
            history.Clear();
            BeginPhase(1);
        }

        /// <summary>쉬는 시간(초). 금이 갈수록 짧아진다(뒤로 갈수록 거세게).</summary>
        public float RestSeconds(int cracks = 0)
        {
            var e = Entry;
            var min = Math.Min(e.restMin, e.restMax);
            var max = Math.Max(e.restMin, e.restMax);
            var cut = Math.Max(0.4f, 1f - e.restCutPerCrack * Math.Max(0, cracks));
            return (float)(min + random.NextDouble() * (max - min)) * cut;
        }

        readonly List<BossAttackKind> pattern = new List<BossAttackKind>(6);
        readonly List<BossChain> chainCandidates = new List<BossChain>(12);
        BossChain lastChain;

        /// <summary>
        /// 다음 패턴: 단발 하나 또는 연속기(기술 여러 개, 11/12). 페이즈 처음 몇 번은 단발만(기본기 먼저),
        /// 그 뒤 확률로 연속기. 금이 갈수록 확률이 오르고 더 긴 연속기가 풀린다. 같은 연속기는 연달아 안 나온다.
        /// 돌려준 목록은 다음 호출 때 다시 쓰이므로 바로 쓴다.
        /// </summary>
        public IReadOnlyList<BossAttackKind> NextPattern(int cracks = 0)
        {
            pattern.Clear();
            var e = Entry;
            var chain = inPhase >= Math.Max(1, e.singlesFirst) ? PickChain(e, Math.Max(0, cracks)) : null;
            if (chain == null)
            {
                lastChain = null;
                pattern.Add(Next());
                return pattern;
            }
            lastChain = chain;
            inPhase++;
            foreach (var kind in chain.attacks)
            {
                pattern.Add(kind);
                history.Add(kind);
                if (kind == BossAttackKind.AlternatingCombo) comboShown = true;
            }
            return pattern;
        }

        BossChain PickChain(BossPhaseEntry e, int cracks)
        {
            if (e.chains == null || e.chains.Length == 0) return null;
            var chance = e.chainChance + e.chainChancePerCrack * cracks;
            if (random.NextDouble() >= chance) return null;
            chainCandidates.Clear();
            var total = 0f;
            foreach (var c in e.chains)
            {
                if (c == null || c.attacks == null || c.attacks.Length < 2 || c.weight <= 0f || c.minCracks > cracks || c == lastChain) continue;
                chainCandidates.Add(c);
                total += c.weight;
            }
            if (chainCandidates.Count == 0) return null;
            var roll = random.NextDouble() * total;
            foreach (var c in chainCandidates)
            {
                roll -= c.weight;
                if (roll < 0) return c;
            }
            return chainCandidates[chainCandidates.Count - 1];
        }

        public BossAttackKind Next()
        {
            var e = Entry;
            BossAttackKind pick;
            if (inPhase == 0) pick = e.firstAttack;
            else if (e.showComboWithin > 0 && !comboShown && inPhase >= e.showComboWithin - 1 && Allowed(BossAttackKind.AlternatingCombo)) pick = BossAttackKind.AlternatingCombo;
            else pick = Weighted(e);
            inPhase++;
            if (pick == BossAttackKind.AlternatingCombo) comboShown = true;
            history.Add(pick);
            return pick;
        }

        BossAttackKind Weighted(BossPhaseEntry e)
        {
            candidates.Clear();
            var total = 0f;
            foreach (var w in e.weights)
            {
                if (w.weight <= 0f || !Allowed(w.kind)) continue;
                candidates.Add(w);
                total += w.weight;
            }
            if (candidates.Count == 0)
            {
                // 규칙에 모두 막히면 직전과 다른 손 펀치(늘 허용되는 안전한 선택).
                var last = Last(0);
                return last == BossAttackKind.PunchLeft ? BossAttackKind.PunchRight : BossAttackKind.PunchLeft;
            }
            var roll = random.NextDouble() * total;
            foreach (var c in candidates)
            {
                roll -= c.weight;
                if (roll < 0) return c.kind;
            }
            return candidates[candidates.Count - 1].kind;
        }

        bool Allowed(BossAttackKind kind)
        {
            var last = Last(0);
            var before = Last(1);
            switch (kind)
            {
                case BossAttackKind.PunchLeft:
                case BossAttackKind.PunchRight:
                    return !(last == kind && before == kind);
                case BossAttackKind.SlamBoth:
                    if (last == BossAttackKind.SlamBoth) return false;
                    return phase < 3 || last != BossAttackKind.Charge;
                case BossAttackKind.Charge:
                    if (last == BossAttackKind.Charge || before == BossAttackKind.Charge) return false;
                    return phase < 3 || last != BossAttackKind.SlamBoth;
                default:
                    return true;
            }
        }

        /// <summary>뒤에서 n번째(0 = 마지막). 없으면 -1처럼 쓰려고 어떤 기술과도 다른 값.</summary>
        BossAttackKind Last(int n)
        {
            var i = history.Count - 1 - n;
            return i >= 0 ? history[i] : (BossAttackKind)(-1);
        }
    }
}
