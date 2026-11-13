using System.Collections.Generic;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 스스로 싸우기(M2-04, 서버만 동작): 근접 거리 밖이면 걸어오고, 안이면 쉬는 시간 뒤 페이즈 표대로 기술을 고른다.
    /// HP 경계(60·25·10%)에서 페이즈 전환(공격이 끝난 뒤 포효 2초 무적, 조종사 HP 회복), 10%에서 무릎. 부스트에 맞으면 뒤로 밀려났다가 다시 걸어온다.
    /// 보스 무기가 부서지면(= 페이즈 전환) 하던 동작을 끊고 짧은 포효 뒤 보복 공격을 건다(대본 장면 5 돌진, 장면 7 검 내려치기).
    /// 돌진은 실제로 달려온다(<see cref="BossChargePath"/>): 물러나 자세 → 달려옴 → 안전한 쪽 반대편으로 스침 → 미끄러져 멈춤 → 근접 거리로 복귀.
    /// 위치·회전은 서버만 바꾸고 NetworkTransform이 복제한다. 사령관 대사 사건(글자·소리 자리)도 여기서 낸다.
    /// 보스 프리팹 루트에 메뉴가 붙인다. <see cref="BossServer"/>보다 먼저 돈다.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class BossBrain : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("보스 서버. 비어 있으면 같은 오브젝트에서 찾는다.")]
        BossServer server;

        [SerializeField, Tooltip("페이즈 순서표(05). 메뉴가 Data/Boss/BossPhaseTable.asset을 넣는다. 비면 05 기본값.")]
        BossPhaseTable table;

        [SerializeField, Tooltip("조종사 로봇 루트(다가갈 목표). 비어 있으면 시작할 때 조종석에서 찾는다.")]
        Transform player;

        [SerializeField, Tooltip("스스로 공격한다(패턴·페이즈). 끄면 M2-02 시험 공격(조절 메뉴 \"보스 시험\")으로 돌아간다.")]
        bool autoAttack = true;

        [Header("거리 (05 \"거리\")")]
        [SerializeField, Min(1f), Tooltip("근접 거리(m, 플레이어 루트 → 보스 루트). 끝까지 뻗은 주먹 + 판정만 허용 절반 + 보스 몸통 ≈ 30 m(M1-05 메모).")]
        float meleeDistance = 30f;
        [SerializeField, Min(0f), Tooltip("근접 거리보다 이만큼 더 멀어지면 다시 걸어온다(왔다 갔다 방지).")]
        float approachSlack = 4f;
        [SerializeField, Min(0.1f), Tooltip("걷는 속도(m/s). 65 m 로봇이 묵직하게.")]
        float walkSpeed = 6f;
        [SerializeField, Min(1f), Tooltip("조종사 쪽으로 도는 최대 속도(°/s).")]
        float turnDegreesPerSecond = 30f;

        [SerializeField, Range(0f, 15f), Tooltip("공격하는 동안(예고~회복) 이만큼(m) 뒤로 물러서서 친다. 30 m에서 상체를 숙이거나 팔을 뻗으면 우리 몸·조종석 카메라와 겹쳐 보인다(11/13 자동 점검). 쉬거나 빈틈이면 제자리(근접 거리)로 돌아와 우리 주먹이 닿는다. 0이면 끔.")]
        float attackStandOff = 7f;
        [SerializeField, Min(0.5f), Tooltip("물러서고 돌아오는 속도(m/s).")]
        float standOffSpeed = 7f;

        [Header("돌진 이동 (05 기술표 \"돌진\")")]
        [SerializeField, Tooltip("돌진 때 보스가 움직이는 길: 물러나 자세 잡기 → 달려옴 → 스쳐 지나감 → 미끄러져 멈춤 → 근접 거리로 복귀. 판 흐름이 걷기를 막은 장면(상실 장면의 보복)에서도 움직인다.")]
        BossChargeSettings charge = new BossChargeSettings();

        [Header("부스트 밀려남 (05 \"보스가 맞았을 때\")")]
        [SerializeField, Min(0f), Tooltip("뒤로 밀려나는 거리(m).")]
        float knockbackMeters = 10f;
        [SerializeField, Min(0.1f), Tooltip("밀려나는 시간(초, 밀려남 클립 길이에 맞춘다).")]
        float knockbackSeconds = 1.5f;

        [Header("첫 공격")]
        [SerializeField, Min(0f), Tooltip("판 시작(또는 재시작) 뒤 첫 공격까지(초).")]
        float firstAttackDelay = 4f;

        [Header("필살기·쓰러짐 (05 \"끝\")")]
        [SerializeField, Min(5f), Tooltip("무릎 뒤 이만큼(초) 필살기를 못 하면 다시 안내(05: 20).")]
        float finisherHintSeconds = 20f;
        [SerializeField, Min(0f), Tooltip("쓰러짐 뒤 승리 알림까지(초, Death 클립 길이에 맞춘다).")]
        float victoryDelay = 3f;
        [SerializeField, Min(0f), Tooltip("무릎 꿇으면 보스가 이 거리(m, 플레이어 루트 → 보스 루트)까지 뒤로 물러난다. 광선·폭발이 한눈에 보이게(선우 11/04). 0이면 그 자리.")]
        float finisherDistance = 90f;
        [SerializeField, Min(0.1f), Tooltip("물러나는 시간(초).")]
        float finisherRetreatSeconds = 2.5f;

        [Header("보스 무기·원거리 (M4-02)")]
        [SerializeField, Tooltip("보스 무기가 부서지면 보복 공격(무기 목록의 보복: 팔뚝 포 → 돌진, 방패 → 검 내려치기). 끄면 예전처럼 포효.")]
        bool retaliate = true;
        [SerializeField, Min(0f), Tooltip("무기가 부서진 뒤 보복 공격 예고가 시작될 때까지 뜸(초). 그동안 하던 동작(빈틈·휘청·밀려남)을 끊고 포효한다.")]
        float paybackDelay = 0.8f;
        [SerializeField, Range(0f, 0.5f), Tooltip("검 힘겨루기와 맞출 때: 보복 내려치기의 명중 순간을 힘겨루기 시작보다 이만큼(초) 늦춘다(검이 닿기 직전에 맞물려, 피해 판정 없이 힘겨루기로 넘어간다).")]
        float paybackClashLead = 0.15f;
        [SerializeField, Min(0.5f), Tooltip("접근 장면 주먹 포탄 사이 쉬는 시간 최소(초, 11 참고표 3.0).")]
        float rangedRestMin = 3f;
        [SerializeField, Min(0.5f), Tooltip("주먹 포탄 사이 쉬는 시간 최대(초, 4.0).")]
        float rangedRestMax = 4f;

        [Header("개발용")]
        [SerializeField, Tooltip("판마다 페이즈 시간·피해·가드 성공률 CSV(logs/bossfight_<날짜시각>.csv, 에디터·개발 빌드).")]
        bool writeCsv = true;

        BossPatternPicker picker;
        BossPhaseRules phases;
        BossFightStats stats = new BossFightStats();
        HashSet<BossAttackKind> seen = new HashSet<BossAttackKind>();
        bool bound, sawOpening, finishedLog;
        CombatAuthority boundAuthority;
        double nextAttackAt, nextRangedAt, fightStart, lastHint, wonAt = -1;
        Vector3 startPosition, knockFrom, knockTo;
        Quaternion startRotation;
        double knockStart = -1;
        double retreatStart = -1;
        Vector3 retreatFrom, retreatTo;
        int seed;
        // 보복(무기가 부서진 뒤 예약된 공격).
        bool paybackPending;
        BossAttackKind paybackKind;
        double paybackAt;
        /// <summary>예약한 보복을 이 시간(초) 안에 못 걸면(계속 휘청·밀려남) 버린다.</summary>
        const double PaybackGiveUpSeconds = 4.0;
        // 돌진 이동.
        bool chargeActive, chargeReturning;
        double chargeStart;
        float chargeTelegraph, chargeFrom;
        Vector3 chargeOrigin, chargeForward;
        int chargeSafe;
        bool saidIncoming;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CsvLog log;
