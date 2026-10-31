using System;
using System.Collections.Generic;
using Anseong.Combat;
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
    /// 시험용: 숫자 1~5로 기술 하나(에디터·개발 빌드). 기술 고르기·페이즈는 M2-04.
    /// 보스 프리팹 루트(NetworkObject와 함께)에 메뉴가 붙인다.
    /// </summary>
    public class BossServer : NetworkBehaviour
    {
        [SerializeField, Tooltip("전투 서버. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatServer combat;

        [SerializeField, Tooltip("기술 에셋(05 기술표). 메뉴가 Data/Boss의 5개를 넣는다.")]
        BossAttackDefinition[] attacks = new BossAttackDefinition[0];

        [SerializeField, Tooltip("쉬는 중에도 피해를 받는다. 기술을 스스로 고르기 전(M2-02·03)에는 켜 두고, 패턴(M2-04)이 들어오면 끈다(05: 빈틈에만 피해).")]
        bool restIsOpening = true;

        [SerializeField, Min(0f), Tooltip("빈틈이 끝나도 연계가 이어지는 중이면 다음 공격을 이만큼까지 미룬다(05: 2.4초).")]
        float comboHoldSeconds = 2.4f;

        [SerializeField, Tooltip("시험용 숫자 키 1~5로 기술 하나(왼손 펀치, 오른손 펀치, 좌우 연타, 내려찍기, 돌진). 에디터·개발 빌드만.")]
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
            if (!IsServer || !bound) return false;
            if (!machine.Begin(SpecOf(kind), Now, bossEvents)) return false;
            Publish();
            return true;
        }

        /// <summary>서버: 지금 피해를 받는지.</summary>
        public bool Vulnerable() => machine.CanBeDamaged || (restIsOpening && machine.IsResting);

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
            Publish();
        }

        bool TryBind()
        {
            if (combat == null || combat.Authority == null) return false;
            combat.Authority.Resolver.BossVulnerable = Vulnerable;
            combat.ServerReset += OnReset;
            bound = true;
            return true;
        }

        void OnImmediate(BossEvent e) => ServerEvent?.Invoke(e);

        void OnReset()
        {
            machine.Reset();
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
                        break;
                    case BossEventType.Impact:
                        cue.Cue = CombatCue.BossStrike;
                        cue.Amount = e.Hit;
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
