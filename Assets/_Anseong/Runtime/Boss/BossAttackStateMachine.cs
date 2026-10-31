using System.Collections.Generic;
using Anseong.Pilot;

namespace Anseong.Boss
{
    public enum BossEventType
    {
        /// <summary>예고 시작(공격 시작).</summary>
        AttackStarted,
        /// <summary>명중 순간(연타는 타마다). 대응 판정(M2-03)이 이때 <see cref="BossAttackStateMachine.ReportOutcome"/>를 부른다.</summary>
        Impact,
        /// <summary>마지막 명중 뒤 회복 시작.</summary>
        RecoveryStarted,
        /// <summary>경직 시작. Seconds = 길이.</summary>
        StaggerStarted,
        /// <summary>빈틈 시작. Seconds = 길이.</summary>
        OpeningStarted,
        /// <summary>빈틈이 끝나 쉬는 중으로.</summary>
        OpeningEnded,
        Defeated,
    }

    public struct BossEvent
    {
        public BossEventType Type;
        public BossAttackKind Attack;
        /// <summary>몇 번째 타(0부터, Impact).</summary>
        public int Hit;
        public PilotSide Side;
        /// <summary>그 일이 일어나기로 한 서버 시각(프레임 시각이 아니라 정해진 시각).</summary>
        public double Time;
        public float Seconds;
    }

    /// <summary>
    /// 보스 한 번의 공격 흐름(05): 쉬는 중 → 예고 → 명중 순간 → 회복 → (경직) → 빈틈 → 쉬는 중. 서버 시각으로만 진행. 순수 클래스.
    /// 대응 결과(막음·피함)는 명중 순간마다 <see cref="ReportOutcome"/>로 받는다. 받지 못한 타는 실패로 친다.
    /// 빈틈이 끝날 때 연계가 이어지는 중이면 연계가 끝날 때까지(최대 <see cref="ComboHoldSeconds"/>) 빈틈을 늘린다.
    /// </summary>
    public sealed class BossAttackStateMachine
    {
        BossAttackSpec attack;
        int nextHit, succeeded;
        double stateEnd, holdUntil;
        float pendingOpening;

        /// <summary>연계가 이어지는 동안 다음 공격을 미루는 최대 시간(05: 2.4초).</summary>
        public float ComboHoldSeconds = 2.4f;

        /// <summary>사건이 생기는 즉시 불린다(목록에 더하기 전). 서버가 명중 순간 대응 판정을 여기서 한다.</summary>
        public System.Action<BossEvent> Immediate;

        public BossState State { get; private set; } = BossState.Rest;
        /// <summary>지금 상태가 시작된 서버 시각(애니메이션 복제용).</summary>
        public double StateStart { get; private set; }
        public double AttackStart { get; private set; }
        public BossAttackSpec Attack => attack;
        public bool HasAttack => attack != null;
        /// <summary>빈틈·경직·무릎: 이때만 피해가 들어간다(무릎은 HP 바닥에 막혀 필살기만 끝낸다).</summary>
        public bool CanBeDamaged => State == BossState.Opening || State == BossState.Staggered || State == BossState.Kneel;
        public bool IsResting => State == BossState.Rest;
        /// <summary>공격 중(예고·명중·회복).</summary>
        public bool IsAttacking => State == BossState.Telegraph || State == BossState.Strike || State == BossState.Recovery;

        /// <summary>쉬는 중일 때만 새 공격을 시작한다.</summary>
        public bool Begin(BossAttackSpec spec, double now, List<BossEvent> events)
        {
            if (spec == null || State != BossState.Rest) return false;
            attack = spec;
            nextHit = 0;
            succeeded = 0;
            AttackStart = now;
            Enter(BossState.Telegraph, now);
            Add(events, new BossEvent { Type = BossEventType.AttackStarted, Attack = spec.kind, Side = spec.firstSide, Time = now, Seconds = spec.telegraphSeconds });
            return true;
        }

        /// <summary>명중 순간의 대응 결과(M2-03). 같은 타를 두 번 보내지 않는다.</summary>
        public void ReportOutcome(bool success)
        {
            if (success) succeeded++;
        }

        /// <summary>모든 타를 막았는지(연타 4타 모두).</summary>
        public bool AllSucceeded => attack != null && succeeded >= attack.HitCount;

        public void Update(double now, bool comboActive, List<BossEvent> events)
        {
            // 한 프레임이 길어도 순서대로 지나간다(여러 단계를 한 번에 넘을 수 있음).
            for (var guard = 0; guard < 16; guard++)
            {
                if (!Step(now, comboActive, events)) return;
            }
        }

