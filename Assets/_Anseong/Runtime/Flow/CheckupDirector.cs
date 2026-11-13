using System.Collections.Generic;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Net;
using Anseong.Pilot;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 기체 점검(M4-05, 장면 1 "정비고에서 무장 달기"). 서버가 단계를 정하고 판 흐름에 실어 복제한다.
    /// 1-1 두 팔 보정 끝 → 1-2 철판 셋 앞에서 번갈아 세 번 + 트리거 → 철판 날아감 → 1-3 검·방패 장착, 정비 로봇이 던지는 공 막기(오는 쪽은 오른쪽 HUD에만)
    /// → 1-4 스캐너·포 장착 설명 → 1-5 둘이 같이 스틱 앞으로 → 정비고 문이 무겁게 열리고 → 다 열리면 장면 2.
    /// 정비고·정비 로봇·공의 겉모습은 <see cref="CheckupBay"/>(메뉴 'Build Stage Props'가 만들어 칸에 넣는다). 비어 있어도 규칙은 그대로 돈다.
    /// 공은 조준 시작 때 닿는 시각을 정해 사건(<see cref="CombatCue.CheckupBall"/>)으로 보내고, 그 시각에 서버가 방패 판정을 한다(그림과 판정이 같은 순간).
    /// 메뉴 'Add Match Flow'가 Combat Server에 붙인다.
    /// </summary>
    public class CheckupDirector : NetworkBehaviour
    {
        [SerializeField, Min(1), Tooltip("관절 시험: 좌우 번갈아 몇 번(대본 3).")]
        int alternations = 3;
        [SerializeField, Tooltip("공 막기 연습(장면 1-3): 횟수·예고·날아오는 시간·간격.")]
        ShieldDrillSettings drill = new ShieldDrillSettings();
        [SerializeField, Min(1f), Tooltip("스캐너·포 설명 뒤 출격 안내까지(초).")]
        float briefSeconds = 5f;
        [SerializeField, Range(0.5f, 6f), Tooltip("출격 문이 열리는 시간(초). 다 열린 뒤 장면 2로 넘어간다.")]
        float doorSeconds = 2.8f;

        [Header("정비고 (메뉴 'Build Stage Props'가 넣는다)")]
        [SerializeField, Tooltip("정비고 벽·문 + 정비 로봇. 비면 겉모습 없이 규칙만.")]
        CheckupBay bay;
        [SerializeField, Tooltip("공이 오는 쪽 글자(XR 카메라 아래, 오른쪽 조종사 PC에만 켠다).")]
        TextMesh ballCallout;
        [SerializeField, Tooltip("공 글자를 눈앞 가운데에서 옆으로 띄우는 거리(m, 카메라 기준). 오는 쪽에 뜬다.")]
        Vector3 calloutOffset = new Vector3(0.17f, 0.04f, 0.9f);
        [SerializeField, Tooltip("공 글자에 쓸 글꼴(이 PC에 설치된 것 중 먼저 찾은 것).")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [Header("철판 (조종사 루트 기준 m)")]
        [SerializeField] Vector3[] plateOffsets = { new Vector3(-12f, 30f, 28f), new Vector3(0f, 34f, 30f), new Vector3(12f, 30f, 28f) };
        [SerializeField] Vector3 plateSize = new Vector3(8f, 10f, 1f);
        [SerializeField, Min(0f), Tooltip("철판이 날아가는 빠르기(m/s). 정비고 문 앞에 떨어지게 느리고 묵직하게.")]
        float plateFlySpeed = 12f;
        [SerializeField, Min(0f), Tooltip("철판이 위로 뜨는 빠르기(m/s).")]
        float plateFlyUp = 6f;
        [SerializeField, Min(0.5f), Tooltip("날아간 철판이 사라지기까지(초).")]
        float plateSeconds = 4.5f;
        [SerializeField] Material shapeMaterial;
        [SerializeField, Tooltip("M4-08 철판 표적 모델(성재 Meshy, Checkup_SteelPlate). 비면 도형 상자. 모델 크기 그대로, 원점 = 바닥 가운데.")]
        GameObject platePrefab;

        MatchFlow flow;
        CombatServer combat;
        PilotCalibrationFlow calibration;
        PilotHandInput input;
        SessionBootstrap session;
        Transform player;
        readonly AlternationCounter counter = new AlternationCounter();
        readonly List<CombatEvent> scratch = new List<CombatEvent>(8);
        readonly bool[] calibrated = new bool[2];
        ShieldDrill drillRules;
        CheckupStep step;
        double stepStart, doorDoneAt, calloutUntil;
        bool triggerArmed, doorOpening;
        bool bound, cueBound, reportedCalibration, triggerWasDown, bayInside, bayKnown;
        CombatDirector cueSource;
        MeshRenderer calloutRenderer;
        GameObject[] plates;
        bool[] flown;
        float[] flownAt;

        public override void OnNetworkSpawn()
        {
            flow = GetComponent<MatchFlow>();
            combat = GetComponent<CombatServer>();
            calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            input = FindAnyObjectByType<PilotHandInput>();
            session = FindAnyObjectByType<SessionBootstrap>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) player = seat.transform.root;
            if (flow != null) flow.CheckupControlled = true;
            if (flow != null) flow.SceneChanged += OnScene;
            if (bay != null) bay.Bind(player);
            bayKnown = false;
            SetUpCallout();
            BuildPlates();
        }

        public override void OnNetworkDespawn()
        {
            if (flow != null) flow.SceneChanged -= OnScene;
            if (bound && combat != null) combat.ServerCue -= OnCue;
            if (cueBound && cueSource != null) cueSource.Cue -= OnClientCue;
            bound = cueBound = false;
            cueSource = null;
            if (calloutRenderer != null) calloutRenderer.enabled = false;
        }

        void Update()
        {
            ClientTick();
            if (!IsServer || flow == null || combat == null || combat.Authority == null) return;
            if (!bound)
            {
                combat.ServerCue += OnCue;
                bound = true;
            }
            if (flow.Scene != MatchScene.Checkup) return;
            var now = Now;
            switch (step)
            {
                case CheckupStep.Connect:
                    // 1인 양팔은 한 PC가 두 팔을 다 보고하고, 2인은 PC마다 자기 팔.
                    if (calibrated[0] && calibrated[1]) Go(CheckupStep.JointTest, now);
                    break;
                case CheckupStep.ShieldDrill:
                    Drill(now);
                    break;
                case CheckupStep.GunBrief:
                    if (now - stepStart >= briefSeconds) Go(CheckupStep.Sortie, now);
                    break;
                case CheckupStep.Sortie:
                    // 문이 다 열린 뒤에 장면 2(마당 등장)로.
                    if (doorOpening && now >= doorDoneAt)
                    {
                        doorOpening = false;
                        Emit(CombatCue.HangarDoor, 1, now, PilotSide.Left, 0f, 0f, DoorPoint);
                        flow.ServerSignal(MatchSignal.CheckupDone);
                        combat.Flush();
                    }
                    break;
            }
        }

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        /// <summary>서버: 공 막기 연습에서 공이 조준·비행 중이면 true와 오는 쪽(자동 점검이 방패를 그쪽으로 든다).</summary>
        public bool BallIncoming(out PilotSide side)
        {
            side = drillRules != null ? drillRules.Side : PilotSide.Left;
            return IsServer && step == CheckupStep.ShieldDrill && drillRules != null && drillRules.InFlight;
        }

        Vector3 DoorPoint => bay != null ? bay.DoorCenter : player != null ? player.position + player.forward * 78f : Vector3.zero;

        Vector3 BallPoint(PilotSide side) =>
            bay != null ? bay.BallTarget(side) : player != null ? player.position + player.rotation * new Vector3(side == PilotSide.Left ? -6f : 6f, 50f, 13f) : Vector3.zero;

        void OnScene(MatchScene scene, MatchScene previous)
        {
            if (!IsServer) return;
            if (scene == MatchScene.Checkup || scene == MatchScene.Waiting)
            {
                calibrated[0] = calibrated[1] = false;
                counter.Reset();
                triggerArmed = doorOpening = false;
                drillRules = null;
                Go(CheckupStep.Connect, Now);
            }
        }

        void Go(CheckupStep next, double now)
        {
            step = next;
            stepStart = now;
            flow.SetCheckupStep((int)next);
            Emit(CombatCue.CheckupStep, (int)next, now, PilotSide.Left);
            switch (next)
            {
                case CheckupStep.Connect: Line(CommanderLines.Reach, now); break;
                case CheckupStep.JointTest: Line(CommanderLines.JointTest, now); counter.Reset(); triggerArmed = false; break;
                case CheckupStep.ShieldDrill:
                    Line(CommanderLines.CallOut, now);
                    drillRules = new ShieldDrill(drill);
                    drillRules.Begin(now);
                    break;
                case CheckupStep.GunBrief: Line(CommanderLines.WeaponsBrief, now); break;
                case CheckupStep.Sortie: Line(CommanderLines.PushTogether, now); doorOpening = false; break;
            }
            combat.Flush();
        }

        void OnCue(CombatEvent e)
        {
            if (flow == null || flow.Scene != MatchScene.Checkup) return;
            switch (step)
            {
                case CheckupStep.JointTest:
                    if (e.Cue == CombatCue.Miss || e.Cue == CombatCue.Hit)
                    {
                        if (counter.Strike(e.Side) >= alternations && !triggerArmed)
                        {
                            triggerArmed = true;
                            Line(CommanderLines.TriggerNow, e.Time);
                        }
                    }
                    break;
                case CheckupStep.Sortie:
                    // 둘이 같이 스틱을 앞으로 → 문이 열리기 시작한다. 다 열리면 Update가 장면을 넘긴다.
                    if (e.Cue == CombatCue.MoveStart && (MoveKind)(int)e.Amount == MoveKind.Approach && !doorOpening)
                    {
                        doorOpening = true;
                        doorDoneAt = e.Time + doorSeconds;
                        Emit(CombatCue.HangarDoor, 0, e.Time, PilotSide.Left, doorSeconds, 0f, DoorPoint);
                    }
                    break;
            }
        }

        /// <summary>
        /// 1-3 공 막기: 조준(오는 쪽은 오른쪽 HUD에만) → 던짐 → 닿는 순간에 방패 판정. 못 막아도 피해는 없다(연습).
        /// 닿는 시각은 조준을 시작할 때 사건에 실어 보내므로 각 PC의 공이 그 순간 방패 자리에 온다.
        /// </summary>
        void Drill(double now)
        {
            if (drillRules == null)
            {
                drillRules = new ShieldDrill(drill);
                drillRules.Begin(now);
            }
            switch (drillRules.Update(now, Random.value))
            {
                case DrillStep.Aim:
                    Emit(CombatCue.CheckupBall, 0, now, drillRules.Side, drill.aimSeconds, drill.flightSeconds, BallPoint(drillRules.Side));
                    combat.Flush();
                    break;
                case DrillStep.Impact:
                    var side = drillRules.Side;
                    var need = side == PilotSide.Left ? BossCounter.LeftGuard : BossCounter.RightGuard;
                    // 판정만 빌려 쓴다: 피해 0, 피격 사건(경보·붉은 화면)은 내보내지 않는다.
                    scratch.Clear();
                    var blocked = DefenseRules.IsSuccess(combat.Authority.ResolveBossStrike(need, side, 0f, -1, now, scratch));
                    var point = BallPoint(side);
                    if (blocked)
                        foreach (var made in scratch)
                        {
                            if (made.Cue != CombatCue.GuardBlock) continue;
                            // 막기 소리·진동·외골격 저항은 실전과 같은 사건으로.
                            var block = made;
                            block.Point = point;
                            combat.Events.Add(block);
                        }
                    Emit(CombatCue.CheckupBall, blocked ? 1 : 2, now, side, 0f, 0f, point);
                    drillRules.Report(blocked, now);
                    combat.Flush();
                    break;
                case DrillStep.Done:
                    Go(CheckupStep.GunBrief, now);
                    break;
            }
        }

        void Line(int line, double now) => Emit(CombatCue.CommanderLine, line, now, PilotSide.Left);

        void Emit(CombatCue cue, int code, double now, PilotSide side, float amount = 0f, float damage = 0f, Vector3 point = default)
        {
            combat.Events.Add(new CombatEvent
            {
                Cue = cue, Side = side, Kind = PunchKind.None, Code = code, Time = now, Amount = amount, Damage = damage, Point = point,
                BossHealth = combat.Authority.Resolver.Boss.Current,
            });
        }

        [Rpc(SendTo.Server)]
        void CalibratedRpc(byte side)
        {
            if (side < 2) calibrated[side] = true;
        }

        [Rpc(SendTo.Server)]
        void TriggerRpc()
        {
            if (step == CheckupStep.JointTest && triggerArmed && flow != null && flow.Scene == MatchScene.Checkup) Go(CheckupStep.ShieldDrill, Now);
        }

        // ── 모든 PC ──

        /// <summary>공이 오는 쪽을 이 PC가 보는가: 오른쪽 조종사(클라이언트)와 1인 양팔만(대본 "오른쪽 HUD에만").</summary>
        bool SeesBallSide => session == null || session.Mode == SessionMode.SoloBothArms || session.Mode == SessionMode.Client;

        void ClientTick()
        {
            if (!IsSpawned || flow == null) return;
            if (!cueBound && combat != null && combat.Director != null)
            {
                cueSource = combat.Director;
                cueSource.Cue += OnClientCue;
                cueBound = true;
            }
            var scene = flow.Scene;
            var inCheckup = scene == MatchScene.Checkup;
            if (!inCheckup) reportedCalibration = false;
            if (inCheckup && calibration != null && calibration.IsComplete && !reportedCalibration && input != null)
            {
                reportedCalibration = true;
                for (var h = 0; h < 2; h++)
                    if (input.IsArmHand((PilotSide)h)) CalibratedRpc((byte)h);
            }
            var trigger = false;
            if (input != null)
                for (var h = 0; h < 2; h++)
                    if (input.IsArmHand((PilotSide)h) && input.Get((PilotSide)h).Trigger > 0.7f) trigger = true;
            if (trigger && !triggerWasDown && inCheckup && flow.CheckupStep == (int)CheckupStep.JointTest) TriggerRpc();
            triggerWasDown = trigger;
            UpdatePlates(inCheckup && flow.CheckupStep <= (int)CheckupStep.JointTest);
            TickBay(scene, inCheckup);
            if (calloutRenderer != null && calloutRenderer.enabled && (Now > calloutUntil || !inCheckup)) calloutRenderer.enabled = false;
        }

        /// <summary>정비고: 대기·오프닝·점검 동안은 닫혀 서 있고, 그 밖의 장면이 되면 땅속으로 내려간다. 판을 다시 시작하면 다시 선다.</summary>
        void TickBay(MatchScene scene, bool inCheckup)
        {
            if (bay == null) return;
            var inside = inCheckup || scene == MatchScene.Waiting || scene == MatchScene.Opening;
            if (!bayKnown || inside != bayInside)
            {
                bayKnown = true;
                bayInside = inside;
                if (inside) bay.SetClosed();
                else bay.Leave(Now);
            }
            bay.ShowThrower(inCheckup && flow.CheckupStep == (int)CheckupStep.ShieldDrill);
            bay.Tick(Now);
        }

        void OnClientCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.CheckupBall:
                    if (e.Code == 0)
                    {
                        var release = e.Time + e.Amount;
                        var impact = release + e.Damage;
                        if (bay != null) bay.Aim(e.Side, e.Time, release, impact);
                        ShowCallout(e.Side, impact);
                    }
                    else
                    {
                        if (bay != null) bay.BallResult(e.Code == 1);
                        if (calloutRenderer != null) calloutRenderer.enabled = false;
                    }
                    break;
                case CombatCue.HangarDoor:
                    if (e.Code == 0 && bay != null) bay.OpenDoors(e.Time, e.Amount);
                    break;
            }
        }

        void SetUpCallout()
        {
            if (ballCallout == null) return;
            calloutRenderer = ballCallout.GetComponent<MeshRenderer>();
            var font = osFontNames != null && osFontNames.Length > 0
                ? Font.CreateDynamicFontFromOSFont(osFontNames, ballCallout.fontSize > 0 ? ballCallout.fontSize : 32) : null;
            if (font != null)
            {
                ballCallout.font = font;
                if (calloutRenderer != null) calloutRenderer.sharedMaterial = font.material;
            }
            if (calloutRenderer != null) calloutRenderer.enabled = false;
        }

        void ShowCallout(PilotSide side, double until)
        {
            if (ballCallout == null || calloutRenderer == null || !SeesBallSide) return;
            var left = side == PilotSide.Left;
            ballCallout.text = left ? "◀◀ 왼쪽에서 공!" : "오른쪽에서 공! ▶▶";
            var offset = calloutOffset;
            offset.x = left ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x);
            ballCallout.transform.localPosition = offset;
            calloutRenderer.enabled = true;
            calloutUntil = until;
        }

        void BuildPlates()
        {
            plates = new GameObject[plateOffsets.Length];
            flown = new bool[plateOffsets.Length];
            flownAt = new float[plateOffsets.Length];
            for (var i = 0; i < plates.Length; i++) plates[i] = MakePlate(i);
        }

        GameObject MakePlate(int i)
        {
            if (platePrefab != null)
            {
                // 모델 원점 = 바닥 가운데(13_ai_tools 4번) → 빈 부모 아래 반 높이 내려 가운데를 맞춘다.
                var holder = new GameObject("Checkup Plate " + i);
                var model = Instantiate(platePrefab, holder.transform, false);
                model.transform.localPosition = Vector3.down * (plateSize.y * 0.5f);
                foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);
                foreach (var rb in model.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
                holder.SetActive(false);
                return holder;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            var r = go.GetComponent<Renderer>();
            if (shapeMaterial != null && r != null) r.sharedMaterial = shapeMaterial;
            go.name = "Checkup Plate " + i;
            go.transform.localScale = plateSize;
            go.SetActive(false);
            return go;
        }

        void UpdatePlates(bool show)
        {
            if (plates == null) return;
            for (var i = 0; i < plates.Length; i++)
            {
                var plate = plates[i];
                if (plate == null) continue;
                if (show)
                {
                    if (flown[i])
                    {
                        // 다시 점검: 날아간 철판을 새로 만든다.
                        Destroy(plate);
                        plates[i] = plate = MakePlate(i);
                        flown[i] = false;
                    }
                    if (!plate.activeSelf) plate.SetActive(true);
                    if (player != null) plate.transform.SetPositionAndRotation(player.position + player.rotation * plateOffsets[i], player.rotation);
                }
                else if (plate.activeSelf && !flown[i] && flow.Scene == MatchScene.Checkup)
                {
                    // 트리거 → 철판이 날아간다(정비고 안에 떨어지게 느리고 묵직하게).
                    flown[i] = true;
                    flownAt[i] = Time.time;
                    var body = plate.GetComponent<Rigidbody>();
                    if (body == null) body = plate.AddComponent<Rigidbody>();
                    body.mass = 50f;
                    body.linearVelocity = (player != null ? player.forward : Vector3.forward) * plateFlySpeed + Vector3.up * plateFlyUp;
                    body.angularVelocity = Random.insideUnitSphere * 3f;
                }
                else if (plate.activeSelf && (flow.Scene != MatchScene.Checkup || (flown[i] && Time.time - flownAt[i] > plateSeconds)))
                    plate.SetActive(false);
            }
        }
    }
}