#endif

        public BossPhaseRules Phases => phases;
        public int Phase => phases != null ? phases.Phase : 1;

        /// <summary>판 흐름(M4-01)이 켠다: 걷지 않는다(기체 점검·등장·드론·상실·재기동·결과 동안).</summary>
        public bool MovementHeld { get; set; }

        /// <summary>판 흐름(M4-01)이 켠다: 새 공격을 시작하지 않는다. 풀리면 첫 공격 지연 뒤부터 공격.</summary>
        public bool AttacksHeld { get; set; }

        /// <summary>조종사가 근접 거리 안에 있는지(접근 → 근접전 전환).</summary>
        public bool InMeleeRange => InReach();

        /// <summary>판 흐름(M4-02)이 접근 장면에서 켠다: 걸어오면서 주먹 포탄(펀치 클립 + 탄)을 쏜다. 근접 공격 잠금과 따로.</summary>
        public bool RangedAllowed { get; set; }

        /// <summary>
        /// 판 흐름(MatchFlow)이 이 보스를 이끈다(판 흐름이 매 프레임 켠다). 켜지면 장면 대사(무기 상실)와 상실 뒤 기체 HP 회복은 판 흐름이 맡고,
        /// 여기서는 같은 순간에 겹치는 대사·회복을 내지 않는다. 판 흐름이 없는 씬(샌드박스)에서는 예전처럼 여기서 낸다.
        /// </summary>
        public bool FlowDirected { get; set; }

        /// <summary>서버: 무기가 부서진 뒤 보복이 예약돼 아직 시작 전인지.</summary>
        public bool PaybackPending => paybackPending;

        float[] thresholds;

        /// <summary>보스 무기 목록(페이즈 표에 넣은 것). 없으면 null(기본안).</summary>
        public BossWeaponSet Weapons => table != null ? table.weaponSet : null;

        /// <summary>무기 이름(목록에서, 없으면 기본안 이름).</summary>
        public string WeaponName(BossWeapon weapon)
        {
            var w = Weapons != null ? Weapons.At((int)weapon) : null;
            return w != null && !string.IsNullOrEmpty(w.name) ? w.name : BossWeaponRules.Name(weapon);
        }

        /// <summary>페이즈 경계(%). 모든 PC가 같은 표에서 계산한다(보스 무기 표시용).</summary>
        public float[] Thresholds
        {
            get
            {
                if (thresholds == null) thresholds = table != null ? table.ActiveThresholds : BossWeaponRules.Thresholds(30f);
                return thresholds;
            }
        }

        /// <summary>조종사(루트)에서 몸을 잰 거리. 조종사를 모르면 -1.</summary>
        public float DistanceToPilot
        {
            get
            {
                if (player == null) return -1f;
                var to = player.position - transform.position;
                to.y = 0f;
                return to.magnitude;
            }
        }

        /// <summary>서버: 검 힘겨루기 시작. 보스가 검을 내리누른 채 버틴다(끝은 <see cref="EndClash"/>).</summary>
        public void BeginClash(double seconds)
        {
            if (server == null) return;
            // 힘겨루기가 보복 내려치기를 받아 낸다: 아직 못 건 보복은 버리고, 진행 중인 공격은 Hold가 끊는다(의도).
            paybackPending = false;
            if (chargeActive) chargeReturning = true;
            server.Machine.Hold(BossState.Clash, server.Now, seconds);
        }

        /// <summary>
        /// 서버: 예약된 보복의 명중 순간을 이 서버 시각에 맞춘다(판 흐름이 검 힘겨루기 장면에 들어갈 때 부른다).
        /// 내려치는 검이 닿기 직전에 힘겨루기가 맞물리게 예고 시작을 앞뒤로 옮긴다. 예약된 보복이 없으면 아무 일도 없다.
        /// </summary>
        public void AlignPayback(double impactAt)
        {
            if (!paybackPending || server == null) return;
            var now = server.Now;
            paybackAt = System.Math.Max(now, impactAt + paybackClashLead - server.SpecOf(paybackKind).telegraphSeconds);
            // 포효 길이도 새 시작 시각에 맞춘다(끝나면 바로 보복 예고).
            if (server.Machine.State == BossState.Roar) server.Machine.Hold(BossState.Roar, now, paybackAt - now);
        }

        /// <summary>서버: 검 힘겨루기 끝. 밀어냈으면 보스가 크게 휘청이고 빈틈이 이어진다.</summary>
        public void EndClash(bool pushedBack, float staggerSeconds)
        {
            if (server == null) return;
            var now = server.Now;
            if (server.Machine.State == BossState.Clash) server.Machine.Release(now);
            if (pushedBack) server.Machine.Stagger(staggerSeconds, 0f, now, null);
        }

        /// <summary>서버: 조종사에서 meters 떨어진 곳으로 옮기고 조종사를 보게 한다(장면 2 마당 끝 120 m, M4-02). 방향은 지금 보스가 있는 쪽.</summary>
        public void PlaceAtDistance(float meters)
        {
            if (player == null) return;
            var away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = player.forward;
            var position = player.position + away.normalized * meters;
            position.y = transform.position.y;
            transform.position = position;
            var look = player.position - position;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            knockStart = -1;
            standOff = 0f;
            chargeActive = false;
            paybackPending = false;
            if (server != null && server.Machine.State == BossState.Walk) server.Machine.Release(server.Now);
        }

        void Start()
        {
            if (server == null) server = GetComponent<BossServer>();
            if (player == null)
            {
                var seat = FindAnyObjectByType<CockpitSeat>();
                if (seat != null) player = seat.transform.root;
            }
            startPosition = transform.position;
            startRotation = transform.rotation;
            if (server == null) enabled = false;
        }

        void OnDestroy()
        {
            if (bound && server != null && server.Combat != null) server.Combat.ServerCue -= OnCue;
            if (server != null)
            {
                server.ServerEvent -= OnBossEvent;
                server.ServerReset -= OnReset;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WriteLog("quit");
            log?.Dispose();
#endif
        }

        void Unbind()
        {
            if (server.Combat != null) server.Combat.ServerCue -= OnCue;
            server.ServerEvent -= OnBossEvent;
            server.ServerReset -= OnReset;
            bound = false;
        }

        bool TryBind()
        {
            if (!server.IsServer || server.Combat == null || server.Combat.Authority == null) return false;
            if (stats == null) stats = new BossFightStats();
            if (seen == null) seen = new HashSet<BossAttackKind>();
            var phaseTable = table != null ? table.phases : null;
            seed = table != null && table.seed != 0 ? table.seed : System.Environment.TickCount;
            picker = new BossPatternPicker(phaseTable, seed);
            phases = new BossPhaseRules(Thresholds, table != null ? table.roarSeconds : 2f);
            server.Combat.ServerCue += OnCue;
            server.ServerEvent += OnBossEvent;
            server.ServerReset += OnReset;
            server.RestIsOpening = !autoAttack;
            server.TestAttacksBlocked = autoAttack;
            bound = true;
            boundAuthority = server.Combat.Authority;
            BeginRun();
            return true;
        }

        float standOff;

        void BeginRun()
        {
            standOff = 0f;
            var now = server.Now;
            nextAttackAt = now + firstAttackDelay;
            seen.Clear();
            sawOpening = finishedLog = saidIncoming = false;
            paybackPending = false;
            chargeActive = chargeReturning = false;
            knockStart = -1;
            retreatStart = -1;
            wonAt = -1;
            fightStart = now;
            stats.Begin(now);
        }

        void Update()
        {
            // Play 중 스크립트가 다시 컴파일되면(예: Play 중 git pull) 직렬화 안 되는 규칙 객체만 비고 bound는 남는다 → 다시 묶는다.
            if (bound && (phases == null || picker == null)) Unbind();
            // 세션을 다시 시작하면(M3-01 다시 고르기·호스트 재시작) 전투 서버가 새 판정 객체를 만든다 → 새것에 다시 묶고 판을 처음부터.
            if (bound && server.Combat != null && server.Combat.Authority != boundAuthority) Unbind();
            if (!bound && !TryBind()) return;
            if (server.Combat == null || server.Combat.Authority == null) return;
            var now = server.Now;
            var machine = server.Machine;
            var authority = server.Combat.Authority;
            server.RestIsOpening = !autoAttack;
            server.TestAttacksBlocked = autoAttack;
            if (machine.State == BossState.Defeated)
            {
                if (wonAt >= 0 && now >= wonAt)
                {
                    wonAt = -1;
                    Emit(CombatCue.EncounterWon, 0, now, (float)(now - fightStart));
                    Emit(CombatCue.CommanderLine, CommanderLines.Victory, now);
                    WriteLog("won");
                    server.Combat.Flush();
                }
                return;
            }

            // 페이즈 바닥: 경계 아래로 한 번에 못 내려간다.
            authority.BossPhase = phases.Kneeling ? 4 : phases.Phase;
            var state = machine.State;
            authority.BossGrabbable = state == BossState.Staggered || state == BossState.Opening || state == BossState.Grabbed;
            // 잡고 베기 안내: 휘청일 때 한 판에 두 번까지 알려 준다.
            if (state == BossState.Staggered && grabHints < 2 && authority.Grab.Available(now, true) && now - lastGrabHint > 20.0)
            {
                grabHints++;
                lastGrabHint = now;
                Emit(CombatCue.CommanderLine, CommanderLines.GrabHint, now);
            }
            var boss = authority.Resolver.Boss;
            boss.Floor = boss.Max * phases.FloorPercent / 100f;
            var hp = boss.Max > 0f ? boss.Current / boss.Max * 100f : 0f;
            switch (phases.Update(now, hp, machine.IsAttacking || machine.State == BossState.Staggered))
            {
                case PhaseStep.PhaseChanged:
                    // M4-02: 무기를 잃은 보스의 보복(무기 목록에서: 기본 팔뚝 포 → 돌진, 둘째 → 큰 내려찍기).
                    // 무기는 늘 빈틈·휘청·밀려남 중에 부서지므로 바로 시작할 수 없다 → 하던 동작을 끊고(Hold) 짧게 포효한 뒤 보복을 건다(TickPayback).
                    var broken = Weapons != null ? Weapons.At(phases.Phase - 2) : null;
                    var payback = broken != null ? broken.retaliation : phases.Phase == 2 ? BossAttackKind.Charge : BossAttackKind.SlamBoth;
                    var doPayback = retaliate && (broken == null || broken.retaliate);
                    var roar = table != null ? table.roarSeconds : 2f;
                    if (doPayback)
                    {
                        paybackKind = payback;
                        paybackPending = true;
                        paybackAt = now + paybackDelay;
                        machine.Hold(BossState.Roar, now, paybackDelay);
                    }
                    else machine.Hold(BossState.Roar, now, roar);
                    picker.BeginPhase(phases.Phase);
                    authority.Grab.NewPhase();
                    // 판 흐름이 있으면 회복은 상실 장면이 끝날 때 한 번(보복에 깎인 것까지 채움), 상실 대사도 판 흐름이 낸다.
                    if (!FlowDirected)
                    {
                        var restore = table != null ? table.pilotRestorePercent : 60f;
                        if (restore > 0f) authority.Pilot.RestoreTo(restore);
                    }
                    Emit(CombatCue.PhaseChanged, phases.Phase, now);
                    if (!FlowDirected) Emit(CombatCue.CommanderLine, phases.Phase == 2 ? CommanderLines.Phase2 : CommanderLines.Phase3, now);
                    nextAttackAt = now + roar + 0.5f;
                    stats.EnterPhase(phases.Phase, now);
                    break;
                case PhaseStep.Kneel:
                    paybackPending = false;
                    machine.Hold(BossState.Kneel, now);
                    Emit(CombatCue.BossKneel, 0, now);
                    Emit(CombatCue.CommanderLine, CommanderLines.KneelFinisher, now);
                    stats.EnterPhase(4, now);
                    authority.Finisher.Enabled = true;
                    lastHint = now;
                    BeginRetreat(now);
                    break;
            }
            if (phases.Kneeling) Finisher(now, authority);

            // 밀려남·돌진은 판 흐름이 걷기를 막은 장면에서도 이어진다(상실 장면의 보복 돌진).
            if (!ForcedMove(now, machine))
            {
                if (!MovementHeld) Move(now, machine);
                else if (machine.State == BossState.Walk) machine.Release(now);
            }
            if (AttacksHeld) nextAttackAt = now + firstAttackDelay;
            if (paybackPending) TickPayback(now, machine, authority);
            if (RangedAllowed) Ranged(now, machine, authority);
            else nextRangedAt = now + firstAttackDelay;

            // 보복이 예약된 동안은 다른 공격이 끼어들지 않는다(보복이 시작되면 상태기계가 막는다).
            if (autoAttack && !AttacksHeld && !paybackPending && machine.IsResting && now >= nextAttackAt && !authority.Pilot.IsDefeated && InReach())
            {
                var pattern = picker.NextPattern(BossWeaponRules.CrackCount(hp, Thresholds));
                if (server.TryBeginChain(pattern))
                {
                    stats.AttackStarted();
                    foreach (var kind in pattern)
                        if (seen.Add(kind)) FirstSight(kind, now);
                }
            }
            server.Combat.Flush();
        }

        /// <summary>
        /// 예약된 보복: 뜸이 지나고 보스가 쉬는 중이 되면 바로 건다. 판 흐름이 근접 공격을 막은 장면(상실)에서도 나간다.
        /// 계속 못 걸면(다시 휘청·밀려남) 조금 기다리다 버린다.
        /// </summary>
        void TickPayback(double now, BossAttackStateMachine machine, CombatAuthority authority)
        {
            if (authority.Pilot.IsDefeated || machine.State == BossState.Defeated)
            {
                paybackPending = false;
                return;
            }
            if (now < paybackAt) return;
            // 포효·걷기는 끊고 바로 시작(다른 상태는 끝나기를 기다린다).
            if (machine.State == BossState.Roar || machine.State == BossState.Walk) machine.Release(now);
            if (!machine.IsResting)
            {
                if (now > paybackAt + PaybackGiveUpSeconds) paybackPending = false;
                return;
            }
            paybackPending = false;
            if (server.TryBegin(paybackKind)) stats.AttackStarted();
        }

        /// <summary>접근 장면: 걷다가 멈춰 주먹 포탄 한 발(좌/우 펀치 기술 그대로, 판정은 같은 막기). 쏘고 나면 다시 걷는다.</summary>
        void Ranged(double now, BossAttackStateMachine machine, CombatAuthority authority)
        {
            if (paybackPending || now < nextRangedAt || authority.Pilot.IsDefeated || InReach()) return;
            if (machine.State != BossState.Walk && !machine.IsResting) return;
            if (machine.State == BossState.Walk) machine.Release(now);
            var kind = Random.value < 0.5f ? BossAttackKind.PunchLeft : BossAttackKind.PunchRight;
            if (server.TryBegin(kind))
            {
                stats.AttackStarted();
                // 대사 7 "포탄이 온다": 접근 장면의 첫 포탄에(대본 장면 4).
                if (!saidIncoming)
                {
                    saidIncoming = true;
                    Emit(CombatCue.CommanderLine, CommanderLines.IncomingAttack, now);
                }
            }
            nextRangedAt = now + Random.Range(Mathf.Min(rangedRestMin, rangedRestMax), Mathf.Max(rangedRestMin, rangedRestMax));
        }

        /// <summary>무릎 뒤: 필살기 충전·발사·쓰러짐(M2-05). 20초마다 못 하면 다시 안내.</summary>
        void Finisher(double now, CombatAuthority authority)
        {
            var finisher = authority.Finisher;
            switch (finisher.Update(now))
            {
                case FinisherStep.ChargeStarted:
                    Emit(CombatCue.FinisherCharge, 0, now, finisher.Settings.chargeSeconds);
                    break;
                case FinisherStep.ChargeReset:
                    Emit(CombatCue.FinisherReset, 0, now);
                    break;
                case FinisherStep.Fired:
                    Emit(CombatCue.FinisherFire, 0, now, finisher.Settings.beamSeconds);
                    break;
                case FinisherStep.Finished:
                    // 바닥을 풀고 남은 HP를 모두 깎는다 → 쓰러짐.
                    phases.ReleaseFloor();
                    var boss = authority.Resolver.Boss;
                    boss.Floor = 0f;
                    var damage = boss.Apply(100f);
                    server.Combat.Events.Add(new CombatEvent
                    {
                        Cue = CombatCue.BossDefeated, Kind = PunchKind.None, Time = now, Damage = damage, BossHealth = boss.Current, Point = transform.position,
                    });
                    server.Machine.Defeat(now, null);
                    stats.EnterPhase(4, now);
                    wonAt = now + victoryDelay;
                    return;
            }
            if (finisher.Charging || finisher.Firing) lastHint = now;
            else if (now - lastHint >= finisherHintSeconds)
            {
                lastHint = now;
                Emit(CombatCue.CommanderLine, CommanderLines.FinisherHint, now);
            }
        }

        /// <summary>
        /// 판 흐름이 걷기를 막아도 이어지는 이동: 부스트 밀려남(무기가 부서져 포효로 끊겨도 끝까지 미끄러진다)과 돌진.
        /// 움직였으면 true(그 프레임엔 걷기·물러서기를 하지 않는다).
        /// </summary>
        bool ForcedMove(double now, BossAttackStateMachine machine)
        {
            if (knockStart >= 0)
            {
                chargeActive = false;
                var t = Mathf.Clamp01((float)((now - knockStart) / knockbackSeconds));
                // 처음에 크게, 끝에 묵직하게 멈춘다.
                transform.position = Vector3.Lerp(knockFrom, knockTo, 1f - (1f - t) * (1f - t));
                if (t >= 1f) knockStart = -1;
                standOff = 0f;
                return true;
            }
            return ChargeMove(now, machine);
        }

        /// <summary>
        /// 돌진 이동: 예고 동안 물러나 자세를 잡고, 끝에 달려와 안전한 쪽(서버가 정함) 반대편으로 스치고, 미끄러져 멈춘 뒤 근접 거리로 돌아온다.
        /// 돌아오는 동안 휘청·빈틈이 이어지므로 우리 주먹이 닿는 자리로 온다.
        /// </summary>
        bool ChargeMove(double now, BossAttackStateMachine machine)
        {
            if (player == null) return false;
            var charging = machine.IsAttacking && machine.HasAttack && machine.Attack.kind == BossAttackKind.Charge;
            if (charging && (!chargeActive || machine.AttackStart != chargeStart)) BeginCharge(machine);
            if (!chargeActive) return false;
            if (retreatStart >= 0 || machine.State == BossState.Defeated)
            {
                chargeActive = false;
                return false;
            }
            var dt = Time.deltaTime;
            var t = (float)(now - chargeStart);
            if (!charging || t > BossChargePath.EndSeconds(charge, chargeTelegraph)) chargeReturning = true;
            if (!chargeReturning)
            {
                var p = BossChargePath.At(charge, chargeFrom, meleeDistance, chargeTelegraph, t, chargeSafe);
                var right = Vector3.Cross(Vector3.up, chargeForward);
                var position = chargeOrigin + chargeForward * p.y + right * p.x;
                position.y = transform.position.y;
                transform.position = position;
            }
            else
            {
                var home = chargeOrigin + chargeForward * meleeDistance;
                home.y = transform.position.y;
                transform.position = Vector3.MoveTowards(transform.position, home, charge.returnSpeed * dt);
                if ((transform.position - home).sqrMagnitude < 0.01f) chargeActive = false;
            }
            var to = player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
            {
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, charge.turnDegreesPerSecond * dt);
            }
            return true;
        }

        void BeginCharge(BossAttackStateMachine machine)
        {
            chargeActive = true;
            chargeReturning = false;
            chargeStart = machine.AttackStart;
            chargeTelegraph = machine.Attack.telegraphSeconds;
            chargeOrigin = player.position;
            var away = transform.position - chargeOrigin;
            away.y = 0f;
            chargeFrom = away.magnitude;
            chargeForward = chargeFrom > 0.01f ? away / chargeFrom : -transform.forward;
            chargeSafe = server.ChargeSafeSide == PilotSide.Left ? -1 : 1;
            // 물러서 있던 만큼(다른 공격의 물러서기)은 시작 위치에 이미 들어 있다.
            standOff = 0f;
        }

        void Move(double now, BossAttackStateMachine machine)
        {
            var dt = Time.deltaTime;
            if (retreatStart >= 0)
            {
                var r = Mathf.Clamp01((float)((now - retreatStart) / finisherRetreatSeconds));
                // 처음에 크게, 끝에 묵직하게 멈춘다(밀려남과 같은 곡선).
                transform.position = Vector3.Lerp(retreatFrom, retreatTo, 1f - (1f - r) * (1f - r));
                if (r >= 1f) retreatStart = -1;
                standOff = 0f;
                return;
            }
            if (player != null)
            {
                // 예고·명중 동안만 조금 물러서고, 회복부터 제자리로(빈틈에 우리 주먹이 닿게. 위치에 더했다 뺀다: 걷기·회전 계산과 따로).
                var want = machine.State == BossState.Telegraph || machine.State == BossState.Strike ? attackStandOff : 0f;
                var next = Mathf.MoveTowards(standOff, want, standOffSpeed * dt);
                if (!Mathf.Approximately(next, standOff))
                {
                    var away = transform.position - player.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 1e-4f) transform.position += away.normalized * (next - standOff);
                    standOff = next;
                }
            }
            if (player == null || (!machine.IsResting && machine.State != BossState.Walk)) return;

            var to = player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
            {
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnDegreesPerSecond * dt);
            }
            var distance = to.magnitude;
            if (machine.State == BossState.Walk)
            {
                if (distance <= meleeDistance)
                {
                    machine.Release(now);
                    nextAttackAt = System.Math.Max(nextAttackAt, now + 0.8);
                    return;
                }
                var step = Mathf.Min(walkSpeed * dt, distance - meleeDistance);
                transform.position += to.normalized * step;
            }
            else if (distance > meleeDistance + approachSlack && autoAttack && standOff < 0.05f)
            {
                machine.Hold(BossState.Walk, now);
            }
        }

        bool InReach()
        {
            if (player == null) return true;
            var to = player.position - transform.position;
            to.y = 0f;
            return to.magnitude <= meleeDistance + approachSlack;
        }

        void FirstSight(BossAttackKind kind, double now)
        {
            // 대사 7 "포탄이 온다"는 접근 장면 첫 포탄에 낸다(Ranged). 판 흐름이 없는 씬에서만 예전처럼 첫 공격에.
            if (seen.Count == 1 && !FlowDirected && !saidIncoming)
            {
                saidIncoming = true;
                Emit(CombatCue.CommanderLine, CommanderLines.IncomingAttack, now);
            }
            switch (kind)
            {
                case BossAttackKind.SlamBoth: Emit(CombatCue.CommanderLine, CommanderLines.FirstSlam, now); break;
                case BossAttackKind.AlternatingCombo: Emit(CombatCue.CommanderLine, CommanderLines.FirstCombo, now); break;
                case BossAttackKind.Charge: Emit(CombatCue.CommanderLine, CommanderLines.FirstCharge, now); break;
            }
        }

        void OnBossEvent(BossEvent e)
        {
            switch (e.Type)
            {
                case BossEventType.OpeningStarted:
                    if (!sawOpening)
                    {
                        sawOpening = true;
                        Emit(CombatCue.CommanderLine, CommanderLines.CounterNow, e.Time);
                    }
                    break;
                case BossEventType.OpeningEnded:
                    if (picker != null) nextAttackAt = e.Time + picker.RestSeconds(CurrentCracks());
                    break;
            }
        }

        /// <summary>지금 차례 보스 무기에 간 금 수(0~2).</summary>
        int CurrentCracks()
        {
            if (server == null || server.Combat == null || server.Combat.Authority == null) return 0;
            var boss = server.Combat.Authority.Resolver.Boss;
            var hp = boss.Max > 0f ? boss.Current / boss.Max * 100f : 0f;
            return BossWeaponRules.CrackCount(hp, Thresholds);
        }

        void OnCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.Boost:
                    Knockback(e.Time);
                    break;
                case CombatCue.GuardBlock:
                case CombatCue.Dodge:
                    stats.Strike(true);
                    break;
                case CombatCue.GrabStart:
                    // 붙잡힘: 버틸 수 있는 시간만큼 그 자리에서 버둥거린다(시간이 지나면 규칙이 GrabBreak를 낸다).
                    server.Machine.Hold(BossState.Grabbed, e.Time, e.Amount + 0.5f);
                    Emit(CombatCue.CommanderLine, e.Code == 1 ? CommanderLines.GrabHurry : CommanderLines.GrabHeld, e.Time);
                    break;
                case CombatCue.GrabSlash:
                    server.Machine.Release(e.Time);
                    server.Machine.Stagger(e.Amount, server.Combat.Authority.Grab.Settings.openingAfter, e.Time, null);
                    break;
                case CombatCue.GrabBreak:
                    if (server.Machine.State == BossState.Grabbed) server.Machine.Release(e.Time);
                    // 무기가 부서져 포효로 끊긴 잡기: 상실 대사와 겹치지 않게 "놓쳤다"는 내지 않는다.
                    if (!paybackPending) Emit(CombatCue.CommanderLine, CommanderLines.GrabLost, e.Time);
                    // 뿌리친 보스는 바로 되갚는다(쉬는 시간 없이 다음 공격).
                    if (e.Amount >= 1f) nextAttackAt = e.Time + 0.3;
                    break;
                case CombatCue.PilotHit:
                    stats.Strike(false);
                    stats.PilotDamage(e.Damage);
                    break;
            }
        }

        int grabHints;
        double lastGrabHint = double.NegativeInfinity;

        void Knockback(double now)
        {
            var machine = server.Machine;
            if (machine.State == BossState.Defeated || machine.State == BossState.Kneel || knockbackMeters <= 0f) return;
            var away = transform.position - (player != null ? player.position : transform.position - transform.forward);
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            machine.Hold(BossState.KnockedBack, now, knockbackSeconds);
            chargeActive = false;
            knockFrom = transform.position;
            knockTo = knockFrom + away.normalized * knockbackMeters;
            knockStart = now;
            Emit(CombatCue.BossKnockback, 0, now, knockbackMeters);
        }

        /// <summary>서버: 지금 밀려나는 중이면 더 멀리(맵 연출 사일로 처박기, M4-07).</summary>
        public void PushFurther(float extraMeters)
        {
            if (knockStart < 0 || extraMeters <= 0f) return;
            var away = knockTo - knockFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) return;
            knockTo += away.normalized * extraMeters;
        }

        /// <summary>서버: 경직(크레인 코일에 맞음·컨베이어 다리에 박힘, M4-07). 끝나면 빈틈.</summary>
        public void Stun(float seconds, float openingAfter)
        {
            if (server == null) return;
            server.Machine.Stagger(seconds, openingAfter, server.Now, null);
        }

        /// <summary>무릎: 필살기 광선이 잘 보이게 플레이어에게서 멀어진다(이미 멀면 그대로).</summary>
        void BeginRetreat(double now)
        {
            if (player == null || finisherDistance <= 0f) return;
            var away = transform.position - player.position;
            away.y = 0f;
            if (away.magnitude >= finisherDistance) return;
            if (away.sqrMagnitude < 1e-4f) away = transform.forward;
            retreatFrom = transform.position;
            retreatTo = player.position + away.normalized * finisherDistance;
            retreatTo.y = retreatFrom.y;
            retreatStart = now;
            knockStart = -1;
            chargeActive = false;
        }

        void OnReset()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WriteLog("restart");
