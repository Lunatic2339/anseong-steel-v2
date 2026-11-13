using System;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Net;
using Anseong.Pilot;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>복제되는 드론 4칸(종류·상태·위치).</summary>
    public struct DroneNetState : INetworkSerializeByMemcpy, IEquatable<DroneNetState>
    {
        public byte K0, K1, K2, K3;
        public byte S0, S1, S2, S3;
        public Vector3 P0, P1, P2, P3;
        /// <summary>해킹 드론이 붙은 팔: 0 없음, 1 왼팔, 2 오른팔.</summary>
        public byte Hack;

        public DroneKind Kind(int i) => (DroneKind)(i == 0 ? K0 : i == 1 ? K1 : i == 2 ? K2 : K3);
        public DroneState State(int i) => (DroneState)(i == 0 ? S0 : i == 1 ? S1 : i == 2 ? S2 : S3);
        public Vector3 Position(int i) => i == 0 ? P0 : i == 1 ? P1 : i == 2 ? P2 : P3;

        public void Set(int i, DroneKind k, DroneState s, Vector3 p)
        {
            switch (i)
            {
                case 0: K0 = (byte)k; S0 = (byte)s; P0 = p; break;
                case 1: K1 = (byte)k; S1 = (byte)s; P1 = p; break;
                case 2: K2 = (byte)k; S2 = (byte)s; P2 = p; break;
                case 3: K3 = (byte)k; S3 = (byte)s; P3 = p; break;
            }
        }

        public bool Equals(DroneNetState o) =>
            K0 == o.K0 && K1 == o.K1 && K2 == o.K2 && K3 == o.K3 && S0 == o.S0 && S1 == o.S1 && S2 == o.S2 && S3 == o.S3 &&
            P0 == o.P0 && P1 == o.P1 && P2 == o.P2 && P3 == o.P3 && Hack == o.Hack;
        public override bool Equals(object obj) => obj is DroneNetState o && Equals(o);
        public override int GetHashCode() => S0 ^ (S1 << 2) ^ (S2 << 4) ^ (S3 << 6) ^ P0.GetHashCode();
    }

    /// <summary>
    /// 드론(M4-04). 서버가 장면에 맞춰 드론을 띄우고 상태·위치를 복제한다. 각 PC는 자기 팔로 판정해 보고만 한다.
    /// 장면 2: 보스 등 뒤에서 드론이 튀어나와 흩어져 사라진다(복선, 겉모습만).
    /// 장면 3: 숨은 드론 사냥. 물결 셋(연습 1기 → 2기 → 움직이는 3기). 숨은 드론은 안 보이고(아지랑이만) 쏠 때 총구가 번쩍한다.
    ///   왼팔 스캐너로 훑으면 드러나고, 드러난 동안 오른팔 포로 격추. 다 잡으면 사령관이 보스를 쏘라고 하고 포탄이 장갑에 튕긴다(3-4).
    /// 장면 4-2: 끼어드는 드론이 보스 등 뒤에서 날아와 숨는다. 장면 6: 달려드는 드론(경고음이 빨라짐, 쳐서 떨굼).
    /// 장면 8: 해킹 드론이 날아와 팔뚝에 붙는다 → 그 팔이 느려짐 → 동료가 반대 손으로 쥐고 당겨 뜯어낸다.
    /// 겉모습은 도형 + 선·궤적·파티클(AI 생성 없음). 성재 드론 모델이 오면 <see cref="dronePrefab"/>에.
    /// 메뉴 'Add Match Flow'가 Combat Server에 붙이고 재질 칸을 채운다.
    /// </summary>
    public class DroneDirector : NetworkBehaviour, ITunable
    {
        public const int Slots = 4;
        /// <summary>포 보고에서 "보스를 쐈다" / "아무것도 못 맞혔다"를 뜻하는 번호.</summary>
        const byte ShotAtBoss = 255, ShotAtNothing = 254;
        const int ShotCount = 10, DecoyCount = 6;

        [SerializeField] DroneSettings settings = new DroneSettings();

        [Header("드론 자리 (조종사 루트 기준 m: x 오른쪽, y 위, z 앞)")]
        [SerializeField, Tooltip("장면 3 첫 드론(연습, 크레인 꼭대기).")]
        Vector3 practiceOffset = new Vector3(-10f, 45f, 70f);
        [SerializeField, Tooltip("장면 3 둘째 물결 두 기(왼쪽 구조물 뒤, 오른쪽 하늘). 근접전 끼어들기에도 쓴다.")]
        Vector3[] pairOffsets = { new Vector3(-55f, 15f, 50f), new Vector3(45f, 55f, 40f) };
        [SerializeField, Tooltip("장면 3 셋째 물결(움직이는 드론). 보스 등 뒤에서 날아와 숨고 좌우로 오간다. 비우면 이 물결 없음.")]
        Vector3[] movingOffsets = { new Vector3(-42f, 48f, 62f), new Vector3(6f, 66f, 78f), new Vector3(52f, 28f, 56f) };
        [SerializeField, Tooltip("드론이 튀어나오는 보스 등 뒤 자리(보스 기준 m: y 위, z 음수 = 등 뒤).")]
        Vector3 bossBackOffset = new Vector3(0f, 45f, -14f);
        [SerializeField, Tooltip("달려드는 드론이 노리는 곳(조종사 루트 기준 m). x는 드론마다 좌우로 번갈아 뒤집힌다(두 사람이 번갈아 친다).")]
        Vector3 rushTargetOffset = new Vector3(9f, 30f, 4f);
        [SerializeField, Min(0f), Tooltip("드론 포탄이 날아와 맞는 높이(조종사 루트 위 m, 우리 로봇 가슴).")]
        float playerChestHeight = 38f;

        [Header("보스 몸통 (우리 포탄이 튕기는 자리)")]
        [SerializeField, Min(1f), Tooltip("보스 몸통 반지름(m). 조준선이 이 원통에 닿으면 장갑에 맞은 것.")]
        float bossBodyRadius = 13f;
        [SerializeField, Tooltip("보스 몸통 높이 범위(발밑에서 m: x 아래, y 위).")]
        Vector2 bossBodyHeights = new Vector2(8f, 60f);
        [SerializeField, Range(0f, 30f), Tooltip("조준선이 몸통을 살짝 벗어나도 보스 가슴에서 이 각도(°) 안이면 맞은 것으로 본다.")]
        float bossAimDegrees = 12f;

        [Header("겉모습 (비우면 도형)")]
        [SerializeField, Tooltip("드론 모델(성재). 비우면 구.")] GameObject dronePrefab;
        [SerializeField, Tooltip("도형 재질(색만 바꿔 쓴다). 메뉴가 넣는다.")] Material shapeMaterial;
        [SerializeField, Tooltip("불꽃·섬광·아지랑이 파티클 재질(더하기 섞기). 메뉴가 넣는다. 비면 불꽃·아지랑이 없이 도형·선·궤적만 나온다.")]
        Material glowMaterial;
        [SerializeField, Min(0.5f), Tooltip("드론 크기(m).")] float droneSize = 6f;
        [SerializeField, Tooltip("날아다니는(아직 안 드러난) 드론 색.")] Color hiddenColor = new Color(0.15f, 0.15f, 0.18f);
        [SerializeField, Tooltip("스캐너에 드러난 드론 색.")] Color revealedColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField, Tooltip("달려드는 드론 색.")] Color rushColor = new Color(1f, 0.35f, 0.05f);
        [SerializeField, Tooltip("해킹 드론·해킹 불꽃 색.")] Color hackColor = new Color(0.75f, 0.2f, 1f);
        [SerializeField, Tooltip("드론 포탄·궤적 색.")] Color droneShellColor = new Color(1f, 0.3f, 0.1f);
        [SerializeField, Tooltip("우리 포탄·궤적 색.")] Color cannonShellColor = new Color(1f, 0.9f, 0.45f);
        [SerializeField, Tooltip("숨은 드론 아지랑이 색(알파 = 평소 세기).")] Color hazeColor = new Color(0.6f, 0.85f, 1f, 0.07f);
        [SerializeField, Min(10f), Tooltip("스캐너 레이저 길이(m).")] float laserLength = 150f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly NetworkVariable<DroneNetState> net = new NetworkVariable<DroneNetState>();
        CombatServer combat;
        MatchFlow flow;
        BossBrain boss;
        Transform player;
        PilotHandInput input;
        PlayerWeaponVisuals weapons;
        readonly Transform[] hands = new Transform[2];
        readonly RobotArmDriver[] arms = new RobotArmDriver[2];

        // ───────── 서버 상태 ─────────
        enum HuntPhase { Waves, BossShot, Leaving, Done }

        readonly DroneKind[] kind = new DroneKind[Slots];
        readonly DroneState[] state = new DroneState[Slots];
        readonly Vector3[] position = new Vector3[Slots];
        readonly Vector3[] post = new Vector3[Slots];      // 머무는 자리(달려드는 드론은 안 씀)
        readonly Vector3[] origin = new Vector3[Slots];    // 날아오기 시작한 자리
        readonly bool[] moving = new bool[Slots];          // 자리에서 좌우로 오감
        readonly bool[] rushRight = new bool[Slots];       // 달려드는 드론이 노리는 쪽
        readonly double[] revealUntil = new double[Slots];
        readonly double[] spawnedAt = new double[Slots];
        readonly double[] moveStart = new double[Slots];
        readonly double[] shellAt = new double[Slots];     // 쏜 포탄이 닿는 시각(0 = 없음)
        readonly Vector3[] shellFrom = new Vector3[Slots];
        readonly double[] downAt = new double[Slots];      // 우리 포탄이 닿아 터지는 시각(0 = 없음)
        readonly double[] warnAt = new double[Slots];
        readonly bool[] canFire = new bool[Slots];
        MatchScene serverScene;
        double sceneStart, nextRush, nextPublish, nextVolley, phaseAt, ricochetAt, hackedAt;
        Vector3 ricochetPoint;
        HuntPhase huntPhase;
        int huntWave, interludesDone, hacksDone, lastCracks, lastShooter, hackSlot = -1, rushCount;
        bool interlude, saidNoGun, saidBounce, bossBounced, scattered;
        PilotSide hackVictim;
        PilotSide? hacked;

        // ───────── 모든 PC: 겉모습 ─────────
        sealed class Flyer
        {
            public GameObject Root, Head;
            public Renderer[] HeadRenderers;
            public TrailRenderer Trail;
            public Vector3 From, Velocity, Spin;
            public float Gravity, Start, Travel, Linger, Size, EndSize;
            public bool Active, Burst;
            public Color BurstColor;
        }

        GameObject visualRoot;
        readonly GameObject[] bodies = new GameObject[Slots];
        readonly Renderer[][] bodyRenderers = new Renderer[Slots][];
        readonly DroneState[] shownState = new DroneState[Slots];
        readonly DroneKind[] shownKind = new DroneKind[Slots];
        readonly float[] hiddenSince = new float[Slots];
        readonly float[] nextHaze = new float[Slots];
        readonly Vector3[] targets = new Vector3[Slots];
        readonly bool[] eligible = new bool[Slots];
        readonly float[] lastReport = new float[Slots];
        readonly Flyer[] shots = new Flyer[ShotCount];
        readonly Flyer[] decoys = new Flyer[DecoyCount];
        int nextShot, nextDecoy;
        LineRenderer laser, aimLine, tether;
        ParticleSystem sparks, glows;
        MaterialPropertyBlock block;
        readonly HackPull pull = new HackPull();
        bool triggerWasDown;
        float nextHackSpark;

        public override void OnNetworkSpawn()
        {
            combat = GetComponent<CombatServer>();
            flow = GetComponent<MatchFlow>();
            boss = FindAnyObjectByType<BossBrain>();
            input = FindAnyObjectByType<PilotHandInput>();
            weapons = FindAnyObjectByType<PlayerWeaponVisuals>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) player = seat.transform.root;
            foreach (var arm in FindObjectsByType<RobotArmDriver>(FindObjectsSortMode.None))
            {
                arms[(int)arm.Side] = arm;
                hands[(int)arm.Side] = arm.HandBone;
            }
            block = new MaterialPropertyBlock();
            hackSlot = -1;
            BuildVisuals();
            // 드론 감독이 있으니 장면 3은 시간으로 넘기지 않는다(MatchFlowRules.SceneSeconds).
            if (flow != null) flow.Settings.droneDirectorActive = true;
            if (combat != null && combat.Director != null) combat.Director.Cue += OnClientCue;
        }

        public override void OnNetworkDespawn()
        {
            if (combat != null && combat.Director != null) combat.Director.Cue -= OnClientCue;
            if (flow != null) flow.Settings.droneDirectorActive = false;
            for (var h = 0; h < 2; h++)
                if (arms[h] != null) arms[h].ResponseScale = 1f;
            if (visualRoot != null) Destroy(visualRoot);
            visualRoot = null;
            for (var i = 0; i < Slots; i++) bodies[i] = null;
            for (var i = 0; i < ShotCount; i++) shots[i] = null;
            for (var i = 0; i < DecoyCount; i++) decoys[i] = null;
            laser = aimLine = tether = null;
            sparks = glows = null;
        }

        void Update()
        {
            if (IsServer) ServerTick();
            if (IsSpawned) ClientTick();
        }

        // ───────── 서버 ─────────

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        void ServerTick()
        {
            if (flow == null || combat == null || combat.Authority == null) return;
            var now = Now;
            var scene = flow.Scene;
            if (scene != serverScene)
            {
                EnterScene(scene, now);
                serverScene = scene;
            }
            switch (scene)
            {
                case MatchScene.Entrance: Foreshadow(now); break;
                case MatchScene.DroneHunt: Hunt(now); break;
                case MatchScene.FightCannon: Interlude(now); break;
                case MatchScene.FightArmor: Rushers(now); break;
                case MatchScene.FightCore: Hackers(now); break;
            }
            Move(now);
            Fire(now);
            Ricochet(now);
            if (now >= nextPublish) Publish(now);
            if (combat.Events.Count > 0) combat.Flush();
        }

        void EnterScene(MatchScene scene, double now)
        {
            if (scene == MatchScene.Rebooting) return; // 재기동 뒤 같은 구간으로 돌아오면 다시 시작
            ClearAll();
            sceneStart = now;
            huntWave = 0;
            huntPhase = HuntPhase.Waves;
            bossBounced = scattered = false;
            ricochetAt = 0;
            interlude = false;
            nextRush = now + settings.rushInterval * 0.5f;
            if (scene == MatchScene.Waiting || scene == MatchScene.Checkup)
            {
                interludesDone = hacksDone = rushCount = 0;
                saidNoGun = saidBounce = false;
            }
            if (scene == MatchScene.FightCannon) lastCracks = Cracks();
            if (scene == MatchScene.DroneHunt)
            {
                // 3-1·3-2: 연습 드론은 이미 숨어 있다. 나오자마자 한 발 맞는다.
                Spawn(0, DroneKind.Cloaked, Offset(practiceOffset), now, false, false);
                nextVolley = now + settings.fireInterval * 0.5f;
            }
            flow.SetDronesActive(false);
            SetHacked(null, now);
            Publish(now);
        }

        /// <summary>장면 2 복선: 보스 등 뒤에서 드론 몇 기가 튀어나와 흩어져 사라진다(겉모습만, 각 PC가 그린다).</summary>
        void Foreshadow(double now)
        {
            if (scattered || settings.scatterCount <= 0 || now - sceneStart < settings.scatterDelay) return;
            scattered = true;
            Emit(CombatCue.DroneScatter, PilotSide.Left, now, BossBack(), settings.scatterCount, 0f);
        }

        /// <summary>장면 3: 물결 셋(연습 1기 → 2기 → 움직이는 3기) → 보스를 쏴 보기 → 끝.</summary>
        void Hunt(double now)
        {
            switch (huntPhase)
            {
                case HuntPhase.Waves:
                    if (now - sceneStart >= settings.huntTimeout)
                    {
                        // 남은 드론은 보스에게 돌아간다(막히지 않게).
                        Retreat(now);
                        huntPhase = HuntPhase.Leaving;
                        phaseAt = now + settings.enterSeconds;
                        return;
                    }
                    if (AnyAlive()) return;
                    huntWave++;
                    if (huntWave == 1 && pairOffsets != null && pairOffsets.Length > 0)
                    {
                        // 3-3: 서로 다른 방향에 이미 숨어 번갈아 쏜다.
                        Line(CommanderLines.FindAndShoot, now);
                        SpawnWave(pairOffsets, false, now);
                    }
                    else if (huntWave <= 2 && movingOffsets != null && movingOffsets.Length > 0)
                    {
                        // 셋째 물결: 보스 등 뒤에서 날아와 숨고, 좌우로 오간다.
                        huntWave = 2;
                        Line(CommanderLines.DronesAgain, now);
                        Emit(CombatCue.DroneScatter, PilotSide.Left, now, BossBack(), Mathf.Min(movingOffsets.Length, Slots), 1f);
                        SpawnWave(movingOffsets, true, now);
                    }
                    else if (saidBounce || settings.bossShotWindow <= 0f)
                    {
                        // 이미 보스를 쏴 봤다(장갑에 튕김): 바로 끝.
                        FinishHunt();
                    }
                    else
                    {
                        // 3-4: 보스를 쏴 보게 한다.
                        huntPhase = HuntPhase.BossShot;
                        phaseAt = now + settings.bossShotWindow;
                        Line(CommanderLines.ShootBoss, now);
                    }
                    break;
                case HuntPhase.BossShot:
                    if (ricochetAt > 0) return; // 포탄이 날아가는 중
                    if (now >= phaseAt) FinishHunt();
                    break;
                case HuntPhase.Leaving:
                    if (now < phaseAt) return;
                    ClearAll();
                    FinishHunt();
                    break;
            }
        }

        void FinishHunt()
        {
            huntPhase = HuntPhase.Done;
            flow.ServerSignal(MatchSignal.DronesCleared);
        }

        void SpawnWave(Vector3[] offsets, bool flyIn, double now)
        {
            for (var i = 0; i < offsets.Length && i < Slots; i++)
                Spawn(i, DroneKind.Cloaked, Offset(offsets[i]), now + i * 1.2, flyIn, flyIn);
            nextVolley = now + settings.fireInterval * 0.5f + (flyIn ? settings.enterSeconds : 0f);
            Publish(now);
        }

        /// <summary>남은 숨은 드론을 보스 등 뒤로 돌려보낸다.</summary>
        void Retreat(double now)
        {
            for (var i = 0; i < Slots; i++)
            {
                if (!Alive(i)) continue;
                state[i] = DroneState.Leaving;
                origin[i] = position[i];
                moveStart[i] = now;
                spawnedAt[i] = now;
                shellAt[i] = downAt[i] = 0;
            }
            Publish(now);
        }

        /// <summary>근접전 1구간: 보스 팔뚝 포에 금이 1개·2개 갈 때 드론 끼어들기(대본 4-2). 보스 등 뒤에서 날아와 숨는다.</summary>
        void Interlude(double now)
        {
            var cracks = Cracks();
            if (!interlude && cracks > lastCracks && interludesDone < 2 && pairOffsets != null && pairOffsets.Length > 0)
            {
                interlude = true;
                interludesDone++;
                Emit(CombatCue.DroneScatter, PilotSide.Left, now, BossBack(), Mathf.Min(pairOffsets.Length, Slots), 1f);
                for (var i = 0; i < pairOffsets.Length && i < Slots; i++)
                    Spawn(i, DroneKind.Cloaked, Offset(pairOffsets[i]), now + i * 1.2, true, false);
                nextVolley = now + settings.enterSeconds + settings.fireInterval * 0.5f;
                flow.SetDronesActive(true);
                Line(interludesDone == 2 ? CommanderLines.ShootCrane : CommanderLines.DronesAgain, now);
                Publish(now);
            }
            lastCracks = cracks;
            if (interlude && !AnyAlive())
            {
                interlude = false;
                flow.SetDronesActive(false);
                Line(CommanderLines.MeleeArms, now);
            }
        }

        /// <summary>검·방패만: 달려드는 드론(대본 6). 보스 등 뒤에서 떠서 좌우 번갈아 달려든다. 다가올수록 경고음이 빨라진다.</summary>
        void Rushers(double now)
        {
            if (now >= nextRush && boss != null && player != null)
            {
                nextRush = now + settings.rushInterval;
                var slot = FreeSlot();
                if (slot >= 0)
                {
                    Spawn(slot, DroneKind.Rusher, BossBack(), now, false, false);
                    state[slot] = DroneState.Revealed;
                    rushRight[slot] = rushCount % 2 == 1;
                    rushCount++;
                    Emit(CombatCue.DroneScatter, rushRight[slot] ? PilotSide.Right : PilotSide.Left, now, origin[slot], 1, 1f);
                    if (!saidNoGun)
                    {
                        saidNoGun = true;
                        Line(CommanderLines.NoGun, now);
                    }
                    Publish(now);
                }
            }
            for (var i = 0; i < Slots; i++)
            {
                if (kind[i] != DroneKind.Rusher || state[i] != DroneState.Revealed || player == null) continue;
                var t = Mathf.Clamp01((float)((now - moveStart[i]) / settings.rushSeconds));
                var aim = rushTargetOffset;
                if (!rushRight[i]) aim.x = -aim.x;
                position[i] = DroneRules.RushPosition(origin[i], Offset(aim), t, settings.rushArc);
                var side = rushRight[i] ? PilotSide.Right : PilotSide.Left;
                if (t >= 1f)
                {
                    // 못 쳐냈다: 들이받고 터진다.
                    Emit(CombatCue.DroneFire, side, now, position[i], i, 1f);
                    Hit(now, i, position[i]);
                    state[i] = DroneState.None;
                    Publish(now);
                    continue;
                }
                if (now >= warnAt[i])
                {
                    warnAt[i] = now + DroneRules.WarnInterval(t, settings.warnSlow, settings.warnFast);
                    Emit(CombatCue.DroneWarn, side, now, position[i], i, t);
                }
            }
        }

        /// <summary>맨손: 해킹 드론 두 번(대본 8). 보스 등 뒤에서 날아와 한쪽 팔뚝에 붙는다.</summary>
        void Hackers(double now)
        {
            if (hackSlot >= 0)
            {
                var i = hackSlot;
                if (state[i] == DroneState.Entering)
                {
                    var t = (float)((now - moveStart[i]) / settings.hackApproachSeconds);
                    position[i] = DroneRules.Fly(origin[i], AttachPoint(hackVictim), t);
                    if (t >= 1f)
                    {
                        state[i] = DroneState.Attached;
                        hackedAt = now;
                        SetHacked(hackVictim, now);
                        Publish(now);
                    }
                }
                else if (state[i] == DroneState.Attached || state[i] == DroneState.Grabbed)
                {
                    position[i] = AttachPoint(hackVictim);
                    // 끝까지 못 떼면 드론이 타서 떨어진다(막히지 않게).
                    if (settings.hackMaxSeconds > 0f && now - hackedAt >= settings.hackMaxSeconds) TearOff(now, hackVictim, 3f);
                }
                else hackSlot = -1;
                return;
            }
            if (settings.hackTimes == null || hacksDone >= settings.hackTimes.Length) return;
            if (now - sceneStart < settings.hackTimes[hacksDone]) return;
            var slot = FreeSlot();
            if (slot < 0) return;
            hacksDone++;
            hackVictim = UnityEngine.Random.value < 0.5f ? PilotSide.Left : PilotSide.Right;
            Spawn(slot, DroneKind.Hacker, AttachPoint(hackVictim), now, true, false);
            hackSlot = slot;
            Emit(CombatCue.DroneScatter, hackVictim, now, origin[slot], 1, 1f);
            Publish(now);
        }

        void SetHacked(PilotSide? side, double now)
        {
            if (hacked == side) return;
            var was = hacked;
            hacked = side;
            if (side.HasValue)
            {
                Emit(CombatCue.HackStart, side.Value, now, AttachPoint(side.Value), 0);
                Line(CommanderLines.Hacked, now);
            }
            else if (was.HasValue)
            {
                Emit(CombatCue.HackCleared, was.Value, now, AttachPoint(was.Value), 0);
                Line(CommanderLines.Restored, now);
            }
            combat.Flush();
        }

        /// <summary>해킹 드론이 팔에서 떨어져 나간다. amount = 2 동료가 뜯음, 3 타서 저절로 떨어짐.</summary>
        void TearOff(double now, PilotSide by, float amount)
        {
            if (hackSlot < 0) return;
            var i = hackSlot;
            hackSlot = -1;
            Down(i, now, by, amount);
            SetHacked(null, now);
        }

        /// <summary>날아오기·오가기·돌아가기(숨은 드론). 달려드는 드론·해킹 드론은 각자 함수에서 옮긴다.</summary>
        void Move(double now)
        {
            for (var i = 0; i < Slots; i++)
            {
                if (kind[i] != DroneKind.Cloaked || now < spawnedAt[i]) continue;
                switch (state[i])
                {
                    case DroneState.Entering:
                    {
                        var t = (float)((now - moveStart[i]) / settings.enterSeconds);
                        position[i] = DroneRules.Fly(origin[i], post[i], t);
                        if (t >= 1f)
                        {
                            // 자리에 닿으면 숨는다.
                            state[i] = DroneState.Hidden;
                            moveStart[i] = now;
                            Emit(CombatCue.DroneHidden, PilotSide.Left, now, position[i], i);
                            Publish(now);
                        }
                        break;
                    }
                    case DroneState.Hidden:
                    case DroneState.Revealed:
                        if (moving[i])
                            position[i] = DroneRules.Patrol(post[i], player != null ? player.right : Vector3.right,
                                settings.patrolAmplitude, settings.patrolPeriod, (float)(now - moveStart[i]) + i * 1.7f);
                        break;
                    case DroneState.Leaving:
                    {
                        var t = (float)((now - moveStart[i]) / settings.enterSeconds);
                        position[i] = DroneRules.Fly(origin[i], BossBack(), t);
                        if (t >= 1f)
                        {
                            state[i] = DroneState.None;
                            Publish(now);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>숨은 드론 사격: 떠 있는 드론들이 번갈아 한 발씩. 포탄이 날아와 닿는 순간 피해. 우리 포탄에 맞은 드론은 포탄이 닿을 때 터진다.</summary>
        void Fire(double now)
        {
            var any = false;
            for (var i = 0; i < Slots; i++)
            {
                canFire[i] = false;
                if (shellAt[i] > 0 && now >= shellAt[i])
                {
                    shellAt[i] = 0;
                    Hit(now, i, shellFrom[i]);
                }
                if (kind[i] != DroneKind.Cloaked || (state[i] != DroneState.Hidden && state[i] != DroneState.Revealed)) continue;
                if (now < spawnedAt[i]) continue;
                if (downAt[i] > 0)
                {
                    if (now < downAt[i]) continue;
                    downAt[i] = 0;
                    Down(i, now, PilotSide.Right, 0f);
                    continue;
                }
                if (state[i] == DroneState.Revealed && now >= revealUntil[i])
                {
                    state[i] = DroneState.Hidden;
                    Emit(CombatCue.DroneHidden, PilotSide.Left, now, position[i], i);
                    Publish(now);
                }
                canFire[i] = true;
                any = true;
            }
            if (!any || now < nextVolley) return;
            var shooter = DroneRules.NextShooter(canFire, lastShooter);
            if (shooter < 0) return;
            lastShooter = shooter;
            nextVolley = now + settings.fireInterval;
            shellFrom[shooter] = position[shooter];
            shellAt[shooter] = now + Mathf.Min(settings.shellSeconds, settings.fireInterval * 0.9f);
            Emit(CombatCue.DroneFire, PilotSide.Left, now, position[shooter], shooter);
        }

        /// <summary>우리 포탄이 보스 장갑에 닿는 순간: 튕김 사건 + 사령관(대본 3-4).</summary>
        void Ricochet(double now)
        {
            if (ricochetAt <= 0 || now < ricochetAt) return;
            ricochetAt = 0;
            Emit(CombatCue.CannonRicochet, PilotSide.Right, now, ricochetPoint, 0);
            if (!saidBounce)
            {
                saidBounce = true;
                Line(CommanderLines.ArmorBounces, now);
            }
            if (serverScene == MatchScene.DroneHunt && huntPhase == HuntPhase.BossShot && !bossBounced)
            {
                bossBounced = true;
                phaseAt = now + settings.bossShotAfter;
            }
        }

        void Hit(double now, int i, Vector3 from)
        {
            if (player == null) return;
            var side = Vector3.Dot(from - player.position, player.right) < 0f ? PilotSide.Left : PilotSide.Right;
            combat.Authority.ResolveBossStrike(BossCounter.AlternateGuard, side, settings.fireDamage, 100 + i, now, combat.Events);
            combat.Flush();
        }

        /// <param name="at">자리(머물 곳). 날아오는 드론(flyIn)은 보스 등 뒤에서 여기로 온다.</param>
        /// <param name="when">나타나는 시각(물결 안에서 조금씩 늦춘다).</param>
        void Spawn(int i, DroneKind k, Vector3 at, double when, bool flyIn, bool patrol)
        {
            kind[i] = k;
            state[i] = flyIn ? DroneState.Entering : DroneState.Hidden;
            post[i] = at;
            origin[i] = flyIn || k == DroneKind.Rusher ? BossBack() : at;
            position[i] = origin[i];
            moving[i] = patrol;
            spawnedAt[i] = when;
            moveStart[i] = when;
            warnAt[i] = when;
            revealUntil[i] = 0;
            shellAt[i] = downAt[i] = 0;
        }

        void ClearAll()
        {
            for (var i = 0; i < Slots; i++)
            {
                state[i] = DroneState.None;
                shellAt[i] = downAt[i] = 0;
            }
            hackSlot = -1;
        }

        bool Alive(int i) => state[i] == DroneState.Hidden || state[i] == DroneState.Revealed || state[i] == DroneState.Entering;

        bool AnyAlive()
        {
            for (var i = 0; i < Slots; i++)
                if (Alive(i)) return true;
            return false;
        }

        int FreeSlot()
        {
            for (var i = 0; i < Slots; i++)
                if (state[i] == DroneState.None || state[i] == DroneState.Down) return i;
            return -1;
        }

        Vector3 Offset(Vector3 local) => player != null ? player.position + player.rotation * local : local;

        /// <summary>드론이 튀어나오는 보스 등 뒤 자리(월드).</summary>
        Vector3 BossBack() => boss != null
            ? boss.transform.position + boss.transform.rotation * bossBackOffset
            : Offset(new Vector3(0f, bossBackOffset.y, 120f));

        /// <summary>우리 로봇 가슴(월드): 드론 포탄이 날아와 맞는 곳.</summary>
        Vector3 PlayerChest() => player != null ? player.position + Vector3.up * playerChestHeight : Vector3.up * playerChestHeight;

        /// <summary>해킹 드론이 붙는 자리(그 팔 팔뚝 가운데, 월드). 모든 PC가 자기 화면의 로봇 뼈로 계산한다.</summary>
        Vector3 AttachPoint(PilotSide side)
        {
            var arm = arms[(int)side];
            if (arm != null && arm.ForeArmBone != null && arm.HandBone != null)
                return Vector3.Lerp(arm.ForeArmBone.position, arm.HandBone.position, 0.5f);
            return PlayerChest();
        }

        /// <summary>우리 포탄이 포구에서 point까지 날아가는 시간(초).</summary>
        float ShellDelay(Vector3 point)
        {
            var tip = weapons != null ? weapons.CannonTip : null;
            var from = tip != null ? tip.position : PlayerChest();
            return (point - from).magnitude / Mathf.Max(1f, settings.cannonShellSpeed);
        }

        int Cracks()
        {
            if (boss == null) return 0;
            var b = combat.Authority.Resolver.Boss;
            var hp = b.Max > 0f ? b.Current / b.Max * 100f : 0f;
            return BossWeaponRules.Current(hp, boss.Thresholds) == BossWeapon.ArmCannon ? BossWeaponRules.CrackCount(hp, boss.Thresholds) : 2;
        }

        void Publish(double now)
        {
            nextPublish = now + 0.05;
            var s = new DroneNetState();
            for (var i = 0; i < Slots; i++)
            {
                var visible = state[i] != DroneState.None && now >= spawnedAt[i];
                s.Set(i, kind[i], visible ? state[i] : DroneState.None, position[i]);
            }
            if (hackSlot >= 0 && (state[hackSlot] == DroneState.Attached || state[hackSlot] == DroneState.Grabbed))
                s.Hack = (byte)((int)hackVictim + 1);
            if (!net.Value.Equals(s)) net.Value = s;
        }

        void Line(int line, double now) => Emit(CombatCue.CommanderLine, PilotSide.Left, now, Vector3.zero, line);

        void Emit(CombatCue cue, PilotSide side, double now, Vector3 point, int code, float amount = 0f)
        {
            combat.Events.Add(new CombatEvent
            {
                Cue = cue, Side = side, Kind = PunchKind.None, Code = code, Amount = amount, Time = now, Point = point,
                BossHealth = combat.Authority.Resolver.Boss.Current,
            });
        }

        // 보고(각 PC → 서버)

        [Rpc(SendTo.Server)]
        void RevealRpc(byte i)
        {
            if (combat == null || combat.Authority == null) return;
            if (i >= Slots || kind[i] != DroneKind.Cloaked || state[i] != DroneState.Hidden || Now < spawnedAt[i]) return;
            var now = Now;
            state[i] = DroneState.Revealed;
            revealUntil[i] = now + settings.revealSeconds;
            Emit(CombatCue.DroneRevealed, PilotSide.Left, now, position[i], i);
            if (flow.Scene == MatchScene.DroneHunt && huntWave == 0) Line(CommanderLines.Found, now);
            combat.Flush();
            Publish(now);
        }

        [Rpc(SendTo.Server)]
        void ShootRpc(byte i, Vector3 aim)
        {
            if (combat == null || combat.Authority == null) return;
            var now = Now;
            if (i == ShotAtBoss)
            {
                // 보스를 쐈다: 포탄이 닿으면 장갑에 튕긴다(대본 3-4).
                Emit(CombatCue.CannonFire, PilotSide.Right, now, aim, 0, 1f);
                ricochetAt = now + ShellDelay(aim);
                ricochetPoint = aim;
                combat.Flush();
                return;
            }
            Emit(CombatCue.CannonFire, PilotSide.Right, now, aim, i);
            if (i < Slots && state[i] == DroneState.Revealed && kind[i] == DroneKind.Cloaked && downAt[i] <= 0)
            {
                // 포탄이 닿을 때 터진다. 그때까지 다시 숨지 않는다.
                downAt[i] = now + ShellDelay(position[i]);
                revealUntil[i] = Math.Max(revealUntil[i], downAt[i] + 0.2);
            }
            combat.Flush();
        }

        [Rpc(SendTo.Server)]
        void PunchDroneRpc(byte i, byte side)
        {
            if (combat == null || combat.Authority == null) return;
            if (i >= Slots || kind[i] != DroneKind.Rusher || state[i] != DroneState.Revealed) return;
            Down(i, Now, side == (byte)PilotSide.Right ? PilotSide.Right : PilotSide.Left, 1f);
            combat.Flush();
        }

        /// <summary>동료 손이 해킹 드론을 붙잡았다/놓았다.</summary>
        [Rpc(SendTo.Server)]
        void HackGrabRpc(bool held, byte helper)
        {
            if (combat == null || combat.Authority == null || hackSlot < 0) return;
            var i = hackSlot;
            var now = Now;
            if (held && state[i] == DroneState.Attached)
            {
                state[i] = DroneState.Grabbed;
                Emit(CombatCue.HackGrab, helper == (byte)PilotSide.Right ? PilotSide.Right : PilotSide.Left, now, position[i], i);
                combat.Flush();
            }
            else if (!held && state[i] == DroneState.Grabbed) state[i] = DroneState.Attached;
            Publish(now);
        }

        /// <summary>동료가 쥐고 당겨 뜯어냈다.</summary>
        [Rpc(SendTo.Server)]
        void HackClearRpc(byte helper)
        {
            if (combat == null || combat.Authority == null || hackSlot < 0) return;
            if (state[hackSlot] != DroneState.Attached && state[hackSlot] != DroneState.Grabbed) return;
            var by = helper == (byte)PilotSide.Right ? PilotSide.Right : PilotSide.Left;
            // 뜯긴 드론은 잡은 손에서 날아간다.
            if (hands[(int)by] != null) position[hackSlot] = hands[(int)by].position;
            TearOff(Now, by, 2f);
        }

        /// <param name="amount">0 포에 격추, 1 주먹·검에 맞아 튕겨 나감, 2 동료가 뜯어냄, 3 타서 떨어짐(해킹 드론).</param>
        void Down(int i, double now, PilotSide by, float amount)
        {
            state[i] = DroneState.Down;
            shellAt[i] = downAt[i] = 0;
            Emit(CombatCue.DroneDown, by, now, position[i], i, amount);
            if (amount < 2.5f) flow.ServerDroneDown();
            Publish(now);
        }

        // ───────── 모든 PC: 겉모습·판정 보고 ─────────

        void OnClientCue(CombatEvent e)
        {
            if (visualRoot == null) return;
            switch (e.Cue)
            {
                case CombatCue.DroneFire:
                    if (e.Amount < 0.5f)
                    {
                        // 숨은 드론이 쐈다: 총구가 번쩍(찾기 힌트) + 빛나는 탄이 우리 가슴으로 날아온다.
                        Glow(e.Point, droneShellColor, droneSize * 2.5f, 0.25f);
                        var to = PlayerChest();
                        var seconds = Mathf.Max(0.05f, Mathf.Min(settings.shellSeconds, settings.fireInterval * 0.9f));
                        Launch(shots, ref nextShot, e.Point, (to - e.Point) / seconds, 0f, seconds, 0.5f,
                            droneSize * 0.45f, droneSize * 0.45f, droneShellColor, false, Vector3.zero);
                    }
                    else
                    {
                        // 달려든 드론이 들이받고 터졌다.
                        Glow(e.Point, rushColor, droneSize * 3f, 0.3f);
                        Burst(e.Point, rushColor, 30, 45f, Vector3.zero);
                    }
                    break;
                case CombatCue.CannonFire:
                {
                    // 우리 포탄: 포구에서 조준점까지 빛나는 탄 + 궤적.
                    var tip = weapons != null ? weapons.CannonTip : null;
                    var from = tip != null ? tip.position : PlayerChest();
                    var seconds = Mathf.Max(0.03f, (e.Point - from).magnitude / Mathf.Max(1f, settings.cannonShellSpeed));
                    Glow(from, cannonShellColor, droneSize * 1.5f, 0.15f);
                    Launch(shots, ref nextShot, from, (e.Point - from) / seconds, 0f, seconds, 0.45f,
                        droneSize * 0.35f, droneSize * 0.35f, cannonShellColor, false, Vector3.zero);
                    break;
                }
                case CombatCue.CannonRicochet:
                {
                    // 보스 장갑에 튕김: 명중점에서 불꽃 + 튕겨 나가는 탄 궤적.
                    var tip = weapons != null ? weapons.CannonTip : null;
                    var from = tip != null ? tip.position : PlayerChest();
                    var normal = boss != null ? e.Point - boss.transform.position : from - e.Point;
                    normal.y = 0f;
                    var away = DroneRules.Ricochet(e.Point - from, normal);
                    Glow(e.Point, cannonShellColor, droneSize * 2.2f, 0.2f);
                    Burst(e.Point, cannonShellColor, 36, 55f, away * 0.8f);
                    Launch(shots, ref nextShot, e.Point, away * 130f, 25f, 1.1f, 0.5f,
                        droneSize * 0.3f, droneSize * 0.15f, cannonShellColor, false, Vector3.zero);
                    break;
                }
                case CombatCue.DroneDown:
                {
                    var away = e.Point - PlayerChest();
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
                    if (e.Amount > 0.5f && e.Amount < 2.5f)
                    {
                        // 쳐냈다·뜯어냈다: 드론이 튕겨 날아가다 터진다.
                        var color = e.Amount > 1.5f ? hackColor : rushColor;
                        var size = droneSize * (e.Amount > 1.5f ? 0.5f : 1f);
                        Burst(e.Point, color, 16, 35f, away * 0.5f);
                        var flyer = Launch(decoys, ref nextDecoy, e.Point, away * 70f + Vector3.up * 28f, 40f, 1.2f, 0.6f,
                            size, size * 0.8f, color, true, new Vector3(540f, 200f, 90f));
                        if (flyer != null) flyer.BurstColor = color;
                    }
                    else Burst(e.Point, e.Amount > 2.5f ? hackColor : droneShellColor, 28, 40f, Vector3.zero);
                    break;
                }
                case CombatCue.DroneScatter:
                    if (e.Amount < 0.5f)
                    {
                        // 장면 2 복선: 등 뒤에서 튀어나와 사방으로 흩어지며 줄어들어 사라진다.
                        var back = boss != null ? -boss.transform.forward : Vector3.forward;
                        var right = boss != null ? boss.transform.right : Vector3.right;
                        var count = Mathf.Min(e.Code, DecoyCount);
                        Glow(e.Point, revealedColor, droneSize * 2f, 0.3f);
                        for (var k = 0; k < count; k++)
                            Launch(decoys, ref nextDecoy, e.Point, DroneRules.ScatterDirection(k, count, back, right) * (48f + 6f * k),
                                0f, 2.4f + 0.15f * k, 0.3f, droneSize, droneSize * 0.03f, hiddenColor, false, new Vector3(0f, 180f, 0f));
                    }
                    else Glow(e.Point, revealedColor, droneSize * 1.5f, 0.3f);
                    break;
                case CombatCue.HackStart:
                    Burst(e.Point, hackColor, 24, 30f, Vector3.zero);
                    break;
            }
        }

        void BuildVisuals()
        {
            if (visualRoot != null) Destroy(visualRoot);
            visualRoot = new GameObject("Drone Visuals");
            var root = visualRoot.transform;
            for (var i = 0; i < Slots; i++)
            {
                var go = MakeBody("Drone " + i, root);
                go.transform.localScale = Vector3.one * droneSize;
                bodyRenderers[i] = go.GetComponentsInChildren<Renderer>(true);
                go.SetActive(false);
                bodies[i] = go;
                shownState[i] = DroneState.None;
            }
            for (var i = 0; i < ShotCount; i++) shots[i] = MakeFlyer("Shell " + i, root, false, 0.4f);
            for (var i = 0; i < DecoyCount; i++) decoys[i] = MakeFlyer("Drone Flyer " + i, root, true, 0.7f);
            laser = MakeLine("Scanner Laser", root, 0.4f, new Color(0.2f, 1f, 0.6f));
            aimLine = MakeLine("Cannon Aim", root, 0.25f, new Color(1f, 0.25f, 0.15f));
            tether = MakeLine("Hack Tether", root, 0.6f, hackColor);
            if (glowMaterial != null)
            {
                sparks = MakeParticles("Drone Sparks", root, 3f, true);
                glows = MakeParticles("Drone Glows", root, 0f, false);
            }
        }

        /// <summary>드론 몸(모델이 있으면 모델, 없으면 구).</summary>
        GameObject MakeBody(string label, Transform parent)
        {
            GameObject go;
            if (dronePrefab != null) go = Instantiate(dronePrefab, parent);
            else go = MakeSphere(parent);
            go.name = label;
            return go;
        }

        GameObject MakeSphere(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (shapeMaterial != null) r.sharedMaterial = shapeMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            go.transform.SetParent(parent, false);
            return go;
        }

        LineRenderer MakeLine(string label, Transform parent, float width, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            // 선·궤적은 도형 재질에 색만 입힌다(재질 속성 블록).
            if (shapeMaterial != null) line.sharedMaterial = shapeMaterial;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Tint(line, color);
            line.enabled = false;
            return line;
        }

        /// <summary>날아가는 것 하나(포탄 = 빛나는 구 + 궤적, 드론 = 드론 몸 + 궤적). 미리 만들어 돌려 쓴다.</summary>
        Flyer MakeFlyer(string label, Transform parent, bool droneBody, float trailSeconds)
        {
            var rootObject = new GameObject(label);
            rootObject.transform.SetParent(parent, false);
            var head = droneBody ? MakeBody("Head", rootObject.transform) : MakeSphere(rootObject.transform);
            head.name = "Head";
            var trail = rootObject.AddComponent<TrailRenderer>();
            trail.time = trailSeconds;
            trail.minVertexDistance = 0.5f;
            trail.widthMultiplier = 1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            if (shapeMaterial != null) trail.sharedMaterial = shapeMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            rootObject.SetActive(false);
            return new Flyer { Root = rootObject, Head = head, HeadRenderers = head.GetComponentsInChildren<Renderer>(true), Trail = trail };
        }

        /// <summary>한 번씩 터뜨려 쓰는 파티클(불꽃 = 중력 받는 늘어난 점, 빛 = 제자리에서 사라지는 점).</summary>
        ParticleSystem MakeParticles(string label, Transform parent, float gravity, bool stretched)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize = 1f;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretched)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.03f;
                r.lengthScale = 1.5f;
            }
            ps.Play();
            return ps;
        }

        /// <summary>제자리에서 번쩍하고 사라지는 빛 하나(총구 섬광, 아지랑이).</summary>
        void Glow(Vector3 at, Color color, float size, float seconds)
        {
            if (glows == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = at, velocity = Vector3.zero, startColor = color, startSize = size, startLifetime = seconds,
            };
            glows.Emit(p, 1);
        }

        /// <summary>사방으로 튀는 불꽃. push = 한쪽으로 쏠리는 방향(0이면 고르게).</summary>
        void Burst(Vector3 at, Color color, int count, float speed, Vector3 push)
        {
            if (sparks == null) return;
            for (var n = 0; n < count; n++)
            {
                var dir = (UnityEngine.Random.onUnitSphere + push * 1.5f).normalized;
                var p = new ParticleSystem.EmitParams
                {
                    position = at, velocity = dir * (speed * UnityEngine.Random.Range(0.35f, 1f)), startColor = color,
                    startSize = droneSize * UnityEngine.Random.Range(0.08f, 0.2f), startLifetime = UnityEngine.Random.Range(0.3f, 0.9f),
                };
                sparks.Emit(p, 1);
            }
        }

        Flyer Launch(Flyer[] pool, ref int next, Vector3 from, Vector3 velocity, float gravity, float travel, float linger,
            float size, float endSize, Color color, bool burst, Vector3 spin)
        {
            var f = pool[next];
            next = (next + 1) % pool.Length;
            if (f == null || f.Root == null) return null;
            f.From = from;
            f.Velocity = velocity;
            f.Gravity = gravity;
            f.Start = Time.time;
            f.Travel = Mathf.Max(0.01f, travel);
            f.Linger = linger;
            f.Size = size;
            f.EndSize = endSize;
            f.Burst = burst;
            f.BurstColor = color;
            f.Spin = spin;
            f.Active = true;
            f.Root.transform.position = from;
            f.Root.SetActive(true);
            f.Head.SetActive(true);
            f.Head.transform.localScale = Vector3.one * size;
            Tint(f.HeadRenderers, color);
            if (f.Trail != null)
            {
                f.Trail.Clear();
                f.Trail.widthMultiplier = Mathf.Max(0.3f, size * 0.7f);
                f.Trail.startColor = color;
                f.Trail.endColor = new Color(color.r, color.g, color.b, 0f);
                Tint(f.Trail, color);
            }
            return f;
        }

        void TickFlyers(Flyer[] pool, float dt)
        {
            for (var i = 0; i < pool.Length; i++)
            {
                var f = pool[i];
                if (f == null || !f.Active || f.Root == null) continue;
                var elapsed = Time.time - f.Start;
                var t = Mathf.Min(elapsed, f.Travel);
                var at = f.From + f.Velocity * t + Vector3.down * (0.5f * f.Gravity * t * t);
                f.Root.transform.position = at;
                if (elapsed < f.Travel)
                {
                    var k = t / f.Travel;
                    f.Head.transform.localScale = Vector3.one * Mathf.Lerp(f.Size, f.EndSize, k * k);
                    if (f.Spin != Vector3.zero) f.Head.transform.Rotate(f.Spin * dt, Space.Self);
                    continue;
                }
                if (f.Head.activeSelf)
                {
                    // 다 날아갔다: 머리는 지우고 궤적만 남겨 사라지게 한다.
                    f.Head.SetActive(false);
                    if (f.Burst)
                    {
                        Glow(at, f.BurstColor, droneSize * 3f, 0.3f);
                        Burst(at, f.BurstColor, 30, 45f, Vector3.zero);
                    }
                }
                if (elapsed >= f.Travel + f.Linger)
                {
                    f.Active = false;
                    f.Root.SetActive(false);
                }
            }
        }

        void Tint(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) Tint(renderers[i], color);
        }

        void Tint(Renderer r, Color color)
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
        }

        /// <summary>상태가 바뀔 때만: 숨으면 몸을 끄고(아지랑이만), 드러나면 붉게, 날아다닐 땐 어둡게.</summary>
        void ApplyLook(int i, DroneKind k, DroneState st)
        {
            var visible = st != DroneState.Hidden;
            var renderers = bodyRenderers[i];
            if (renderers != null)
                for (var n = 0; n < renderers.Length; n++)
                    if (renderers[n] != null) renderers[n].enabled = visible;
            Color color;
            float scale;
            if (k == DroneKind.Hacker)
            {
                color = hackColor;
                scale = 0.5f;
            }
            else if (st == DroneState.Revealed)
            {
                color = k == DroneKind.Rusher ? rushColor : revealedColor;
                scale = 1f;
            }
            else
            {
                color = hiddenColor;
                scale = 0.6f;
            }
            Tint(renderers, color);
            bodies[i].transform.localScale = Vector3.one * droneSize * scale;
            if (st == DroneState.Hidden) hiddenSince[i] = Time.time;
        }

        void ClientTick()
        {
            if (visualRoot == null) return;
            var s = net.Value;
            var dt = Time.deltaTime;
            var hackOn = s.Hack != 0;
            var victim = s.Hack == 2 ? PilotSide.Right : PilotSide.Left;
            var helper = victim == PilotSide.Left ? PilotSide.Right : PilotSide.Left;
            var hackBody = -1;
            var grabbed = false;

            for (var i = 0; i < Slots; i++)
            {
                var st = s.State(i);
                var k = s.Kind(i);
                var on = st != DroneState.None && st != DroneState.Down;
                var body = bodies[i];
                if (body == null) continue;
                if (body.activeSelf != on) body.SetActive(on);
                targets[i] = s.Position(i);
                if (!on)
                {
                    shownState[i] = DroneState.None;
                    continue;
                }
                var first = shownState[i] == DroneState.None;
                if (first || shownState[i] != st || shownKind[i] != k)
                {
                    shownState[i] = st;
                    shownKind[i] = k;
                    ApplyLook(i, k, st);
                }
                if (k == DroneKind.Hacker && hackOn && (st == DroneState.Attached || st == DroneState.Grabbed))
                {
                    // 팔에 붙은 드론은 이 PC 화면의 로봇 뼈를 따라간다. 붙잡히면 동료 손에.
                    hackBody = i;
                    grabbed = st == DroneState.Grabbed && hands[(int)helper] != null;
                    body.transform.position = grabbed ? hands[(int)helper].position : AttachPoint(victim);
                }
                else if (first) body.transform.position = targets[i];
                else body.transform.position = Vector3.Lerp(body.transform.position, targets[i], 1f - Mathf.Exp(-dt * 12f));

                // 숨은 드론: 몸은 안 보이고 아지랑이만. 오래 못 찾으면 진해진다(대본: 막히지 않게).
                if (st == DroneState.Hidden && Time.time >= nextHaze[i])
                {
                    nextHaze[i] = Time.time + 0.18f;
                    var hint = Time.time - hiddenSince[i] > settings.hintAfterSeconds;
                    var c = hazeColor;
                    if (hint) c.a = Mathf.Min(1f, c.a * 5f);
                    Glow(body.transform.position + UnityEngine.Random.insideUnitSphere * (droneSize * 0.35f), c, droneSize * (hint ? 1.6f : 1.1f), 0.7f);
                }
            }

            TickHack(hackOn && hackBody >= 0, hackBody, grabbed, victim, helper, dt);
            TickFlyers(shots, dt);
            TickFlyers(decoys, dt);

            if (input == null)
            {
                laser.enabled = aimLine.enabled = false;
                return;
            }
            var loadout = flow != null && flow.Running ? flow.Loadout : Loadout.Bare;

            // 왼팔 스캐너: 숨은 드론을 훑으면 드러남.
            var scanTip = weapons != null ? weapons.ScannerTip : null;
            var scanning = loadout == Loadout.Ranged && input.IsArmHand(PilotSide.Left) && scanTip != null && scanTip.gameObject.activeInHierarchy;
            laser.enabled = scanning;
            if (scanning)
            {
                var o = scanTip.position;
                var d = scanTip.up;
                laser.SetPosition(0, o);
                laser.SetPosition(1, o + d * laserLength);
                for (var i = 0; i < Slots; i++) eligible[i] = s.State(i) == DroneState.Hidden && s.Kind(i) == DroneKind.Cloaked;
                var hit = DroneRules.Pick(o, d, targets, eligible, settings.scanDegrees);
                if (hit >= 0 && Time.time - lastReport[hit] > 0.5f)
                {
                    lastReport[hit] = Time.time;
                    RevealRpc((byte)hit);
                }
            }

            // 오른팔 포: 드러난 드론을 겨누면 조준선이 붙는다. 트리거 → 격추, 보스를 겨누면 장갑에 튕김.
            var cannonTip = weapons != null ? weapons.CannonTip : null;
            var aiming = loadout == Loadout.Ranged && input.IsArmHand(PilotSide.Right) && cannonTip != null && cannonTip.gameObject.activeInHierarchy;
            var locked = -1;
            if (aiming)
            {
                for (var i = 0; i < Slots; i++) eligible[i] = s.State(i) == DroneState.Revealed && s.Kind(i) == DroneKind.Cloaked;
                locked = DroneRules.Pick(cannonTip.position, cannonTip.up, targets, eligible, settings.aimDegrees);
            }
            aimLine.enabled = locked >= 0;
            if (locked >= 0)
            {
                aimLine.SetPosition(0, cannonTip.position);
                aimLine.SetPosition(1, bodies[locked] != null ? bodies[locked].transform.position : targets[locked]);
            }
            var trigger = input.IsArmHand(PilotSide.Right) && input.Get(PilotSide.Right).Trigger > 0.7f;
            if (trigger && !triggerWasDown && aiming)
            {
                var o = cannonTip.position;
                var d = cannonTip.up;
                if (locked >= 0) ShootRpc((byte)locked, targets[locked]);
                else if (BossHit(o, d, out var bossPoint)) ShootRpc(ShotAtBoss, bossPoint);
                else ShootRpc(ShotAtNothing, o + d * 300f);
            }
            triggerWasDown = trigger;

            // 주먹·검: 달려드는 드론에 닿으면 쳐서 떨군다.
            for (var h = 0; h < 2; h++)
            {
                var hand = hands[h];
                if (hand == null || !input.IsArmHand((PilotSide)h)) continue;
                for (var i = 0; i < Slots; i++)
                {
                    if (s.Kind(i) != DroneKind.Rusher || s.State(i) != DroneState.Revealed || bodies[i] == null) continue;
                    if ((hand.position - bodies[i].transform.position).magnitude > settings.punchRadius) continue;
                    if (Time.time - lastReport[i] < 0.3f) continue;
                    lastReport[i] = Time.time;
                    PunchDroneRpc((byte)i, (byte)h);
                }
            }
        }

        /// <summary>조준선이 보스 몸통에 닿는지(조금 벗어나도 가슴 근처면 인정). point = 장갑 겉면 명중점(보스 몸 높이).</summary>
        bool BossHit(Vector3 origin, Vector3 dir, out Vector3 point)
        {
            point = origin + dir * 100f;
            if (boss == null) return false;
            var feet = boss.transform.position;
            if (DroneRules.BossHit(origin, dir, feet, bossBodyHeights.x, bossBodyHeights.y, bossBodyRadius, out point)) return true;
            var chest = feet + Vector3.up * ((bossBodyHeights.x + bossBodyHeights.y) * 0.5f);
            return DroneRules.AngleTo(origin, dir, chest) <= bossAimDegrees;
        }

        /// <summary>
        /// 해킹(대본 8): 붙은 팔을 느리게(그 팔을 모는 PC에서 실제로 적용), 붙은 자리에 보라색 빛·불꽃,
        /// 동료 팔이 이 PC에 있으면 "가까이에서 쥐고 → 당김"을 판정해 서버에 보고한다.
        /// </summary>
        void TickHack(bool attached, int slot, bool grabbed, PilotSide victim, PilotSide helper, float dt)
        {
            for (var h = 0; h < 2; h++)
                if (arms[h] != null) arms[h].ResponseScale = attached && h == (int)victim ? settings.hackArmSlow : 1f;
            if (!attached)
            {
                pull.Reset();
                tether.enabled = false;
                return;
            }
            var at = AttachPoint(victim);
            if (Time.time >= nextHackSpark)
            {
                nextHackSpark = Time.time + 0.08f;
                Glow(at, hackColor, droneSize * 1.1f, 0.3f);
                Burst(at, hackColor, 2, 16f, Vector3.zero);
            }
            tether.enabled = grabbed;
            if (grabbed)
            {
                tether.SetPosition(0, at);
                tether.SetPosition(1, bodies[slot].transform.position);
            }
            var hand = hands[(int)helper];
            if (input == null || hand == null || !input.IsArmHand(helper))
            {
                pull.Reset();
                return;
            }
            var was = pull.Holding;
            var torn = pull.Step(input.Get(helper).GripHeld, (hand.position - at).magnitude, dt, settings);
            if (torn) HackClearRpc((byte)helper);
            else if (pull.Holding != was) HackGrabRpc(pull.Holding, (byte)helper);
        }

        public void AddTuning(TuningList list)
        {
            const string g = "드론";
            list.Describe(g, "왼팔 스캐너로 훑으면 드러남(3초), 오른팔 트리거로 쏨. 달려드는 드론은 주먹·검으로 쳐냄.\n해킹 드론은 동료가 반대 손을 드론 가까이 대고 그립을 쥔 뒤 당겨서 뜯음");
            list.AddFloat(g, "drone.scan", "스캐너 맞힘 각도(°)", () => settings.scanDegrees, v => settings.scanDegrees = v, 2f, 20f, 1f, 0);
            list.AddFloat(g, "drone.aim", "포 조준 붙는 각도(°)", () => settings.aimDegrees, v => settings.aimDegrees = v, 2f, 25f, 1f, 0);
            list.AddFloat(g, "drone.punch", "달려드는 드론 쳐내는 거리(m)", () => settings.punchRadius, v => settings.punchRadius = v, 4f, 20f, 1f, 0);
            list.AddFloat(g, "drone.hackGrab", "해킹 드론 붙잡는 거리(m)", () => settings.hackGrabRadius, v => settings.hackGrabRadius = v, 4f, 20f, 1f, 0);
            list.AddFloat(g, "drone.hackPull", "해킹 드론 뜯기는 거리(m)", () => settings.hackPullDistance, v => settings.hackPullDistance = v, 8f, 30f, 1f, 0);
            list.AddFloat(g, "drone.hackSlow", "해킹된 팔 반응 배율", () => settings.hackArmSlow, v => settings.hackArmSlow = v, 0.1f, 1f, 0.05f, 2);
        }
    }
}
