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

        [SerializeField, Tooltip("스스로 공격한다. 끄면 M2-02처럼 숫자 키로만 공격(쉬는 중에도 맞음).")]
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

        [Header("부스트 밀려남 (05 \"보스가 맞았을 때\")")]
        [SerializeField, Min(0f), Tooltip("뒤로 밀려나는 거리(m).")]
        float knockbackMeters = 10f;
        [SerializeField, Min(0.1f), Tooltip("밀려나는 시간(초, 밀려남 클립 길이에 맞춘다).")]
        float knockbackSeconds = 1.5f;

        [Header("첫 공격")]
        [SerializeField, Min(0f), Tooltip("판 시작(또는 재시작) 뒤 첫 공격까지(초).")]
        float firstAttackDelay = 4f;

        [Header("개발용")]
        [SerializeField, Tooltip("판마다 페이즈 시간·피해·가드 성공률 CSV(logs/bossfight_<날짜시각>.csv, 에디터·개발 빌드).")]
        bool writeCsv = true;

        BossPatternPicker picker;
        BossPhaseRules phases;
        readonly BossFightStats stats = new BossFightStats();
        readonly HashSet<BossAttackKind> seen = new HashSet<BossAttackKind>();
        bool bound, sawOpening, finishedLog;
        double nextAttackAt;
        Vector3 startPosition, knockFrom, knockTo;
        Quaternion startRotation;
        double knockStart = -1;
        int seed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CsvLog log;
#endif

        public BossPhaseRules Phases => phases;
        public int Phase => phases != null ? phases.Phase : 1;

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

        bool TryBind()
        {
            if (!server.IsServer || server.Combat == null || server.Combat.Authority == null) return false;
            var phaseTable = table != null ? table.phases : null;
            seed = table != null && table.seed != 0 ? table.seed : System.Environment.TickCount;
            picker = new BossPatternPicker(phaseTable, seed);
            phases = new BossPhaseRules(table != null ? table.thresholds : null, table != null ? table.roarSeconds : 2f);
            server.Combat.ServerCue += OnCue;
            server.ServerEvent += OnBossEvent;
            server.ServerReset += OnReset;
            server.RestIsOpening = !autoAttack;
            bound = true;
            BeginRun();
            return true;
        }

        void BeginRun()
        {
            var now = server.Now;
            nextAttackAt = now + firstAttackDelay;
            seen.Clear();
            sawOpening = finishedLog = false;
            knockStart = -1;
            stats.Begin(now);
        }

        void Update()
        {
            if (!bound && !TryBind()) return;
            var now = server.Now;
            var machine = server.Machine;
            var authority = server.Combat.Authority;
            server.RestIsOpening = !autoAttack;
            if (machine.State == BossState.Defeated)
            {
                WriteLog("won");
                return;
            }

            // 페이즈 바닥: 경계 아래로 한 번에 못 내려간다.
            authority.BossPhase = phases.Kneeling ? 4 : phases.Phase;
            var boss = authority.Resolver.Boss;
            boss.Floor = boss.Max * phases.FloorPercent / 100f;
            var hp = boss.Max > 0f ? boss.Current / boss.Max * 100f : 0f;
            switch (phases.Update(now, hp, machine.IsAttacking || machine.State == BossState.Staggered))
            {
                case PhaseStep.PhaseChanged:
                    machine.Hold(BossState.Roar, now, table != null ? table.roarSeconds : 2f);
                    picker.BeginPhase(phases.Phase);
                    var restore = table != null ? table.pilotRestorePercent : 60f;
                    if (restore > 0f) authority.Pilot.RestoreTo(restore);
                    Emit(CombatCue.PhaseChanged, phases.Phase, now);
                    Emit(CombatCue.CommanderLine, phases.Phase == 2 ? CommanderLines.Phase2 : CommanderLines.Phase3, now);
                    nextAttackAt = now + (table != null ? table.roarSeconds : 2f) + 0.5f;
                    stats.EnterPhase(phases.Phase, now);
                    break;
                case PhaseStep.Kneel:
                    machine.Hold(BossState.Kneel, now);
                    Emit(CombatCue.BossKneel, 0, now);
                    Emit(CombatCue.CommanderLine, CommanderLines.KneelFinisher, now);
                    stats.EnterPhase(4, now);
                    break;
            }

            Move(now, machine);

            if (autoAttack && machine.IsResting && now >= nextAttackAt && !authority.Pilot.IsDefeated && InReach())
            {
                var kind = picker.Next();
                if (server.TryBegin(kind))
                {
                    stats.AttackStarted();
                    if (seen.Add(kind)) FirstSight(kind, now);
                }
            }
            server.Combat.Flush();
        }

        void Move(double now, BossAttackStateMachine machine)
        {
            var dt = Time.deltaTime;
            if (machine.State == BossState.KnockedBack && knockStart >= 0)
            {
                var t = Mathf.Clamp01((float)((now - knockStart) / knockbackSeconds));
                // 처음에 크게, 끝에 묵직하게 멈춘다.
                transform.position = Vector3.Lerp(knockFrom, knockTo, 1f - (1f - t) * (1f - t));
                if (t >= 1f) knockStart = -1;
                return;
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
            else if (distance > meleeDistance + approachSlack && autoAttack)
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
            if (seen.Count == 1) Emit(CombatCue.CommanderLine, CommanderLines.IncomingAttack, now);
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
                    if (picker != null) nextAttackAt = e.Time + picker.RestSeconds();
                    break;
            }
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
                case CombatCue.PilotHit:
                    stats.Strike(false);
                    stats.PilotDamage(e.Damage);
                    break;
            }
        }

        void Knockback(double now)
        {
            var machine = server.Machine;
            if (machine.State == BossState.Defeated || machine.State == BossState.Kneel || knockbackMeters <= 0f) return;
            var away = transform.position - (player != null ? player.position : transform.position - transform.forward);
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            machine.Hold(BossState.KnockedBack, now, knockbackSeconds);
            knockFrom = transform.position;
            knockTo = knockFrom + away.normalized * knockbackMeters;
            knockStart = now;
            Emit(CombatCue.BossKnockback, 0, now, knockbackMeters);
        }

        void OnReset()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WriteLog("restart");
#endif
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

        /// <summary>인게임 조절 메뉴: 보스 스스로 공격 켜고 끄기(끄면 숫자 키 시험).</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "보스";
            list.Describe(g, "할 것: 1페이즈부터 무릎까지 혼자 진행\n볼 것: 페이즈마다 새 기술이 먼저, 밀려난 뒤 다시 걸어옴");
            list.AddToggle(g, "boss.auto", "보스 스스로 공격(끄면 숫자 키 1~5)", () => autoAttack, v => autoAttack = v);
            list.AddFloat(g, "boss.walkSpeed", "걷는 속도(m/s)", () => walkSpeed, v => walkSpeed = v, 1f, 20f, 0.5f, 1);
        }
    }
}
