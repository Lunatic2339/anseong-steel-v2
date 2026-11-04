using System;
using System.Collections.Generic;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Net;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace Anseong.Boss
{
    /// <summary>복제되는 보스 동작: 상태 + 기술 + 시작 서버 시각. 각 PC가 경과 시간만큼 앞당겨 클립을 튼다.</summary>
    public struct BossAction : INetworkSerializeByMemcpy, IEquatable<BossAction>
    {
        public const byte NoAttack = 255;
        public byte State;
        public byte Attack;
        public double Start;

        public BossState BossState => (BossState)State;
        public bool HasAttack => Attack != NoAttack;
        public BossAttackKind AttackKind => (BossAttackKind)Attack;

        public bool Equals(BossAction other) => State == other.State && Attack == other.Attack && Start.Equals(other.Start);
        public override bool Equals(object obj) => obj is BossAction other && Equals(other);
        public override int GetHashCode() => (State << 8 | Attack) ^ Start.GetHashCode();
    }

    /// <summary>
    /// 보스 두뇌(M2-02, 서버만). 공격 상태기계를 서버 시각으로 돌리고, 사건을 전투 이벤트(예고·명중 순간·빈틈)로 보내고,
    /// 동작(<see cref="BossAction"/>)을 복제한다. 보스가 빈틈·경직일 때만 피해가 들어가게 전투 규칙에 물려 준다.
    /// 시험용(헤드셋을 쓴 채, 키보드 없이): 쉬는 시간마다 기술을 돌아가며 또는 하나만 쓴다(조절 메뉴 "보스 시험"). 기술 고르기·페이즈는 M2-04.
    /// 보스 프리팹 루트(NetworkObject와 함께)에 메뉴가 붙인다.
    /// </summary>
    public class BossServer : NetworkBehaviour, ITunable
    {
        [SerializeField, Tooltip("전투 서버. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatServer combat;

        [SerializeField, Tooltip("기술 에셋(05 기술표). 메뉴가 Data/Boss의 5개를 넣는다.")]
        BossAttackDefinition[] attacks = new BossAttackDefinition[0];

        [SerializeField, Tooltip("쉬는 중에도 피해를 받는다(시험 공격을 끈 때만). 패턴(M2-04)이 들어오면 끈다(05: 빈틈에만 피해).")]
        bool restIsOpening = true;

        [SerializeField, Min(0f), Tooltip("빈틈이 끝나도 연계가 이어지는 중이면 다음 공격을 이만큼까지 미룬다(05: 2.4초).")]
        float comboHoldSeconds = 2.4f;

        [Header("시험 공격 (헤드셋, 키보드 없이)")]
        [SerializeField, Tooltip("쉬는 시간이 지나면 스스로 기술을 쓴다. 0 = 끔, 1 = 다섯 기술 돌아가며, 2~6 = 그 기술만(왼손 펀치, 오른손 펀치, 좌우 연타, 내려찍기, 돌진). 조절 메뉴 \"보스 시험\". 패턴(M2-04)이 켜지면 쓰지 않는다.")]
        int testAttack = 1;
        [SerializeField, Min(0.5f), Tooltip("시험 공격: 공격이 끝나고(빈틈 포함) 다음 공격까지 쉬는 시간(초).")]
        float testRestSeconds = 3f;

        [SerializeField, Tooltip("예비: 에디터에서 숫자 키 1~5로 기술 하나. 헤드셋 시험에는 쓰지 않는다.")]
        bool debugKeys = true;

        readonly NetworkVariable<BossAction> action = new NetworkVariable<BossAction>(new BossAction { Attack = BossAction.NoAttack });
        readonly BossAttackStateMachine machine = new BossAttackStateMachine();
        readonly List<BossEvent> bossEvents = new List<BossEvent>(8);
        bool bound;

        /// <summary>모든 PC: 지금 보스 동작(복제).</summary>
        public BossAction Action => action.Value;
        /// <summary>서버: 공격 상태기계.</summary>
        public BossAttackStateMachine Machine => machine;
        public CombatServer Combat => combat;
        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public bool RestIsOpening { get => restIsOpening; set => restIsOpening = value; }
        /// <summary>시험 공격을 막는다(보스가 스스로 싸우는 패턴이 켜졌을 때, M2-04).</summary>
        public bool TestAttacksBlocked { get; set; }

        static readonly string[] TestAttackNames = { "끔", "다섯 기술 돌아가며", "왼손 펀치만", "오른손 펀치만", "좌우 연타만", "내려찍기만", "돌진만" };
        int testCursor;
        double restSince = -1;

        /// <summary>서버: 보스 사건이 생기는 즉시(명중 순간 대응 판정 M2-03, 페이즈 M2-04가 듣는다).</summary>
        public event Action<BossEvent> ServerEvent;

        /// <summary>서버: 재시작 때.</summary>
        public event Action ServerReset;

        public override void OnNetworkSpawn()
        {
            if (combat == null) combat = FindAnyObjectByType<CombatServer>();
            machine.ComboHoldSeconds = comboHoldSeconds;
            machine.Immediate = OnImmediate;
        }

        public override void OnNetworkDespawn()
        {
            if (combat != null) combat.ServerReset -= OnReset;
            if (bound && combat != null && combat.Authority != null) combat.Authority.Resolver.BossVulnerable = null;
            bound = false;
        }

        public BossAttackDefinition Definition(BossAttackKind kind)
        {
            foreach (var a in attacks)
                if (a != null && a.Kind == kind) return a;
            return null;
        }

        public BossAttackSpec SpecOf(BossAttackKind kind)
        {
            var def = Definition(kind);
            return def != null ? def.spec : BossAttackSpec.Default(kind);
        }

        /// <summary>서버: 기술 하나 시작(쉬는 중일 때만).</summary>
        public bool TryBegin(BossAttackKind kind)
        {
            if (!IsServer || !bound || combat.Authority.Pilot.IsDefeated) return false;
            if (!machine.Begin(SpecOf(kind), Now, bossEvents)) return false;
            Publish();
            return true;
        }

        /// <summary>서버: 지금 피해를 받는지.</summary>
        public bool Vulnerable() => machine.CanBeDamaged || (restIsOpening && machine.IsResting && !TestAttacking);

        /// <summary>시험 공격이 돌고 있다: 쉬는 중은 막힘(05대로 빈틈에만 피해).</summary>
        bool TestAttacking => testAttack > 0 && !TestAttacksBlocked;

        void Update()
        {
            if (!IsServer) return;
            if (!bound && !TryBind()) return;
            var now = Now;
            var authority = combat.Authority;
            if (authority.Resolver.Boss.IsDefeated) machine.Defeat(now, bossEvents);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugKeys) DebugKeys();
#endif
            var snapshot = authority.Snapshot;
            machine.ComboHoldSeconds = comboHoldSeconds;
            machine.Update(now, snapshot.ComboStep > 0 || snapshot.BoostOpen, bossEvents);
            TestAttack(now);
            Publish();
        }

        /// <summary>시험 공격: 쉬는 중이 testRestSeconds 이어지면 다음 기술.</summary>
        void TestAttack(double now)
        {
            if (!machine.IsResting || !TestAttacking)
            {
                restSince = -1;
                return;
            }
            if (restSince < 0) restSince = now;
            if (now - restSince < testRestSeconds) return;
            BossAttackKind kind;
            if (testAttack == 1)
            {
                kind = (BossAttackKind)(testCursor % 5);
                testCursor++;
            }
            else kind = (BossAttackKind)Mathf.Clamp(testAttack - 2, 0, 4);
            restSince = -1;
            TryBegin(kind);
        }

        public void AddTuning(TuningList list)
        {
            const string g = "보스 시험";
            list.Describe(g, "보스가 쉬는 시간마다 기술을 쓴다(키보드 필요 없음)\n할 것: 예고(깜빡임) 때 막기, 빈틈 안내가 뜨면 치기\n볼 것: 예고 중 펀치는 막힘, 빈틈에만 HP가 줄어듦");
            list.AddChoice(g, "boss.testAttack", "시험 공격", () => testAttack, v => { testAttack = v; restSince = -1; }, TestAttackNames);
            list.AddFloat(g, "boss.testRest", "공격 사이 쉬는 시간(초)", () => testRestSeconds, v => testRestSeconds = v, 0.5f, 10f, 0.5f, 1);
        }

        bool TryBind()
        {
            if (combat == null || combat.Authority == null) return false;
            combat.Authority.Resolver.BossVulnerable = Vulnerable;
            combat.ServerReset += OnReset;
            bound = true;
            return true;
        }

        void OnImmediate(BossEvent e)
        {
            if (e.Type == BossEventType.Impact && combat != null && combat.Authority != null)
            {
                // 명중 순간: 먼저 "명중 순간" 사건, 이어서 가드·회피 판정 결과(M2-03). 결과는 빈틈 길이를 정한다.
                combat.Events.Add(new CombatEvent
                {
                    Cue = CombatCue.BossStrike, Side = e.Side, Kind = PunchKind.None, Time = e.Time, Code = (int)e.Attack, Amount = e.Hit,
                    BossHealth = combat.Authority.Resolver.Boss.Current, Point = transform.position,
                });
                var spec = machine.Attack ?? SpecOf(e.Attack);
                var result = combat.Authority.ResolveBossStrike(spec.counter, e.Side, spec.failDamage, (int)e.Attack, e.Time, combat.Events);
                machine.ReportOutcome(DefenseRules.IsSuccess(result));
            }
            ServerEvent?.Invoke(e);
        }

        void OnReset()
        {
            machine.Reset();
            testCursor = 0;
            restSince = -1;
            bossEvents.Clear();
            ServerReset?.Invoke();
            Publish();
        }

        /// <summary>쌓인 보스 사건을 전투 이벤트로 바꿔 보내고 동작을 복제한다.</summary>
        void Publish()
        {
            if (combat == null || combat.Authority == null) return;
            var health = combat.Authority.Resolver.Boss.Current;
            foreach (var e in bossEvents)
            {
                var cue = new CombatEvent { Side = e.Side, Kind = PunchKind.None, Time = e.Time, Code = (int)e.Attack, BossHealth = health, Point = transform.position };
                switch (e.Type)
                {
                    case BossEventType.AttackStarted:
                        cue.Cue = CombatCue.BossTelegraph;
                        cue.Amount = (int)SpecOf(e.Attack).level;
                        // 안내 글자용 타 수(M2-06, 연타 "4번").
                        cue.ComboStep = SpecOf(e.Attack).HitCount;
                        break;
                    case BossEventType.OpeningStarted:
                        cue.Cue = CombatCue.BossOpening;
                        cue.Amount = e.Seconds;
                        break;
                    default:
                        continue;
                }
                combat.Events.Add(cue);
            }
            bossEvents.Clear();
            combat.Flush();

            var next = new BossAction
            {
                State = (byte)machine.State,
                Attack = machine.HasAttack && machine.IsAttacking ? (byte)machine.Attack.kind : BossAction.NoAttack,
                Start = machine.IsAttacking ? machine.AttackStart : machine.StateStart,
            };
            if (!action.Value.Equals(next)) action.Value = next;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void DebugKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) TryBegin(BossAttackKind.PunchLeft);
            else if (keyboard.digit2Key.wasPressedThisFrame) TryBegin(BossAttackKind.PunchRight);
            else if (keyboard.digit3Key.wasPressedThisFrame) TryBegin(BossAttackKind.AlternatingCombo);
            else if (keyboard.digit4Key.wasPressedThisFrame) TryBegin(BossAttackKind.SlamBoth);
            else if (keyboard.digit5Key.wasPressedThisFrame) TryBegin(BossAttackKind.Charge);
        }
#endif
    }
}