        bool Step(double now, bool comboActive, List<BossEvent> events)
        {
            switch (State)
            {
                case BossState.Telegraph:
                case BossState.Strike:
                {
                    if (nextHit < attack.HitCount)
                    {
                        var at = AttackStart + attack.StrikeTime(nextHit);
                        if (now < at) return false;
                        var hit = nextHit++;
                        if (State == BossState.Telegraph) Enter(BossState.Strike, at);
                        Add(events, new BossEvent { Type = BossEventType.Impact, Attack = attack.kind, Hit = hit, Side = attack.SideOf(hit), Time = at });
                        if (nextHit >= attack.HitCount)
                        {
                            Enter(BossState.Recovery, at);
                            Add(events, new BossEvent { Type = BossEventType.RecoveryStarted, Attack = attack.kind, Time = at });
                        }
                        return true;
                    }
                    return false;
                }
                case BossState.Recovery:
                {
                    var end = AttackStart + attack.EndTime;
                    if (now < end) return false;
                    var success = AllSucceeded;
                    pendingOpening = success ? attack.openingOnSuccess : attack.openingOnFail;
                    var stagger = success ? attack.staggerOnSuccess : 0f;
                    if (stagger > 0f) BeginStagger(stagger, end, events);
                    else BeginOpening(pendingOpening, end, events);
                    return true;
                }
                case BossState.Staggered:
                    if (now < stateEnd) return false;
                    BeginOpening(pendingOpening, stateEnd, events);
                    return true;
                case BossState.Opening:
                    if (now < stateEnd) return false;
                    if (comboActive && now < holdUntil) return false;
                    Enter(BossState.Rest, now);
                    attack = null;
                    Add(events, new BossEvent { Type = BossEventType.OpeningEnded, Time = now });
                    return true;
                case BossState.Walk:
                case BossState.Roar:
                case BossState.KnockedBack:
                case BossState.Kneel:
                    if (now < stateEnd) return false;
                    Enter(BossState.Rest, stateEnd);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 공격 밖 동작(걷기·포효·밀려남·무릎, M2-04)으로 바꾼다. 공격·빈틈 중이면 끊는다. seconds가 지나면 쉬는 중으로(무한이면 <see cref="Release"/>까지).
        /// </summary>
        public void Hold(BossState state, double now, double seconds = double.PositiveInfinity)
        {
            if (State == BossState.Defeated) return;
            attack = null;
            Enter(state, now);
            stateEnd = now + seconds;
        }

        /// <summary>공격 밖 동작을 끝내고 쉬는 중으로.</summary>
        public void Release(double now)
        {
            if (!IsHolding) return;
            Enter(BossState.Rest, now);
        }

        /// <summary>걷기·포효·밀려남·무릎 중.</summary>
        public bool IsHolding => State == BossState.Walk || State == BossState.Roar || State == BossState.KnockedBack || State == BossState.Kneel;

        /// <summary>바깥에서 경직을 건다(예: 부스트로 밀려남, M2-04). 끝나면 빈틈 openingAfter초.</summary>
        public void Stagger(float seconds, float openingAfter, double now, List<BossEvent> events)
        {
            if (State == BossState.Defeated || seconds <= 0f) return;
            pendingOpening = openingAfter;
            BeginStagger(seconds, now, events);
        }

        public void Defeat(double now, List<BossEvent> events)
        {
            if (State == BossState.Defeated) return;
            Enter(BossState.Defeated, now);
            Add(events, new BossEvent { Type = BossEventType.Defeated, Time = now });
        }

        public void Reset()
        {
            attack = null;
            nextHit = succeeded = 0;
            pendingOpening = 0f;
            State = BossState.Rest;
            StateStart = AttackStart = stateEnd = holdUntil = 0d;
        }

        void BeginStagger(float seconds, double at, List<BossEvent> events)
        {
            Enter(BossState.Staggered, at);
            stateEnd = at + seconds;
            Add(events, new BossEvent { Type = BossEventType.StaggerStarted, Attack = attack != null ? attack.kind : default, Time = at, Seconds = seconds });
        }

        void BeginOpening(float seconds, double at, List<BossEvent> events)
        {
            Enter(BossState.Opening, at);
            stateEnd = at + seconds;
            holdUntil = stateEnd + ComboHoldSeconds;
            Add(events, new BossEvent { Type = BossEventType.OpeningStarted, Attack = attack != null ? attack.kind : default, Time = at, Seconds = seconds });
        }

        void Enter(BossState state, double at)
        {
            State = state;
            StateStart = at;
        }

        void Add(List<BossEvent> events, BossEvent e)
        {
            // 명중 순간 대응 판정은 다음 단계(회복 끝 → 빈틈 길이)보다 먼저 돌아야 해서 바로 부른다.
            Immediate?.Invoke(e);
            events?.Add(e);
        }
    }
}
