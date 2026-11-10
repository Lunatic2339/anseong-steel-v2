using System;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Net;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>복제되는 판 상태: 지금 장면, 시작 시각(서버 시계), 결과판 숫자.</summary>
    public struct MatchFlowState : INetworkSerializeByMemcpy, IEquatable<MatchFlowState>
    {
        public byte Scene;
        public byte Resume;
        /// <summary>드론이 떠 있음(M4-04): 근접전 중에도 사격 무장.</summary>
        public byte Drones;
        /// <summary>점검 단계(M4-05): 장착한 무기 등.</summary>
        public byte CheckupStep;
        public double Start;
        public MatchResult Result;

        public bool Equals(MatchFlowState o) =>
            Scene == o.Scene && Resume == o.Resume && Drones == o.Drones && CheckupStep == o.CheckupStep && Start.Equals(o.Start) && Result.Equals(o.Result);
        public override bool Equals(object obj) => obj is MatchFlowState o && Equals(o);
        public override int GetHashCode() => Scene ^ (Resume << 8) ^ Start.GetHashCode();
    }

    /// <summary>
    /// 한 판 흐름(M4-01 프로토타입, 10_game_scenario.md). 서버만 장면을 정하고 장면 + 시작 시각을 복제한다.
    /// 전투 사건(<see cref="CombatServer.ServerCue"/>)을 듣고 <see cref="MatchFlowRules"/>에 신호로 넣으며, 장면에 맞춰 보스를 멈추거나 풀고,
    /// 사령관 대사와 장면 사건(<see cref="CombatCue.MatchScene"/>)을 낸다. 소리·글자판·맵 장치는 그 사건만 듣고 갈아 끼운다.
    /// 보스 무기(M4-02)가 부서지면(= 페이즈 전환) 상실 장면, 핵이면 필살기. 드론(M4-04)이 신호를 안 주면 장면 3은 시간으로 넘어간다.
    /// 운영자 조작은 키보드 없이 조절 메뉴(오른손 B) "판" 묶음. 메뉴 'Add Network Session'이 Combat Server 오브젝트에 붙인다.
    /// </summary>
    public class MatchFlow : NetworkBehaviour, ITunable
    {
        [SerializeField, Tooltip("장면 시간(등장 10초, 상실 8초, 결과 15초 등).")]
        MatchFlowSettings settings = new MatchFlowSettings();

        [SerializeField, Tooltip("전투 서버(같은 오브젝트). 비어 있으면 같은 오브젝트에서 찾는다.")]
        CombatServer combat;

        [SerializeField, Tooltip("보스 두뇌. 비어 있으면 시작할 때 한 번 찾는다. 없으면 보스를 멈추지 않는다.")]
        BossBrain boss;

        [SerializeField, Tooltip("조종사가 다 들어오면(1인 양팔은 바로, 호스트는 둘) 운영자가 누르지 않아도 기체 점검을 시작한다.")]
        bool autoStart = true;

        [SerializeField, Min(0f), Tooltip("기체 점검·등장 때 보스를 이 거리(m)에 둔다(대본: 마당 끝 약 120 m). 0이면 옮기지 않는다.")]
        float entranceDistance = 120f;

        [SerializeField, Range(0f, 100f), Tooltip("상실 장면이 끝나 다음 구간을 시작할 때 기체 HP를 이것(%)까지 채운다(대본 60).")]
        float restoreAfterLoss = 60f;

        [SerializeField, Tooltip("결과판 시간이 지나면 저절로 처음부터(전시 운영 편의, 기본 끔: 운영자가 재시작).")]
        bool autoRestartAfterResult;

        readonly NetworkVariable<MatchFlowState> state = new NetworkVariable<MatchFlowState>();
        MatchFlowRules rules;
        MatchFlowRules timing; // 모든 PC: 장면 길이만 읽는다
        readonly MatchStats stats = new MatchStats();
        SessionBootstrap session;
        CombatAuthority boundAuthority;
        bool bound;

        /// <summary>모든 PC: 장면이 바뀜(새 장면, 이전 장면).</summary>
        public event Action<MatchScene, MatchScene> SceneChanged;

        public MatchScene Scene => (MatchScene)state.Value.Scene;
        public double SceneStart => state.Value.Start;
        public MatchResult Result => state.Value.Result;
        public MatchScene ResumeScene => (MatchScene)state.Value.Resume;
        public bool DronesActive => state.Value.Drones != 0;
        public int CheckupStep => state.Value.CheckupStep;

        /// <summary>지금 두 팔 무기(M4-03). 모든 PC가 복제된 장면으로 같은 답을 낸다.</summary>
        public Loadout Loadout => WeaponRules.For(Scene, DronesActive, ResumeScene, CheckupStep);

        /// <summary>기체 점검(M4-05)이 단계를 맡는다: 켜지면 점검 중 스틱 앞으로는 출격 단계에서만 인정(점검이 신호를 넣음).</summary>
        public bool CheckupControlled { get; set; }

        bool dronesActive;
        int checkupStep;

        /// <summary>서버: 드론이 떴는지(M4-04가 부른다).</summary>
        public void SetDronesActive(bool value)
        {
            if (!IsServer || dronesActive == value) return;
            dronesActive = value;
            Publish();
        }

        /// <summary>서버: 기체 점검 단계(M4-05가 부른다).</summary>
        public void SetCheckupStep(int value)
        {
            if (!IsServer || checkupStep == value) return;
            checkupStep = value;
            Publish();
        }

        /// <summary>서버: 드론 격추 하나(결과판 숫자).</summary>
        public void ServerDroneDown() => stats.DroneDown();

        /// <summary>서버: 드론 전멸 등 바깥 신호를 넣는다(M4-04·05).</summary>
        public void ServerSignal(MatchSignal signal)
        {
            if (!IsServer || rules == null) return;
            Signal(signal);
        }
        public MatchFlowSettings Settings => settings;
        public bool Running => IsSpawned;
        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        /// <summary>지금 장면의 남은 시간(초). 시간으로 안 끝나는 장면이면 -1.</summary>
        public float Remaining
        {
            get
            {
                if (timing == null) timing = new MatchFlowRules(settings);
                var seconds = timing.SceneSeconds(Scene);
                if (seconds <= 0f) return -1f;
                return Mathf.Max(0f, seconds - (float)(Now - SceneStart));
            }
        }

        public override void OnNetworkSpawn()
        {
            if (combat == null) combat = GetComponent<CombatServer>();
            if (boss == null) boss = FindAnyObjectByType<BossBrain>();
            session = FindAnyObjectByType<SessionBootstrap>();
            state.OnValueChanged += OnState;
            if (IsServer)
            {
                rules = new MatchFlowRules(settings);
                stats.Reset();
                rules.Reset(Now);
                Publish();
            }
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnState;
            Unbind();
            rules = null;
            matchLog?.Dispose();
            matchLog = null;
        }

        void OnState(MatchFlowState previous, MatchFlowState current)
        {
            if (previous.Scene != current.Scene || !previous.Start.Equals(current.Start))
                SceneChanged?.Invoke((MatchScene)current.Scene, (MatchScene)previous.Scene);
        }

        void Unbind()
        {
            if (bound && combat != null)
            {
                combat.ServerCue -= OnCue;
                combat.ServerReset -= OnReset;
            }
            bound = false;
            boundAuthority = null;
        }

        void Update()
        {
            if (!IsServer || combat == null) return;
            if (rules == null) rules = new MatchFlowRules(settings);
            // 세션을 다시 시작하면 전투 서버가 새 판정 객체를 만든다 → 다시 묶고 처음부터.
            if (bound && combat.Authority != boundAuthority) Unbind();
            if (!bound)
            {
                if (combat.Authority == null) return;
                combat.ServerCue += OnCue;
                combat.ServerReset += OnReset;
                bound = true;
                boundAuthority = combat.Authority;
                OnReset();
            }

            var now = Now;
            if (rules.Scene == MatchScene.Waiting && autoStart && PilotsReady()) Signal(MatchSignal.Start);
            if (rules.Scene == MatchScene.Approach && boss != null && boss.InMeleeRange) Signal(MatchSignal.ReachedMelee);
            var before = rules.Scene;
            if (rules.Update(now)) Enter(before);
            if (autoRestartAfterResult && rules.ResultExpired(now)) combat.RequestReset();
            HoldBoss();
            // M4-03: 무기를 든 동안은 잽·훅 구분 없이 번갈아 세 번이 연계.
            if (combat.Authority != null) combat.Authority.Resolver.Combo.AnyKind = WeaponRules.For(rules.Scene, dronesActive, rules.ResumeScene, checkupStep) != Loadout.Bare;
            combat.Flush();
        }

        bool PilotsReady()
        {
            if (session == null || session.Mode == SessionMode.SoloBothArms) return true;
            return session.PilotCount >= SessionRules.MaxPilots;
        }

        void HoldBoss()
        {
            if (boss == null) return;
            var scene = rules.Scene;
            boss.MovementHeld = !MatchFlowRules.BossMayMove(scene);
            // 드론이 떠 있는 동안(근접전 끼어들기) 보스는 물러나 쉰다.
            boss.AttacksHeld = !MatchFlowRules.BossMayAttack(scene) || dronesActive;
            boss.RangedAllowed = scene == MatchScene.Approach;
        }

        void Signal(MatchSignal signal)
        {
            var before = rules.Scene;
            if (rules.Signal(signal, Now)) Enter(before);
        }

        /// <summary>서버: 전투 사건 → 판 신호, 결과판 숫자.</summary>
        void OnCue(CombatEvent e)
        {
            if (rules == null) return;
            switch (e.Cue)
            {
                case CombatCue.MoveStart:
                    if (!CheckupControlled && (MoveKind)(int)e.Amount == MoveKind.Approach) Signal(MatchSignal.CheckupDone);
                    break;
                // M4-02: 페이즈 전환 = 지금 차례 보스 무기 부서짐(보스 체력을 무기 셋이 30씩 나눠 가짐, BossWeaponRules).
                case CombatCue.PhaseChanged: Signal(MatchSignal.BossWeaponBroken); break;
                case CombatCue.BossKneel: Signal(MatchSignal.BossKneel); break;
                case CombatCue.EncounterWon: Signal(MatchSignal.BossDefeated); break;
                case CombatCue.PilotDefeated: Signal(MatchSignal.PilotDefeated); break;
                case CombatCue.GuardBlock:
                case CombatCue.Dodge: stats.Blocked(); break;
                case CombatCue.PilotHit: stats.Hit(); break;
                case CombatCue.ComboStep:
                case CombatCue.Boost: stats.Combo(e.ComboStep); break;
            }
        }

        /// <summary>서버: 판 재시작(SessionRestart → 전투 서버 초기화). 이전 판 상태를 지우고 대기로.</summary>
        void OnReset()
        {
            if (rules == null) return;
            var before = rules.Scene;
            stats.Reset();
            dronesActive = false;
            checkupStep = 0;
            rules.Reset(Now);
            Enter(before);
        }

        /// <summary>서버: 새 장면에 들어갈 때 할 일. 대사 번호는 10_game_scenario.md.</summary>
        void Enter(MatchScene previous)
        {
            var scene = rules.Scene;
            var now = Now;
            Emit(CombatCue.MatchScene, (int)scene, now, (int)previous);
            switch (scene)
            {
                case MatchScene.Waiting:
                    stats.Reset();
                    break;
                case MatchScene.Checkup:
                    if (boss != null && entranceDistance > 0f) boss.PlaceAtDistance(entranceDistance);
                    break;
                case MatchScene.Entrance:
                    if (boss != null && entranceDistance > 0f && previous != MatchScene.Checkup) boss.PlaceAtDistance(entranceDistance);
                    stats.Begin(now);
                    Line(CommanderLines.Sortie, now);
                    break;
                case MatchScene.DroneHunt:
                    Line(CommanderLines.Ambush, now);
                    break;
                case MatchScene.Approach:
                    if (previous == MatchScene.DroneHunt) Line(CommanderLines.DronesCleared, now);
                    break;
                case MatchScene.FightCannon: Line(CommanderLines.TargetCannon, now); break;
                case MatchScene.LostGun: Line(CommanderLines.GunLost, now); break;
                case MatchScene.FightArmor: Line(CommanderLines.TargetArmor, now); break;
                case MatchScene.LostSword: Line(CommanderLines.AllWeaponsLost, now); break;
                case MatchScene.FightCore: Line(CommanderLines.TargetCore, now); break;
                case MatchScene.Rebooting: Line(CommanderLines.Reboot, now); break;
                case MatchScene.Result:
                    break;
            }
            var authority = combat.Authority;
            // 재기동이 끝나 그 구간으로 돌아감: 기체 HP를 채우고 지금 보스 무기를 금 0개로(그 구간 처음부터).
            if (previous == MatchScene.Rebooting && scene != MatchScene.Waiting && authority != null)
            {
                authority.Pilot.Reset();
                if (boss != null)
                {
                    var bossHealth = authority.Resolver.Boss;
                    var hp = bossHealth.Max > 0f ? bossHealth.Current / bossHealth.Max * 100f : 0f;
                    var weapon = BossWeaponRules.Current(hp, boss.Thresholds);
                    if (weapon != BossWeapon.None) bossHealth.RestoreTo(BossWeaponRules.SegmentTop(weapon, boss.Thresholds));
                }
            }
            // 상실 장면이 끝나 다음 구간: 보복으로 깎인 기체 HP를 채운다.
            if ((previous == MatchScene.LostGun || previous == MatchScene.LostSword) && authority != null)
                authority.Pilot.RestoreTo(restoreAfterLoss);
            Publish();
            if (scene == MatchScene.Result) LogResult();
        }

        CsvLog matchLog;

        /// <summary>M6: 결과판이 뜨면 호스트가 판 기록 한 줄을 남긴다.</summary>
        void LogResult()
        {
            if (!IsServer) return;
            if (matchLog == null) matchLog = new CsvLog(CsvLog.DefaultPath(LogFolder.Root, "matches", DateTime.Now), MatchLogRow.Header);
            matchLog.Add(MatchLogRow.Format(state.Value.Result, DateTime.Now));
            matchLog.Pump();
        }

        void Publish()
        {
            if (!IsServer || rules == null) return;
            var result = rules.Scene == MatchScene.Result ? stats.Finish(rules.SceneStart, rules.Won, rules.RebootsUsed) : default;
            state.Value = new MatchFlowState
            {
                Scene = (byte)rules.Scene, Resume = (byte)rules.ResumeScene, Start = rules.SceneStart, Result = result,
                Drones = (byte)(dronesActive ? 1 : 0), CheckupStep = (byte)checkupStep,
            };
        }

        void Line(int line, double now) => Emit(CombatCue.CommanderLine, line, now);

        void Emit(CombatCue cue, int code, double now, float amount = 0f)
        {
            if (combat == null || combat.Authority == null) return;
            combat.Events.Add(new CombatEvent
            {
                Cue = cue, Kind = PunchKind.None, Code = code, Amount = amount, Time = now,
                BossHealth = combat.Authority.Resolver.Boss.Current,
            });
        }

        // ── 운영자 조작(키보드 없이, 조절 메뉴 "판") ──

        enum Command : byte { Start, Next }

        [Rpc(SendTo.Server)]
        void CommandRpc(byte command)
        {
            if (rules == null) return;
            switch ((Command)command)
            {
                case Command.Start:
                    Signal(MatchSignal.Start);
                    break;
                case Command.Next:
                    if (MatchFlowRules.IsFight(rules.Scene)) BreakBossWeapon();
                    else Signal(MatchSignal.Skip);
                    break;
            }
            combat.Flush();
        }

        /// <summary>
        /// 시험용: 지금 차례 보스 무기를 바로 부순다. 보스 체력을 그 무기 구간 바닥까지 깎아
        /// 보스 두뇌가 페이즈 전환(→ 무기 부서짐 신호)을 내게 한다. 핵이면 무릎.
        /// </summary>
        void BreakBossWeapon()
        {
            var authority = combat != null ? combat.Authority : null;
            if (authority == null) return;
            authority.Resolver.Boss.Apply(100f);
        }

        void Send(Command command)
        {
            if (!IsSpawned)
            {
                Debug.LogWarning("[MatchFlow] 세션이 시작되지 않아 판 조작을 보낼 수 없습니다.", this);
                return;
            }
            CommandRpc((byte)command);
        }

        public void AddTuning(TuningList list)
        {
            const string g = "판";
            list.Describe(g, "지금 장면은 눈앞 위쪽 글자판. \"다음 장면\"은 시험용(싸움 중이면 보스 무기를 바로 부숨)");
            // 값이 아니라 버튼처럼 쓴다: 켜는 순간 실행하고 늘 "끔"으로 보인다.
            list.AddToggle(g, "match.start", "판 시작(대기 → 기체 점검)", () => false, v => { if (v) Send(Command.Start); });
            list.AddToggle(g, "match.next", "다음 장면(시험용)", () => false, v => { if (v) Send(Command.Next); });
            list.AddToggle(g, "match.autoStart", "조종사가 다 들어오면 자동 시작", () => autoStart, v => autoStart = v);
            list.AddToggle(g, "match.autoRestart", "결과판 15초 뒤 자동 재시작", () => autoRestartAfterResult, v => autoRestartAfterResult = v);
        }
    }
}
