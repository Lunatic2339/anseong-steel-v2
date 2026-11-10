using Anseong.Boss;
using Anseong.Combat;
using Anseong.Net;
using Anseong.Pilot;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>
    /// 기체 점검(M4-05 프로토타입, 장면 1 "정비고에서 무장 달기"). 서버가 단계를 정하고 판 흐름에 실어 복제한다.
    /// 1-1 두 팔 보정 끝 → 1-2 철판 셋 앞에서 번갈아 세 번 + 트리거 → 철판 날아감 → 1-3 검·방패 장착, 공 막기 3번(오는 쪽은 오른쪽 HUD에만)
    /// → 1-4 스캐너·포 장착 설명 → 1-5 둘이 같이 스틱 앞으로 = 출격.
    /// 철판은 도형(윤주 정비고 자리가 오면 그 위치로). 메뉴 'Add Match Flow'가 Combat Server에 붙인다.
    /// </summary>
    public class CheckupDirector : NetworkBehaviour
    {
        [SerializeField, Min(1), Tooltip("관절 시험: 좌우 번갈아 몇 번(대본 3).")]
        int alternations = 3;
        [SerializeField, Min(1), Tooltip("공 막기 연습: 몇 번 막으면 넘어가나.")]
        int drillBlocks = 3;
        [SerializeField, Min(0.5f), Tooltip("공이 오는 예고 시간(초).")]
        float drillTelegraph = 2f;
        [SerializeField, Min(0.5f), Tooltip("공 사이 쉬는 시간(초).")]
        float drillRest = 2f;
        [SerializeField, Min(1f), Tooltip("스캐너·포 설명 뒤 출격 안내까지(초).")]
        float briefSeconds = 5f;

        [Header("철판 (조종사 루트 기준 m)")]
        [SerializeField] Vector3[] plateOffsets = { new Vector3(-12f, 30f, 28f), new Vector3(0f, 34f, 30f), new Vector3(12f, 30f, 28f) };
        [SerializeField] Vector3 plateSize = new Vector3(8f, 10f, 1f);
        [SerializeField] Material shapeMaterial;

        MatchFlow flow;
        CombatServer combat;
        PilotCalibrationFlow calibration;
        PilotHandInput input;
        Transform player;
        readonly AlternationCounter counter = new AlternationCounter();
        readonly bool[] calibrated = new bool[2];
        CheckupStep step;
        double stepStart, drillAt;
        int blocks;
        bool drillPending, triggerArmed;
        PilotSide drillSide;
        bool bound, reportedCalibration, triggerWasDown;
        GameObject[] plates;
        bool[] flown;

        public override void OnNetworkSpawn()
        {
            flow = GetComponent<MatchFlow>();
            combat = GetComponent<CombatServer>();
            calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            input = FindAnyObjectByType<PilotHandInput>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) player = seat.transform.root;
            if (flow != null) flow.CheckupControlled = true;
            if (flow != null) flow.SceneChanged += OnScene;
            BuildPlates();
        }

        public override void OnNetworkDespawn()
        {
            if (flow != null) flow.SceneChanged -= OnScene;
            if (bound && combat != null) combat.ServerCue -= OnCue;
            bound = false;
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
            }
        }

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        void OnScene(MatchScene scene, MatchScene previous)
        {
            if (!IsServer) return;
            if (scene == MatchScene.Checkup || scene == MatchScene.Waiting)
            {
                calibrated[0] = calibrated[1] = false;
                counter.Reset();
                blocks = 0;
                drillPending = triggerArmed = false;
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
                case CheckupStep.ShieldDrill: Line(CommanderLines.CallOut, now); blocks = 0; drillAt = now + drillRest; break;
                case CheckupStep.GunBrief: Line(CommanderLines.WeaponsBrief, now); break;
                case CheckupStep.Sortie: Line(CommanderLines.PushTogether, now); break;
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
                    if (e.Cue == CombatCue.MoveStart && (MoveKind)(int)e.Amount == MoveKind.Approach) flow.ServerSignal(MatchSignal.CheckupDone);
                    break;
            }
        }

        /// <summary>1-3 공 막기: 예고(오른쪽 HUD에만 방향) → 명중 순간에 방패 판정(피해 0).</summary>
        void Drill(double now)
        {
            if (!drillPending && now >= drillAt)
            {
                drillPending = true;
                drillSide = Random.value < 0.5f ? PilotSide.Left : PilotSide.Right;
                drillAt = now + drillTelegraph;
                combat.Events.Add(new CombatEvent
                {
                    Cue = CombatCue.BossTelegraph, Side = drillSide, Kind = PunchKind.None, Time = now,
                    Code = (int)(drillSide == PilotSide.Left ? BossAttackKind.PunchLeft : BossAttackKind.PunchRight),
                    BossHealth = combat.Authority.Resolver.Boss.Current,
                });
                combat.Flush();
                return;
            }
            if (drillPending && now >= drillAt)
            {
                drillPending = false;
                drillAt = now + drillRest;
                var need = drillSide == PilotSide.Left ? BossCounter.LeftGuard : BossCounter.RightGuard;
                var result = combat.Authority.ResolveBossStrike(need, drillSide, 0f, -1, now, combat.Events);
                if (DefenseRules.IsSuccess(result)) blocks++;
                combat.Flush();
                if (blocks >= drillBlocks) Go(CheckupStep.GunBrief, now);
            }
        }

        void Line(int line, double now) => Emit(CombatCue.CommanderLine, line, now, PilotSide.Left);

        void Emit(CombatCue cue, int code, double now, PilotSide side)
        {
            combat.Events.Add(new CombatEvent
            {
                Cue = cue, Side = side, Kind = PunchKind.None, Code = code, Time = now, BossHealth = combat.Authority.Resolver.Boss.Current,
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

        void ClientTick()
        {
            if (!IsSpawned || flow == null) return;
            var inCheckup = flow.Scene == MatchScene.Checkup;
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
        }

        void BuildPlates()
        {
            plates = new GameObject[plateOffsets.Length];
            flown = new bool[plateOffsets.Length];
            for (var i = 0; i < plates.Length; i++) plates[i] = MakePlate(i);
        }

        GameObject MakePlate(int i)
        {
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
                    // 트리거 → 철판이 날아간다.
                    flown[i] = true;
                    var body = plate.AddComponent<Rigidbody>();
                    body.mass = 50f;
                    body.linearVelocity = (player != null ? player.forward : Vector3.forward) * 40f + Vector3.up * 10f;
                    body.angularVelocity = Random.insideUnitSphere * 4f;
                }
                else if (flow.Scene != MatchScene.Checkup && plate.activeSelf) plate.SetActive(false);
            }
        }
    }
}