#endif
            grabHints = 0;
            lastGrabHint = double.NegativeInfinity;
            picker?.Reset();
            phases?.Reset();
            transform.SetPositionAndRotation(startPosition, startRotation);
            BeginRun();
        }

        void Emit(CombatCue cue, int code, double time, float amount = 0f)
        {
            var combat = server.Combat;
            combat.Events.Add(new CombatEvent
            {
                Cue = cue, Kind = PunchKind.None, Code = code, Amount = amount, Time = time, Point = transform.position,
                BossHealth = combat.Authority.Resolver.Boss.Current,
            });
        }

        void WriteLog(string result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!writeCsv || finishedLog || !stats.Running || server == null) return;
            var line = stats.Finish(server.Now, seed, result);
            finishedLog = result == "won";
            if (line == null) return;
            if (log == null) log = new CsvLog(CsvLog.DefaultPath(LogFolder.Root, "bossfight", System.DateTime.Now), BossFightStats.Header);
            log.Add(line);
            log.Pump();
            Debug.Log($"[BossBrain] 한 판 기록: {line}", this);
#endif
        }

        /// <summary>인게임 조절 메뉴: 보스 스스로 공격 켜고 끄기(끄면 M2-02 시험 공격).</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "보스";
            list.Describe(g, "할 것: 1페이즈부터 무릎까지 혼자 진행\n볼 것: 페이즈마다 새 기술이 먼저, 밀려난 뒤 다시 걸어옴");
            list.AddToggle(g, "boss.auto", "보스 스스로 공격(끄면 \"보스 시험\" 공격)", () => autoAttack, v => autoAttack = v);
            list.AddFloat(g, "boss.walkSpeed", "걷는 속도(m/s)", () => walkSpeed, v => walkSpeed = v, 1f, 20f, 0.5f, 1);
            list.AddFloat(g, "boss.finisherDistance", "필살기 때 물러나는 거리(m, 0 = 그 자리)", () => finisherDistance, v => finisherDistance = v, 0f, 200f, 10f, 0);
            list.AddFloat(g, "boss.paybackDelay", "무기 부서진 뒤 보복까지 뜸(초)", () => paybackDelay, v => paybackDelay = v, 0f, 3f, 0.1f, 1);
            list.AddFloat(g, "boss.chargeClose", "돌진: 가장 가까이 오는 거리(m)", () => charge.closeDistance, v => charge.closeDistance = v, 8f, 30f, 1f, 0);
            list.AddFloat(g, "boss.chargeSide", "돌진: 옆으로 비켜 스치는 거리(m)", () => charge.sideOffset, v => charge.sideOffset = v, 0f, 25f, 1f, 0);
            list.AddFloat(g, "boss.chargeRun", "돌진: 달려오는 시간(초, 짧을수록 빠름)", () => charge.runSeconds, v => charge.runSeconds = v, 0.3f, 2f, 0.1f, 1);
        }
    }
}
