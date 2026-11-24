using System.Collections.Generic;
using Anseong.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.Skirmish
{
    /// <summary>
    /// Z2 교전을 두 PC에 맞춰 돌린다. 규칙(<see cref="SkirmishSession"/>)은 서버(호스트)에서만 돌고, 상태는 1초에 30번 모든 PC에 보낸다.
    /// 호스트 = 메카(P1), 접속한 PC = 공중포대(P2). 사수 입력은 접속한 PC가 서버에 보낸다(비신뢰, 늦게 온 것은 덮어씀).
    /// 접속·시작 화면은 기존 <see cref="SessionBootstrap"/>을 그대로 쓴다: 1인 = 혼자(빈 자리는 자동 조작), 호스트 = 메카, 클라이언트 = 공중포대.
    /// 운영자 키(모니터 앞 키보드): R 재시작, Enter 사수 없이 시작, B 빈 자리 자동 켜고 끄기, C 착용자 정면 다시 맞추기, G 혼자일 때 자리 바꾸기.
    /// 메뉴 'Anseong → Sandbox → Build Z2 Skirmish'가 NetworkObject와 함께 씬에 놓는다.
    /// </summary>
    public class SkirmishServer : NetworkBehaviour
    {
        [SerializeField] SkirmishSettings settings = new SkirmishSettings();
        [SerializeField] SkirmishView view;
        [SerializeField] SkirmishLocalInput local;
        [SerializeField] SkirmishHud hud;
        [SerializeField, Tooltip("사수가 없으면 자동 조작이 공중포대를 맡는다.")] bool botFillsEmptySeat = true;
        [SerializeField, Range(10, 60), Tooltip("상태·입력을 보내는 횟수(초당).")] int sendRate = 30;
        [SerializeField, Range(0.2f, 3f), Tooltip("이 시간(초) 동안 사수 입력이 안 오면 끊긴 것으로 보고 포를 멈춘다.")] float gunnerStaleSeconds = 0.75f;

        const float Step = 0.02f;

        SkirmishSession session;                                    // 서버만
        readonly SkirmishState shown = new SkirmishState();         // 클라이언트: 마지막으로 받은 상태
        readonly List<SkirmishEvent> pending = new List<SkirmishEvent>(32);
        SessionBootstrap bootstrap;
        GunnerInput remoteGunner;
        float remoteGunnerAt = -100f, accumulator, nextSend;
        bool gunnerConnected, forcedStart, soloAsGunner, watching;
        Vector3 mechAim = Vector3.forward, gunnerAim = Vector3.forward;
        Vector3 mechBefore;     // 직전 틱의 메카 위치(틱 사이를 잇는 데 쓴다)

        /// <summary>클라이언트가 받은 상태 수 / 서버가 받은 사수 입력 수(연결 확인용).</summary>
        public int StatesReceived { get; private set; }
        public int GunnerInputsReceived { get; private set; }
        public SkirmishState CurrentState => IsSpawned && IsServer ? session.State : shown;

        /// <summary>씬을 만드는 에디터 메뉴가 장면별 수치를 넣는다.</summary>
        public void Configure(SkirmishSettings value) => settings = value;

        public void Bind(SkirmishView skirmishView, SkirmishLocalInput input, SkirmishHud skirmishHud)
        {
            view = skirmishView; local = input; hud = skirmishHud;
        }

        void Awake()
        {
            // 두 번째 창이 뒤에 있어도 멈추지 않게(한 PC에서 두 개를 띄워 확인할 때).
            Application.runInBackground = true;
            NetSkirmishState.From(new SkirmishSession(settings).State, Vector3.forward, Vector3.forward).CopyTo(shown);
        }

        public override void OnNetworkSpawn()
        {
            bootstrap = FindAnyObjectByType<SessionBootstrap>();
            pending.Clear();
            StatesReceived = GunnerInputsReceived = 0;
            if (IsServer)
            {
                session = new SkirmishSession(settings);
                mechBefore = session.State.mech;
                gunnerConnected = forcedStart = soloAsGunner = false;
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                watching = true;
            }
            local.SetSeat(IsServer ? SkirmishSeat.Mech : SkirmishSeat.Gunner);
        }

        public override void OnNetworkDespawn()
        {
            if (watching && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            watching = false;
            session = null;
        }

        void OnClientConnected(ulong clientId)
        {
            if (clientId == Unity.Netcode.NetworkManager.ServerClientId) return;
            gunnerConnected = true;
            Debug.Log($"[Skirmish] 공중포대 사수 접속(클라이언트 {clientId})", this);
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (clientId == Unity.Netcode.NetworkManager.ServerClientId) return;
            gunnerConnected = false;
            Debug.LogWarning($"[Skirmish] 공중포대 사수 나감(클라이언트 {clientId})", this);
        }

        bool Solo => bootstrap == null || bootstrap.Mode == SessionMode.SoloBothArms;

        void Update()
        {
            if (view == null || local == null) return;
            if (!IsSpawned)
            {
                // 운영자가 시작 화면에서 고르기 전: 처음 상태만 보여 준다.
                local.Read(shown.mech, settings.gunnerPost);
                view.Show(shown, settings, null, mechAim, Vector3.zero, Time.time);
                if (hud != null) hud.Show(shown, settings, null, local.Seat, local.VrActive, "운영자가 시작하기를 기다리는 중");
                AutoCheck();
                return;
            }
            if (IsServer) ServerUpdate(); else ClientUpdate();
            AutoCheck();
        }

        // ── 서버(호스트 = 메카) ──

        void ServerUpdate()
        {
            var st = session.State;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) ResetSession();
                if (keyboard.enterKey.wasPressedThisFrame) forcedStart = true;
                if (keyboard.bKey.wasPressedThisFrame) botFillsEmptySeat = !botFillsEmptySeat;
                if (keyboard.cKey.wasPressedThisFrame && local.VrActive) local.Recenter();
                if (keyboard.gKey.wasPressedThisFrame && Solo) soloAsGunner = !soloAsGunner;
            }
            st = session.State;
            var seat = Solo && soloAsGunner ? SkirmishSeat.Gunner : SkirmishSeat.Mech;
            local.SetSeat(seat);
            local.Read(st.mech, settings.gunnerPost);

            if (st.phase == SkirmishPhase.Waiting && (Solo || gunnerConnected || forcedStart)) session.SetReady(true, true);

            bool localIsMech = seat == SkirmishSeat.Mech;
            var mechInput = localIsMech && !local.UseBot ? local.Mech : SkirmishBots.Mech(st, settings);
            GunnerInput gunnerInput;
            if (!localIsMech) gunnerInput = local.UseBot ? SkirmishBots.Gunner(st, settings) : local.Gunner;
            else if (gunnerConnected) gunnerInput = Time.unscaledTime - remoteGunnerAt < gunnerStaleSeconds ? remoteGunner : default;
            else gunnerInput = botFillsEmptySeat ? SkirmishBots.Gunner(st, settings) : default;
            if (SkirmishMath.Direction(ref mechInput.aim)) mechAim = mechInput.aim;
            if (SkirmishMath.Direction(ref gunnerInput.aim)) gunnerAim = gunnerInput.aim;
            // 포가 없는 장면에서는 오른팔을 맞붙은 돌격형 쪽으로 보여 준다.
            if (!settings.mechGun && st.GruntAlive) mechAim = (SkirmishSession.GruntCenter(st, settings) - SkirmishSession.Gun(st, settings)).normalized;
            // 가드를 열어 줄 사람도 자동 조작도 없으면 가드가 스스로 더 빨리 풀린다.
            session.Solo = localIsMech && !gunnerConnected && !botFillsEmptySeat;

            session.SetMech(mechInput);
            session.SetGunner(gunnerInput);
            accumulator = Mathf.Min(accumulator + Time.deltaTime, 0.25f);
            while (accumulator >= Step)
            {
                accumulator -= Step;
                mechBefore = session.State.mech;
                session.Tick(Step);
                foreach (var e in session.Events)
                {
                    pending.Add(e);
                    if (e.Kind == SkirmishEventKind.CannonShot || e.Kind == SkirmishEventKind.MechGunShot)
                        ShotRpc((byte)e.Kind, e.Id, e.From, e.To);
                    else EventRpc((byte)e.Kind, e.Id, e.From, e.To);
                }
            }

            // 규칙은 1초에 50번, 화면은 72~90번이라 틱 위치를 그대로 쓰면 달릴 때 화면이 떨린다. 두 틱 사이를 이어서 보여 준다.
            var smooth = Vector3.Lerp(mechBefore, session.State.mech, accumulator / Step);
            view.MechPosition = smooth;
            local.React(pending);
            local.FollowMech(smooth, Time.deltaTime);
            Present(session.State, seat, WaitingText());
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + 1f / Mathf.Max(1, sendRate);
            StateRpc(NetSkirmishState.From(session.State, mechAim, gunnerAim));
        }

        string WaitingText() => Solo ? "" : gunnerConnected ? "" : "공중포대 사수 접속을 기다리는 중 (Enter: 사수 없이 시작)";

        void ResetSession()
        {
            session.Reset();
            mechBefore = session.State.mech;
            forcedStart = false;
            pending.Clear();
            Debug.Log("[Skirmish] 교전 재시작", this);
        }

        // ── 클라이언트(공중포대) ──

        void ClientUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) ResetRpc();
                if (keyboard.cKey.wasPressedThisFrame && local.VrActive) local.Recenter();
            }
            local.Read(shown.mech, settings.gunnerPost);
            var input = local.UseBot ? (StatesReceived > 0 ? SkirmishBots.Gunner(shown, settings) : default) : local.Gunner;
            if (SkirmishMath.Direction(ref input.aim)) gunnerAim = input.aim;

            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 1f / Mathf.Max(1, sendRate);
                byte flags = (byte)((input.fire ? 1 : 0) | (input.lockHeld ? 2 : 0) | (input.cancel ? 4 : 0));
                GunnerInputRpc(input.aim, input.pointer, flags);
            }
            // 상태는 1초에 30번 오므로 그 사이 탄은 속도대로 미리 움직여 둔다.
            foreach (var shell in shown.shells) shell.position += shell.velocity * Time.deltaTime;
            Present(shown, SkirmishSeat.Gunner, StatesReceived == 0 ? "호스트 상태를 기다리는 중" : "메카 조종사를 기다리는 중");
        }

        void Present(SkirmishState st, SkirmishSeat seat, string waiting)
        {
            view.Show(st, settings, pending, mechAim, gunnerAim, Time.time);
            if (hud != null) hud.Show(st, settings, pending, seat, local.VrActive, waiting);
            pending.Clear();
        }

        // ── 네트워크 ──

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void GunnerInputRpc(Vector3 aim, Vector3 pointer, byte flags, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == Unity.Netcode.NetworkManager.ServerClientId) return;
            remoteGunner = new GunnerInput
            {
                aim = aim, pointer = pointer,
                fire = (flags & 1) != 0, lockHeld = (flags & 2) != 0, cancel = (flags & 4) != 0,
            };
            remoteGunnerAt = Time.unscaledTime;
            GunnerInputsReceived++;
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void StateRpc(NetSkirmishState state)
        {
            state.CopyTo(shown);
            if (state.MechAim.sqrMagnitude > 1e-6f) mechAim = state.MechAim;
            StatesReceived++;
        }

        [Rpc(SendTo.NotServer)]
        void EventRpc(byte kind, int id, Vector3 from, Vector3 to) =>
            pending.Add(new SkirmishEvent { Kind = (SkirmishEventKind)kind, Id = id, From = from, To = to });

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void ShotRpc(byte kind, int id, Vector3 from, Vector3 to) =>
            pending.Add(new SkirmishEvent { Kind = (SkirmishEventKind)kind, Id = id, From = from, To = to });

        [Rpc(SendTo.Server)]
        void ResetRpc()
        {
            if (session != null) ResetSession();
        }

        // ── 자동 접속 확인(개발 빌드·에디터 전용) ──
        // 실행 인자 -skirmishAuto <결과 파일>: 이 PC 자리를 자동 조작에 맡기고, 교전이 끝나면 결과를 파일에 쓰고 종료한다.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string autoResultPath;
        float autoStarted = -1f, autoEndedAt = -1f;
        bool autoWasServer;

        [System.Serializable]
        class AutoResult
        {
            public string role, phase;
            public float hull, elapsed;
            public int furnace, statesReceived, gunnerInputsReceived;
            public bool g3Alive, g4Alive;
            public int gruntsDown;
        }

        void Start()
        {
            autoResultPath = SessionRules.ArgValue(System.Environment.GetCommandLineArgs(), "-skirmishAuto");
            if (autoResultPath != null && local != null) local.UseBot = true;
        }

        void AutoCheck()
        {
            if (autoResultPath == null) return;
            if (autoStarted < 0f) autoStarted = Time.unscaledTime;
            if (IsSpawned) autoWasServer = IsServer;
            var st = CurrentState;
            bool ended = st.phase == SkirmishPhase.Cleared || st.phase == SkirmishPhase.Failed;
            if (ended && autoEndedAt < 0f) autoEndedAt = Time.unscaledTime;
            bool timedOut = Time.unscaledTime - autoStarted > 150f;
            // 호스트는 끝난 뒤 2초 더 돌려 마지막 상태가 상대 PC에 닿게 한다. 받는 쪽은 바로 기록한다.
            float linger = IsSpawned && IsServer ? 2f : 0.3f;
            if (!timedOut && (autoEndedAt < 0f || Time.unscaledTime - autoEndedAt < linger)) return;
            var result = new AutoResult
            {
                role = autoWasServer ? "host-mech" : "client-gunner", phase = st.phase.ToString(),
                hull = st.hull, elapsed = st.elapsed, furnace = st.furnace,
                statesReceived = StatesReceived, gunnerInputsReceived = GunnerInputsReceived,
                g3Alive = st.enemies[0].Alive, g4Alive = st.enemies[1].Alive, gruntsDown = st.gruntsDown,
            };
            System.IO.File.WriteAllText(autoResultPath, JsonUtility.ToJson(result, true));
            Debug.Log($"[Skirmish] SKIRMISH_AUTO {result.role} {result.phase} states={result.statesReceived} inputs={result.gunnerInputsReceived}");
            autoResultPath = null;
            Application.Quit(st.phase == SkirmishPhase.Cleared ? 0 : 3);
        }
#else
        void AutoCheck() { }
#endif
    }
}
