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
            P0 == o.P0 && P1 == o.P1 && P2 == o.P2 && P3 == o.P3;
        public override bool Equals(object obj) => obj is DroneNetState o && Equals(o);
        public override int GetHashCode() => S0 ^ (S1 << 2) ^ (S2 << 4) ^ (S3 << 6) ^ P0.GetHashCode();
    }

    /// <summary>
    /// 드론(M4-04 프로토타입). 서버가 장면에 맞춰 드론을 띄우고(장면 3 은폐 사격, 4-2 끼어들기, 6 달려들기, 8 해킹) 상태·위치를 복제한다.
    /// 각 PC는 자기 팔로 판정해 보고만 한다: 왼팔 스캐너로 훑기 → 드러남, 오른팔 포 트리거 → 격추(보스를 쏘면 튕김),
    /// 주먹·검이 달려드는 드론에 닿음 → 떨굼, 동료 손이 해킹된 손에 닿음 → 뜯어냄.
    /// 겉모습은 도형(숨을 땐 어두운 작은 구, 드러나면 붉은 구) + 스캐너 레이저 선. 성재 드론 모델이 오면 <see cref="dronePrefab"/>에.
    /// 메뉴 'Add Match Flow'가 Combat Server에 붙인다.
    /// </summary>
    public class DroneDirector : NetworkBehaviour, ITunable
    {
        public const int Slots = 4;

        [SerializeField] DroneSettings settings = new DroneSettings();

        [Header("드론 자리 (조종사 루트 기준 m: x 오른쪽, y 위, z 앞)")]
        [SerializeField, Tooltip("장면 3 첫 드론(연습, 크레인 꼭대기).")]
        Vector3 practiceOffset = new Vector3(-10f, 45f, 70f);
        [SerializeField, Tooltip("장면 3 나머지 두 기(왼쪽 구조물 뒤, 오른쪽 하늘). 근접전 끼어들기에도 쓴다.")]
        Vector3[] pairOffsets = { new Vector3(-55f, 15f, 50f), new Vector3(45f, 55f, 40f) };

        [Header("겉모습 (비우면 도형)")]
        [SerializeField] GameObject dronePrefab;
        [SerializeField] Material shapeMaterial;
        [SerializeField, Min(0.5f)] float droneSize = 6f;
        [SerializeField] Color hiddenColor = new Color(0.15f, 0.15f, 0.18f);
        [SerializeField] Color revealedColor = new Color(1f, 0.15f, 0.1f);
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

        // 서버
        readonly DroneKind[] kind = new DroneKind[Slots];
        readonly DroneState[] state = new DroneState[Slots];
        readonly Vector3[] position = new Vector3[Slots];
        readonly Vector3[] rushFrom = new Vector3[Slots];
        readonly double[] revealUntil = new double[Slots];
        readonly double[] nextFire = new double[Slots];
        readonly double[] spawnedAt = new double[Slots];
        MatchScene serverScene;
        double sceneStart, nextRush, nextPublish;
        int huntWave, interludesDone, hacksDone, lastCracks;
        bool interlude, saidNoGun, saidBounce;
        PilotSide? hacked;

        // 모든 PC
        readonly GameObject[] bodies = new GameObject[Slots];
        readonly Vector3[] targets = new Vector3[Slots];
        readonly bool[] eligible = new bool[Slots];
        readonly float[] lastReport = new float[Slots];
        LineRenderer laser;
        MaterialPropertyBlock block;
        bool triggerWasDown;
        float lastHackReport;
        PilotSide? clientHacked;

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
                hands[(int)arm.Side] = arm.HandBone;
            block = new MaterialPropertyBlock();
            BuildVisuals();
            if (combat != null && combat.Director != null) combat.Director.Cue += OnClientCue;
        }

        public override void OnNetworkDespawn()
        {
            if (combat != null && combat.Director != null) combat.Director.Cue -= OnClientCue;
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
            var scene = flow.Scene == MatchScene.Rebooting ? MatchScene.Rebooting : flow.Scene;
            if (scene != serverScene)
            {
                EnterScene(scene, now);
                serverScene = scene;
            }
            switch (scene)
            {
                case MatchScene.DroneHunt: Hunt(now); break;
                case MatchScene.FightCannon: Interlude(now); break;
                case MatchScene.FightArmor: Rushers(now); break;
                case MatchScene.FightCore: Hackers(now); break;
            }
            Fire(now);
            if (now >= nextPublish) Publish(now);
        }

        void EnterScene(MatchScene scene, double now)
        {
            if (scene == MatchScene.Rebooting) return; // 재기동 뒤 같은 구간으로 돌아오면 다시 시작
            ClearAll();
            sceneStart = now;
            huntWave = 0;
            interlude = false;
            nextRush = now + settings.rushInterval * 0.5f;
            if (scene == MatchScene.Waiting || scene == MatchScene.Checkup)
            {
                interludesDone = hacksDone = 0;
                saidNoGun = saidBounce = false;
            }
            if (scene == MatchScene.FightCannon) lastCracks = Cracks();
            if (scene == MatchScene.DroneHunt) Spawn(0, DroneKind.Cloaked, Offset(practiceOffset), now);
            flow.SetDronesActive(false);
            SetHacked(null, now);
            Publish(now);
        }

        void Hunt(double now)
        {
            if (now - sceneStart >= settings.huntTimeout)
            {
                // 남은 드론은 보스에게 돌아간다(막히지 않게).
                ClearAll();
                flow.ServerSignal(MatchSignal.DronesCleared);
                return;
            }
            if (AnyAlive()) return;
            if (huntWave == 0)
            {
                huntWave = 1;
                Line(CommanderLines.FindAndShoot, now);
                for (var i = 0; i < pairOffsets.Length && i < Slots; i++) Spawn(i, DroneKind.Cloaked, Offset(pairOffsets[i]), now + i * 2f);
                Publish(now);
                return;
            }
            flow.ServerSignal(MatchSignal.DronesCleared);
        }

        /// <summary>근접전 1구간: 보스 팔뚝 포에 금이 1개·2개 갈 때 드론 끼어들기(대본 4-2).</summary>
        void Interlude(double now)
        {
            var cracks = Cracks();
            if (!interlude && cracks > lastCracks && interludesDone < 2)
            {
                interlude = true;
                interludesDone++;
                for (var i = 0; i < pairOffsets.Length && i < Slots; i++) Spawn(i, DroneKind.Cloaked, Offset(pairOffsets[i]), now + i * 2f);
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

        /// <summary>검·방패만: 달려드는 드론(대본 6).</summary>
        void Rushers(double now)
        {
            if (now >= nextRush && boss != null)
            {
                nextRush = now + settings.rushInterval;
                var slot = FreeSlot();
                if (slot >= 0)
                {
                    rushFrom[slot] = boss.transform.position + Vector3.up * 40f;
                    Spawn(slot, DroneKind.Rusher, rushFrom[slot], now);
                    state[slot] = DroneState.Revealed;
                    if (!saidNoGun)
                    {
                        saidNoGun = true;
                        Line(CommanderLines.NoGun, now);
                    }
                }
            }
            for (var i = 0; i < Slots; i++)
            {
                if (kind[i] != DroneKind.Rusher || state[i] != DroneState.Revealed || player == null) continue;
                var t = (float)((now - spawnedAt[i]) / settings.rushSeconds);
                position[i] = DroneRules.RushPosition(rushFrom[i], player.position + Vector3.up * 30f, t);
                if (t >= 1f)
                {
                    Hit(now, i, position[i]);
                    state[i] = DroneState.None;
                    Publish(now);
                }
            }
        }

        /// <summary>맨손: 해킹 드론 두 번(대본 8).</summary>
        void Hackers(double now)
        {
            if (hacked.HasValue || hacksDone >= settings.hackTimes.Length) return;
            if (now - sceneStart < settings.hackTimes[hacksDone]) return;
            hacksDone++;
            SetHacked(UnityEngine.Random.value < 0.5f ? PilotSide.Left : PilotSide.Right, now);
        }

        void SetHacked(PilotSide? side, double now)
        {
            if (hacked == side) return;
            var was = hacked;
            hacked = side;
            if (side.HasValue)
            {
                Emit(CombatCue.HackStart, side.Value, now, Vector3.zero, 0);
                Line(CommanderLines.Hacked, now);
            }
            else if (was.HasValue)
            {
                Emit(CombatCue.HackCleared, was.Value, now, Vector3.zero, 0);
                Line(CommanderLines.Restored, now);
            }
            combat.Flush();
        }

        void Fire(double now)
        {
            for (var i = 0; i < Slots; i++)
            {
                if (kind[i] != DroneKind.Cloaked || (state[i] != DroneState.Hidden && state[i] != DroneState.Revealed)) continue;
                if (now < spawnedAt[i]) continue;
                if (state[i] == DroneState.Revealed && now >= revealUntil[i])
                {
                    state[i] = DroneState.Hidden;
                    Emit(CombatCue.DroneHidden, PilotSide.Left, now, position[i], i);
                    Publish(now);
                }
                if (now < nextFire[i]) continue;
                nextFire[i] = now + settings.fireInterval;
                Emit(CombatCue.DroneFire, PilotSide.Left, now, position[i], i);
                Hit(now, i, position[i]);
            }
        }

        void Hit(double now, int i, Vector3 from)
        {
            if (player == null) return;
            var side = Vector3.Dot(from - player.position, player.right) < 0f ? PilotSide.Left : PilotSide.Right;
            combat.Authority.ResolveBossStrike(BossCounter.AlternateGuard, side, settings.fireDamage, 100 + i, now, combat.Events);
            combat.Flush();
        }

        void Spawn(int i, DroneKind k, Vector3 at, double now)
        {
            kind[i] = k;
            state[i] = DroneState.Hidden;
            position[i] = at;
            spawnedAt[i] = now;
            revealUntil[i] = 0;
            nextFire[i] = now + settings.fireInterval * (0.5f + 0.25f * i);
        }

        void ClearAll()
        {
            for (var i = 0; i < Slots; i++) state[i] = DroneState.None;
        }

        bool AnyAlive()
        {
            for (var i = 0; i < Slots; i++)
                if (state[i] == DroneState.Hidden || state[i] == DroneState.Revealed) return true;
            return false;
        }

        int FreeSlot()
        {
            for (var i = 0; i < Slots; i++)
                if (state[i] == DroneState.None || state[i] == DroneState.Down) return i;
            return -1;
        }

        Vector3 Offset(Vector3 local) => player != null ? player.position + player.rotation * local : local;

        int Cracks()
        {
            if (boss == null) return 0;
            var b = combat.Authority.Resolver.Boss;
            var hp = b.Max > 0f ? b.Current / b.Max * 100f : 0f;
            return BossWeaponRules.Current(hp, boss.Thresholds) == BossWeapon.ArmCannon ? BossWeaponRules.CrackCount(hp, boss.Thresholds) : 2;
        }

        void Publish(double now)
        {
            nextPublish = now + 0.1;
            var s = new DroneNetState();
            for (var i = 0; i < Slots; i++)
            {
                var visible = state[i] != DroneState.None && now >= spawnedAt[i];
                s.Set(i, kind[i], visible ? state[i] : DroneState.None, position[i]);
            }
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
            var now = Now;
            if (i == 255)
            {
                // 보스를 쐈다: 장갑에 튕긴다(대본 3-4).
                Emit(CombatCue.CannonFire, PilotSide.Right, now, aim, 0, 1f);
                if (!saidBounce)
                {
                    saidBounce = true;
                    Line(CommanderLines.ArmorBounces, now);
                }
                combat.Flush();
                return;
            }
            Emit(CombatCue.CannonFire, PilotSide.Right, now, aim, i);
            if (i < Slots && state[i] == DroneState.Revealed && kind[i] == DroneKind.Cloaked) Down(i, now);
            combat.Flush();
        }

        [Rpc(SendTo.Server)]
        void PunchDroneRpc(byte i)
        {
            if (i >= Slots || kind[i] != DroneKind.Rusher || state[i] != DroneState.Revealed) return;
            Down(i, Now);
            combat.Flush();
        }

        [Rpc(SendTo.Server)]
        void HackClearRpc()
        {
            SetHacked(null, Now);
        }

        void Down(int i, double now)
        {
            state[i] = DroneState.Down;
            Emit(CombatCue.DroneDown, PilotSide.Right, now, position[i], i);
            flow.ServerDroneDown();
            Publish(now);
        }

        // ───────── 모든 PC: 겉모습·판정 보고 ─────────

        void OnClientCue(CombatEvent e)
        {
            if (e.Cue == CombatCue.HackStart) clientHacked = e.Side;
            else if (e.Cue == CombatCue.HackCleared || e.Cue == CombatCue.MatchScene) clientHacked = null;
        }

        void BuildVisuals()
        {
            for (var i = 0; i < Slots; i++)
            {
                GameObject go;
                if (dronePrefab != null) go = Instantiate(dronePrefab);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    var c = go.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                    var r = go.GetComponent<Renderer>();
                    if (shapeMaterial != null && r != null) r.sharedMaterial = shapeMaterial;
                    go.transform.localScale = Vector3.one * droneSize;
                }
                go.name = "Drone " + i;
                go.SetActive(false);
                bodies[i] = go;
            }
            var laserObject = new GameObject("Scanner Laser");
            laser = laserObject.AddComponent<LineRenderer>();
            laser.positionCount = 2;
            laser.widthMultiplier = 0.4f;
            if (shapeMaterial != null) laser.sharedMaterial = shapeMaterial;
            laser.startColor = laser.endColor = new Color(0.2f, 1f, 0.6f);
            laser.enabled = false;
        }

        void Tint(GameObject go, Color color)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        void ClientTick()
        {
            var s = net.Value;
            for (var i = 0; i < Slots; i++)
            {
                var st = s.State(i);
                var on = st == DroneState.Hidden || st == DroneState.Revealed;
                var body = bodies[i];
                if (body == null) continue;
                if (body.activeSelf != on) body.SetActive(on);
                targets[i] = s.Position(i);
                if (!on) continue;
                body.transform.position = Vector3.Lerp(body.transform.position, targets[i], 1f - Mathf.Exp(-Time.deltaTime * 10f));
                var revealed = st == DroneState.Revealed;
                body.transform.localScale = Vector3.one * droneSize * (revealed ? 1f : 0.6f);
                Tint(body, revealed ? revealedColor : hiddenColor);
            }
            if (input == null) return;
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

            // 오른팔 포: 트리거 → 조준이 붙은 드러난 드론 격추, 보스를 겨누면 튕김.
            var cannonTip = weapons != null ? weapons.CannonTip : null;
            var trigger = input.IsArmHand(PilotSide.Right) && input.Get(PilotSide.Right).Trigger > 0.7f;
            if (trigger && !triggerWasDown && loadout == Loadout.Ranged && cannonTip != null && cannonTip.gameObject.activeInHierarchy)
            {
                var o = cannonTip.position;
                var d = cannonTip.up;
                for (var i = 0; i < Slots; i++) eligible[i] = s.State(i) == DroneState.Revealed && s.Kind(i) == DroneKind.Cloaked;
                var hit = DroneRules.Pick(o, d, targets, eligible, settings.aimDegrees);
                if (hit >= 0) ShootRpc((byte)hit, targets[hit]);
                else if (boss != null && DroneRules.AngleTo(o, d, boss.transform.position + Vector3.up * 35f) < 20f) ShootRpc(255, boss.transform.position);
                else ShootRpc(254, o + d * 100f);
            }
            triggerWasDown = trigger;

            // 주먹·검: 달려드는 드론에 닿으면 떨굼.
            for (var h = 0; h < 2; h++)
            {
                var hand = hands[h];
                if (hand == null || !input.IsArmHand((PilotSide)h)) continue;
                for (var i = 0; i < Slots; i++)
                {
                    if (s.Kind(i) != DroneKind.Rusher || s.State(i) != DroneState.Revealed) continue;
                    if ((hand.position - bodies[i].transform.position).magnitude > settings.punchRadius) continue;
                    if (Time.time - lastReport[i] < 0.3f) continue;
                    lastReport[i] = Time.time;
                    PunchDroneRpc((byte)i);
                }
            }

            // 해킹: 동료 손이 해킹된 손에 닿으면 뜯어냄(동료 팔이 이 PC에 있을 때).
            if (clientHacked.HasValue)
            {
                var victim = clientHacked.Value;
                var helper = victim == PilotSide.Left ? PilotSide.Right : PilotSide.Left;
                if (input.IsArmHand(helper) && hands[0] != null && hands[1] != null &&
                    (hands[0].position - hands[1].position).magnitude < settings.hackClearRadius && Time.time - lastHackReport > 0.5f)
                {
                    lastHackReport = Time.time;
                    HackClearRpc();
                }
            }
        }

        public void AddTuning(TuningList list)
        {
            const string g = "드론";
            list.Describe(g, "왼팔 스캐너로 훑으면 드러남(3초), 오른팔 트리거로 쏨. 달려드는 드론은 주먹으로, 해킹은 동료 손으로");
            list.AddFloat(g, "drone.scan", "스캐너 맞힘 각도(°)", () => settings.scanDegrees, v => settings.scanDegrees = v, 2f, 20f, 1f, 0);
            list.AddFloat(g, "drone.aim", "포 조준 붙는 각도(°)", () => settings.aimDegrees, v => settings.aimDegrees = v, 2f, 25f, 1f, 0);
        }
    }
}
