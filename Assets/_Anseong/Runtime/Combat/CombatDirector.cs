using System;
using System.Collections.Generic;
using Anseong.Boss;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 전투 판정 연결(M1-05, M2-01에서 서버 경로로). 펀치 분류(사람 손) → 공격 보정(로봇 주먹 끌어당기기) → 주먹 접촉 → <see cref="HitReport"/>
    /// → <see cref="ICombatLink"/>(전투 서버) → 서버가 보낸 결과 → <see cref="Cue"/> 이벤트.
    /// 이 PC에서는 판정 규칙(연계·부스트·보스 HP)을 돌리지 않는다. 규칙은 서버(<see cref="CombatAuthority"/>)만 돌리고,
    /// 지금 상태는 <see cref="State"/>(서버가 복제)로 읽는다. 그립 잠금·발사 소리처럼 서버를 기다리면 늦는 것만 여기서 바로 낸다.
    /// 소리·진동·보스 애니메이션·HP 막대·안내판은 모두 <see cref="Cue"/>만 듣는다. 새 연출은 듣는 컴포넌트를 하나 더 붙이면 된다.
    /// 실행 순서: 펀치 분류(Update) 뒤, 팔 IK(LateUpdate 100) 뒤에 접촉을 본다.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class CombatDirector : MonoBehaviour, ITunable
    {
        public const int ExecutionOrder = 200;

        [Header("연결 (메뉴가 채운다. 비어 있으면 시작할 때 한 번 찾는다)")]
        [SerializeField] PilotHandInput input;
        [SerializeField] PunchClassifierDriver classifier;
        [SerializeField, Tooltip("왼팔·오른팔 IK. 공격 보정이 주먹 목표를 끌어당긴다.")]
        RobotArmDriver[] arms = new RobotArmDriver[0];
        [SerializeField, Tooltip("왼팔·오른팔 주먹 명중 구.")]
        FistHitProbe[] fists = new FistHitProbe[0];
        [SerializeField, Tooltip("때릴 보스. 비어 있으면 씬에서 한 번 찾는다. 없으면 모든 공격이 헛침.")]
        BossTarget boss;

        [Header("규칙 (01_combat_rules 초기값)")]
        [SerializeField] AttackAssistSettings assist = new AttackAssistSettings();
        [SerializeField] ComboSettings combo = new ComboSettings();
        [SerializeField] BoostSettings boost = new BoostSettings();
        [SerializeField] BossHealthSettings bossHealth = new BossHealthSettings();
        [SerializeField, Tooltip("조종사 로봇 HP·위기 피해 감소(05, M2-03). 서버 PC의 값이 쓰인다.")]
        PilotHealthSettings pilotHealth = new PilotHealthSettings();
        [SerializeField, Tooltip("협동 회피 판정(01, M2-03). 서버 PC의 값이 쓰인다.")]
        DodgeSettings dodge = new DodgeSettings();
        [SerializeField, Tooltip("합동 필살기(01, M2-05). 펴짐·각도는 보고하는 PC, 시간은 서버 PC의 값이 쓰인다.")]
        FinisherSettings finisher = new FinisherSettings();

        [Header("부스트 입력")]
        [SerializeField, Range(0.1f, 1f), Tooltip("트리거를 이만큼 당기면 누름(부스트).")]
        float triggerPress = 0.6f;
        [SerializeField, Range(0f, 0.9f), Tooltip("트리거를 이만큼 아래로 놓으면 뗌.")]
        float triggerRelease = 0.4f;

        [Header("개발용")]
        [SerializeField, Tooltip("전투 이벤트마다 Console에 한 줄(에디터·개발 빌드).")]
        bool logToConsole = true;

        ICombatLink link;
        CombatSnapshot snapshot;
        bool warnedOffline, dispatching;
        // 팔마다 서버에 명중으로 보고한 마지막 동작 번호(같은 동작을 두 번 보내지 않게). 동작 번호는 팔마다 오르기만 한다.
        readonly int[] reported = { 0, 0 };
        readonly List<Pending> events = new List<Pending>(16);
        readonly ArmState[] state = { new ArmState(), new ArmState() };
        readonly RobotArmDriver[] armBySide = new RobotArmDriver[2];
        readonly FistHitProbe[] fistBySide = new FistHitProbe[2];

        struct Pending
        {
            public CombatEvent Event;
            public bool HasState;
            public CombatSnapshot State;
        }

        sealed class ArmState
        {
            public AttackAssist Assist;
            public Func<Vector3, Vector3> Modifier;
            public BossZone AssistZone;
            public PunchKind PeekKind;
            public int Launched, Touched, NearStrike, HitStrike;
            public HitQuality HitHow;
            public float NearGap;
            public BossZone NearZone;
            public Vector3 NearPoint;
            public BossZone TouchedZone;
            public Vector3 TouchedPoint, PreviousFist;
            public bool HasPreviousFist, GripDown, GripSent, TriggerDown;
        }

        /// <summary>전투에서 일어난 일. 소리·애니메이션·안내판은 이것만 듣는다.</summary>
        public event Action<CombatEvent> Cue;

        /// <summary>서버가 복제한 지금 전투 상태(보스 HP·연계·부스트 창).</summary>
        public CombatSnapshot State => snapshot;
        /// <summary>전투 서버와 연결됨. 아니면 명중을 보내지 못한다(호스트 시작 실패 등).</summary>
        public bool Connected => link != null;
        public BossTarget Boss => boss;

        /// <summary>서버가 쓸 규칙 객체를 이 컴포넌트의 규칙 수치(01 초기값, 조절 메뉴)로 만든다. 같은 설정 객체를 나눠 써서 조절 메뉴 값이 바로 반영된다.</summary>
        public CombatResolver CreateResolver() => new CombatResolver(combo, boost, bossHealth);

        /// <summary>서버 규칙 묶음 전체(전투 + 조종사 HP + 회피)를 이 컴포넌트의 수치로 만든다.</summary>
        public CombatAuthority CreateAuthority() => new CombatAuthority(CreateResolver(), pilotHealth, dodge, finisher);

        /// <summary>필살기 수치(준비 판정하는 쪽이 읽는다).</summary>
        public FinisherSettings Finisher => finisher;

        /// <summary>전투 서버 보고 통로(가드·회피 스틱 보고 컴포넌트가 쓴다). 연결 전이면 null.</summary>
        public ICombatLink Link => link;

        /// <summary>그 팔의 그 동작을 이미 명중으로 보고했는지.</summary>
        public bool AlreadyHit(PilotSide side, int strikeId) => strikeId > 0 && strikeId <= reported[(int)side];

        /// <summary>전투 서버가 연결될 때(호스트 시작 뒤). 지금 그립 상태를 한 번 보낸다.</summary>
        public void Connect(ICombatLink value)
        {
            link = value;
            warnedOffline = false;
            if (link == null) return;
            for (var h = 0; h < 2; h++)
            {
                link.SubmitArmInput((PilotSide)h, state[h].GripDown, false);
                state[h].GripSent = state[h].GripDown;
            }
        }

        public void Disconnect(ICombatLink value)
        {
            if (link == value) link = null;
        }

        /// <summary>서버가 보낸 결과 이벤트. 상태를 먼저 바꾸고 듣는 쪽에 낸다.</summary>
        public void ReceiveServer(CombatEvent e, CombatSnapshot value)
        {
            events.Add(new Pending { Event = e, HasState = true, State = value });
            Dispatch();
        }

        /// <summary>서버 상태만 바뀜(재시작 등, 이벤트 없음).</summary>
        public void ReceiveState(CombatSnapshot value) => snapshot = value;

        /// <summary>공격 보정이 지금 그 팔을 끌어당기는 중.</summary>
        public bool IsAssisting(PilotSide side) => state[(int)side].Assist != null && state[(int)side].Assist.Pulling;

        void Awake()
        {
            snapshot = CombatSnapshot.Full(bossHealth.maxHealth, pilotHealth.maxHealth);
            for (var h = 0; h < 2; h++)
            {
                var s = state[h];
                s.Assist = new AttackAssist(assist);
                s.Modifier = s.Assist.Apply;
            }
        }

        void Start()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (classifier == null) classifier = FindAnyObjectByType<PunchClassifierDriver>();
            if (boss == null) boss = FindAnyObjectByType<BossTarget>();
            if (arms == null || arms.Length == 0) arms = FindObjectsByType<RobotArmDriver>(FindObjectsSortMode.None);
            if (fists == null || fists.Length == 0) fists = FindObjectsByType<FistHitProbe>(FindObjectsSortMode.None);
            foreach (var arm in arms)
                if (arm != null) armBySide[(int)arm.Side] = arm;
            foreach (var fist in fists)
                if (fist != null) fistBySide[(int)fist.Side] = fist;
            if (input == null || classifier == null)
            {
                Debug.LogWarning("[CombatDirector] PilotHandInput·PunchClassifierDriver를 찾지 못해 전투 판정을 끕니다. " +
                                 "'Anseong → Setup → Rebuild Robot + Pilot Input'을 실행하세요.", this);
                enabled = false;
                return;
            }
            if (boss == null)
                Debug.LogWarning("[CombatDirector] 씬에 보스(BossTarget)가 없어 모든 공격이 헛침으로 처리됩니다.", this);
            for (var h = 0; h < 2; h++)
                if (armBySide[h] != null) armBySide[h].TargetModifier = state[h].Modifier;
            classifier.StrikeCompleted += OnStrike;
            classifier.HitColumn = HitColumn;
        }

        void OnDestroy()
        {
            if (classifier != null)
            {
                classifier.StrikeCompleted -= OnStrike;
                classifier.HitColumn = null;
            }
            for (var h = 0; h < 2; h++)
                if (armBySide[h] != null) armBySide[h].TargetModifier = null;
        }

        void Update()
        {
            var now = Time.timeAsDouble;
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                var s = state[h];
                if (!input.IsArmHand(side)) continue;
                var sample = input.Get(side);

                if (sample.GripHeld != s.GripDown)
                {
                    s.GripDown = sample.GripHeld;
                    Emit(sample.GripHeld ? CombatCue.GripLock : CombatCue.GripRelease, side, PunchKind.None, now);
                }
                var pressed = false;
                if (!s.TriggerDown && sample.Trigger >= triggerPress) s.TriggerDown = pressed = true;
                else if (s.TriggerDown && sample.Trigger <= triggerRelease) s.TriggerDown = false;
                var gripChanged = sample.GripHeld != s.GripSent;
                if ((gripChanged || pressed) && link != null)
                {
                    // 부스트 입력은 서버가 판정한다. 바뀐 때만 보낸다(M3에서 RPC 수를 줄이려고).
                    link.SubmitArmInput(side, sample.GripHeld, pressed);
                    s.GripSent = sample.GripHeld;
                }

                var punch = classifier.Classifier(side);
                if (punch != null && punch.Peek(out var kind))
                {
                    var strike = punch.CurrentStrikeId;
                    s.PeekKind = kind;
                    if (s.Launched != strike)
                    {
                        s.Launched = strike;
                        Emit(CombatCue.StrikeLaunch, side, kind, now);
                    }
                    if (!AlreadyHit(side, strike)) TryBeginAssist(h);
                }
                // 동작은 끝났지만 로봇 주먹을 기다리는 중이면 그 종류로 계속 접촉·보정을 본다.
                else if (classifier.TryGetHeld(side, out var held) && held.IsAttack && !AlreadyHit(side, held.StrikeId)) s.PeekKind = held.Kind;
                else s.PeekKind = PunchKind.None;
                if (armBySide[h] != null) s.Assist.Track(armBySide[h].FistRatio, armBySide[h].TargetRatio);
                s.Assist.Update(Time.deltaTime);
            }
            Dispatch();
        }

        void TryBeginAssist(int h)
        {
            var arm = armBySide[h];
            var fist = fistBySide[h];
            if (!assist.enabled || assist.mode != AssistMode.Pull || boss == null || arm == null || fist == null) return;
            if (!boss.TryClosest(fist.Position, out var zone, out var point)) return;
            if (!AttackAssist.InRange(arm.ShoulderPosition, arm.BodyRotation, point, arm.ArmLength, assist)) return;
            state[h].Assist.Begin(point, arm.FistRatio);
            state[h].AssistZone = zone.Zone;
        }

        void LateUpdate()
        {
            var now = Time.timeAsDouble;
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                var s = state[h];
                var fist = fistBySide[h];
                if (fist == null) continue;
                var current = fist.Position;
                if (!s.HasPreviousFist)
                {
                    s.PreviousFist = current;
                    s.HasPreviousFist = true;
                    continue;
                }
                var punch = classifier.Classifier(side);
                var strike = punch != null ? punch.CurrentStrikeId : 0;
                if (strike == 0 && classifier.TryGetHeld(side, out var held) && held.IsAttack) strike = held.StrikeId;
                if (strike > 0 && !AlreadyHit(side, strike) && input.IsArmHand(side))
                {
                    TrackNearMiss(s, strike, current, fist.Radius);
                    if (TryContact(s.PreviousFist, current, fist.Radius, out var zone, out var point))
                    {
                        if (s.PeekKind != PunchKind.None) Hit(side, s.PeekKind, strike, HitQuality.Contact, zone, point, now);
                        else
                        {
                            // 닿았지만 아직 종류를 모른다: 동작이 끝나 종류가 정해지면 명중시킨다.
                            s.Touched = strike;
                            s.TouchedZone = zone;
                            s.TouchedPoint = point;
                        }
                    }
                }
                s.PreviousFist = current;
            }
            Dispatch();
        }

        // 판정만 보정: 이 동작 동안 로봇 주먹 겉면이 보스 부위에 가장 가까웠던 거리와 그 자리.
        void TrackNearMiss(ArmState s, int strike, Vector3 fist, float radius)
        {
            if (!assist.enabled || assist.mode != AssistMode.JudgeOnly || boss == null) return;
            if (s.NearStrike != strike)
            {
                s.NearStrike = strike;
                s.NearGap = float.PositiveInfinity;
            }
            if (!boss.TryClosest(fist, out var zone, out var surface)) return;
            var gap = Mathf.Max(0f, Vector3.Distance(fist, surface) - radius);
            if (gap >= s.NearGap) return;
            s.NearGap = gap;
            s.NearZone = zone.Zone;
            s.NearPoint = surface;
        }

        bool TryContact(Vector3 from, Vector3 to, float radius, out BossZone zone, out Vector3 point)
        {
            zone = BossZone.Body;
            point = to;
            if (boss == null) return false;
            var swept = new Bounds(from, Vector3.zero);
            swept.Encapsulate(to);
            swept.Expand(radius * 2f);
            foreach (var z in boss.Zones)
            {
                if (z == null || !z.IsActive || !z.Bounds.Intersects(swept)) continue;
                if (!FistContact.Sweep(from, to, radius, z.ClosestPoint, out point)) continue;
                zone = z.Zone;
                return true;
            }
            return false;
        }

        void OnStrike(PilotSide side, PunchResult result)
        {
            var h = (int)side;
            var s = state[h];
            var now = Time.timeAsDouble;
            var assisted = s.Assist.Pulling;
            s.Assist.Release();
            s.PeekKind = PunchKind.None;
            if (!result.IsAttack || AlreadyHit(side, result.StrikeId)) return;
            if (s.Touched == result.StrikeId)
                Hit(side, result.Kind, result.StrikeId, HitQuality.Contact, s.TouchedZone, s.TouchedPoint, now);
            else if (assisted)
                // 종류가 인식되고 보스가 보정 범위 안이었으면 명중(선우 10/18 공격 보정). 이 결과는 로봇 주먹이 따라온 뒤에 오므로
                // 진동·피해도 로봇 팔이 뻗은 때 난다(선우 10/25: 다 뻗기 전에 진동이 울림).
                Hit(side, result.Kind, result.StrikeId, HitQuality.Assisted, s.AssistZone, s.Assist.Point, now);
            else if (s.NearStrike == result.StrikeId && AttackAssist.CountsAsNearMiss(s.NearGap, assist, result.Kind))
                // 판정만 보정(선우 10/28): 주먹은 그대로 두고, 보스 근처를 지나갔으면 명중으로 친다.
                Hit(side, result.Kind, result.StrikeId, HitQuality.Assisted, s.NearZone, s.NearPoint, now);
            else
            {
                LogNearMiss(s, result.StrikeId, result.Kind);
                if (Online()) link.SubmitMiss(side, result.Kind, result.StrikeId);
            }
            Dispatch();
        }

        void Hit(PilotSide side, PunchKind kind, int strike, HitQuality quality, BossZone zone, Vector3 point, double now)
        {
            var report = new HitReport { Side = side, Kind = kind, StrikeId = strike, Quality = quality, Zone = zone, Point = point, ClientTime = now };
            // 서버로 보고(호스트는 같은 PC 안 호출, M3 PC-B는 ServerRpc). 결과는 서버가 이벤트로 돌려준다.
            reported[(int)side] = strike;
            if (Online()) link.SubmitHit(report);
            var s = state[(int)side];
            s.Assist.Release();
            s.HitStrike = strike;
            s.HitHow = quality;
        }

        string HitColumn(PilotSide side, PunchResult result)
        {
            if (!result.IsAttack) return string.Empty;
            var s = state[(int)side];
            var c = System.Globalization.CultureInfo.InvariantCulture;
            var hit = AlreadyHit(side, result.StrikeId);
            var how = hit && s.HitStrike == result.StrikeId ? s.HitHow.ToString() : string.Empty;
            var gap = s.NearStrike == result.StrikeId && !float.IsInfinity(s.NearGap) ? s.NearGap.ToString("F2", c) : string.Empty;
            var arm = armBySide[(int)side];
            var reach = arm != null ? arm.FistRatio.magnitude.ToString("F2", c) : string.Empty;
            return string.Join(",", hit ? "1" : "0", how, gap, reach);
        }

        void Emit(CombatCue cue, PilotSide side, PunchKind kind, double now)
        {
            events.Add(new Pending
            {
                Event = new CombatEvent
                {
                    Cue = cue, Side = side, Kind = kind, Time = now,
                    ComboStep = snapshot.ComboStep, BossHealth = snapshot.BossHealth,
                    Point = fistBySide[(int)side] != null ? fistBySide[(int)side].Position : Vector3.zero,
                },
            });
        }

        bool Online()
        {
            if (link != null) return true;
            if (!warnedOffline)
            {
                warnedOffline = true;
                Debug.LogWarning("[CombatDirector] 전투 서버가 연결되지 않아 명중을 보내지 못합니다. 호스트가 시작됐는지 안내판·Console을 보세요 " +
                                 "('Anseong → Setup → Add Network Session').", this);
            }
            return false;
        }

        void Dispatch()
        {
            // 듣는 쪽이 이벤트 안에서 다시 보고해 새 이벤트가 생기면(호스트는 바로 돌아옴) 같은 반복에서 이어서 낸다.
            if (dispatching || events.Count == 0) return;
            dispatching = true;
            try
            {
                for (var i = 0; i < events.Count; i++)
                {
                    var p = events[i];
                    if (p.HasState) snapshot = p.State;
                    Log(p.Event);
                    Cue?.Invoke(p.Event);
                }
            }
            finally
            {
                events.Clear();
                dispatching = false;
            }
        }

        // 헛침일 때 보스까지 얼마나 모자랐는지(허용 거리를 정할 때 본다).
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void LogNearMiss(ArmState s, int strike, PunchKind kind)
        {
            if (!logToConsole || !assist.enabled || assist.mode != AssistMode.JudgeOnly || s.NearStrike != strike) return;
            Debug.Log($"[Combat] 빗나감: 로봇 주먹이 보스까지 {s.NearGap:F1}m 모자람 ({kind} 허용 {AttackAssist.Allowance(assist, kind):F1}m)", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Log(CombatEvent e)
        {
            if (!logToConsole || e.Cue == CombatCue.GripLock || e.Cue == CombatCue.GripRelease) return;
            Debug.Log($"[Combat] {e.Cue} {e.Side} {e.Kind} 피해 {e.Damage:F1}% 연계 {e.ComboStep} 보스 HP {e.BossHealth:F1}%" +
                      (e.Cue == CombatCue.ComboBroken ? $" (끊김: {e.Break})" : e.Cue == CombatCue.Hit ? $" ({e.Quality}, {e.Zone})" : string.Empty), this);
        }

        /// <summary>인게임 조절 메뉴: 공격 보정·연계·부스트.</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "공격 보정";
            var a = assist;
            list.Describe(g, "할 것: 보스 옆을 살짝 빗겨 치기, 크게 빗겨 치기\n       판정만 ↔ 끌어당기기, 보정 끄고 켜서 비교\n볼 것: 살짝 빗맞으면 명중, 크게 빗나가면 헛침");
            list.AddCheck(g, "assist.near", "살짝 빗맞아도 명중");
            list.AddCheck(g, "assist.far", "크게 빗나가면 헛침");
            list.AddToggle(g, "assist.enabled", "공격 보정(끄면 실제로 닿아야 명중)", () => a.enabled, v => a.enabled = v);
            list.AddChoice(g, "assist.mode", "보정 방식", () => (int)a.mode, v => a.mode = (AssistMode)v, new[] { "판정만(주먹 그대로)", "주먹 끌어당기기" });
            list.AddFloat(g, "assist.nearMiss", "판정만: 빗맞음 허용 거리(m)", () => a.nearMissMeters, v => a.nearMissMeters = v, 0f, 15f, 0.5f, 1);
            list.AddFloat(g, "assist.hookNearMiss", "판정만: 훅 허용 거리(m)", () => a.hookNearMissMeters, v => a.hookNearMissMeters = v, 0f, 20f, 0.5f, 1);
            list.AddFloat(g, "assist.pullSeconds", "끌어당기기: 끌려가는 시간(초)", () => a.pullSeconds, v => a.pullSeconds = v, 0.02f, 0.6f, 0.02f);

            const string c = "연계·부스트";
            list.Describe(c, "할 것: 잽(한 팔) → 잽(다른 팔) → 훅(처음 팔), 훅 명중 직후 트리거로 부스트\n볼 것: 타이밍이 빡빡하면 아래 시간을 늘림");
            list.AddFloat(c, "combo.comboSeconds", "다음 명중 제한(초)", () => combo.comboSeconds, v => combo.comboSeconds = v, 0.5f, 5f, 0.1f, 1);
            list.AddFloat(c, "boost.windowSeconds", "부스트 입력창(초)", () => boost.windowSeconds, v => boost.windowSeconds = v, 0.1f, 2f, 0.05f);
            // 값이 아니라 버튼처럼 쓴다: 켜는 순간 채우고 늘 "끔"으로 보인다(저장값도 끔).
            list.AddToggle(c, "pilot.crisis", "위기 피해 감소(HP 낮을수록 덜 아픔)", () => pilotHealth.crisisReduction, v => pilotHealth.crisisReduction = v);
            list.AddToggle(c, "boss.refill", "보스 HP 다시 채우기(켜면 바로)", () => false, v => { if (v) ResetBoss(); });
        }

        /// <summary>테스트용: 보스 HP·연계·부스트를 처음 상태로(선우 10/28). 조절 메뉴와 Inspector 오른쪽 클릭 메뉴에서 부른다.</summary>
        [ContextMenu("Reset Boss HP")]
        public void ResetBoss()
        {
            if (!Online()) return;
            link.RequestReset();
            Debug.Log("[CombatDirector] 보스 HP 다시 채움", this);
        }
    }
}
