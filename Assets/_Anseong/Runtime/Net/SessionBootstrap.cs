using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Anseong.Pilot;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Anseong.Net
{
    public enum SessionMode
    {
        /// <summary>한 PC가 두 팔을 모두 맡는다. 호스트로 실행(오프라인 전용 경로 없음). 다른 PC는 받지 않는다.</summary>
        SoloBothArms,
        /// <summary>PC-A: 호스트 + 왼쪽 조종사. PC-B 한 대를 받는다.</summary>
        Host,
        /// <summary>PC-B: 클라이언트 + 오른쪽 조종사. PC-A IP로 접속.</summary>
        Client,
    }

    /// <summary>
    /// 네트워크 세션을 연다(M2-01 → M3-01). 1인 양팔 / 호스트 / 클라이언트를 운영자 시작 화면(<c>OperatorStartScreen</c>)이나
    /// 실행 인자(-mode solo|host|client -ip 192.168.0.10)로 고른다. 에디터는 기본으로 시작 화면 없이 Inspector Mode로 바로 시작한다.
    /// 호스트는 7777에서 기다리고 조종사 2명(호스트 포함)까지만 승인한다(<see cref="SessionRules"/>). 마지막 모드·IP는 이 PC에 기억한다.
    /// 모드에 맞게 이 PC가 맡는 손(PilotHandInput)을 바꾸고 보정을 다시 시작한다.
    /// 시작에 실패하면 이유를 <see cref="Problem"/>에 두고 안내판·시작 화면이 띄운다.
    /// </summary>
    public class SessionBootstrap : MonoBehaviour
    {
        const string PrefMode = "anseong.session.mode";
        const string PrefAddress = "anseong.session.address";

        [SerializeField, Tooltip("에디터에서 시작 화면을 건너뛸 때 쓰는 실행 방식. 빌드는 시작 화면(또는 -mode 실행 인자)으로 고른다.")]
        SessionMode mode = SessionMode.SoloBothArms;

        [SerializeField, Tooltip("네트워크 관리자. 비어 있으면 같은 오브젝트에서 찾는다.")]
        NetworkManager network;

        [SerializeField, Tooltip("클라이언트가 접속할 호스트(PC-A) IP. 시작 화면에서 바꾸면 이 PC에 기억한다.")]
        string address = "127.0.0.1";

        [SerializeField, Tooltip("포트(CLAUDE.md 고정 7777).")]
        ushort port = 7777;

        [SerializeField, Tooltip("에디터 Play에서는 시작 화면 없이 위 Mode로 바로 시작한다(개발 편의). 끄면 에디터에서도 시작 화면이 뜬다.")]
        bool skipStartScreenInEditor = true;

        [SerializeField, Range(1, 30), Tooltip("클라이언트가 호스트를 찾는 시간(초). 넘으면 실패로 보고 시작 화면으로 돌아간다.")]
        int connectSeconds = 5;

        [SerializeField, Tooltip("이 PC 조종사 손 입력. 모드에 맞게 맡은 팔을 바꾼다. 비어 있으면 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Tooltip("보정 흐름. 맡은 팔이 바뀌면 보정을 처음부터. 비어 있으면 한 번 찾는다.")]
        PilotCalibrationFlow calibration;

        [SerializeField, Tooltip("조종석 좌석. 클라이언트(오른팔)는 오른쪽 좌석에 선다. 비어 있으면 한 번 찾는다.")]
        CockpitSeat seat;

        readonly HashSet<ulong> remotePilots = new HashSet<ulong>();
        bool subscribed;
        bool clientConnected;
        bool ending;

        /// <summary>지금(또는 마지막으로 시작한) 실행 방식.</summary>
        public SessionMode Mode => mode;
        /// <summary>이 PC에서 마지막으로 고른 방식(시작 화면이 미리 골라 둔다).</summary>
        public SessionMode LastMode { get; private set; }
        public string Address => address;
        public ushort Port => port;
        /// <summary>시작하지 못한 이유·끊긴 이유(비면 문제 없음). 안내판·시작 화면이 읽는다.</summary>
        public string Problem { get; private set; } = string.Empty;
        public bool Running => network != null && network.IsListening;
        /// <summary>운영자가 모드를 고르기를 기다리는 중(시작 화면을 띄운다).</summary>
        public bool WaitingForOperator { get; private set; }
        /// <summary>클라이언트가 호스트를 찾는 중.</summary>
        public bool Connecting => Running && mode == SessionMode.Client && !clientConnected;
        /// <summary>접속한 조종사 수(이 PC 포함). 클라이언트는 접속되면 2.</summary>
        public int PilotCount
        {
            get
            {
                if (!Running) return 0;
                if (mode == SessionMode.Client) return clientConnected ? 2 : 1;
                return 1 + remotePilots.Count;
            }
        }

        public event Action<string> ProblemChanged;
        /// <summary>시작·끝·접속·끊김. 시작 화면·상태 줄이 다시 그린다.</summary>
        public event Action StateChanged;

        void Awake()
        {
            LastMode = (SessionMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefMode, (int)mode), 0, 2);
            address = PlayerPrefs.GetString(PrefAddress, address);
        }

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            var argIp = SessionRules.ArgValue(args, "-ip");
            if (SessionRules.TryModeFromArgs(args, out var argMode))
            {
                if (!Begin(argMode, argIp ?? address)) SetWaiting(true);
                return;
            }
            if (argIp != null) address = argIp;
            if (!(Application.isEditor && skipStartScreenInEditor) || !Begin(mode, address)) SetWaiting(true);
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        /// <summary>운영자가 고른 방식으로 세션 시작. 이미 돌고 있으면 거절(먼저 End). 클라이언트는 IP가 맞아야 한다.</summary>
        public bool Begin(SessionMode chosen, string hostAddress)
        {
            if (network == null) network = GetComponent<NetworkManager>();
            if (network == null) network = NetworkManager.Singleton;
            if (network == null) return Fail("NetworkManager가 없습니다. 'Anseong → Setup → Add Network Session'을 실행하세요.");
            var transport = network.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null) return Fail("NetworkManager에 UnityTransport가 연결되지 않았습니다. 메뉴를 다시 실행하세요.");

            var target = address;
            if (chosen == SessionMode.Client)
            {
                if (!SessionRules.TryParseAddress(hostAddress, out target))
                    return Fail($"호스트 IP를 다시 확인하세요: '{hostAddress}'. 예: 192.168.0.10 (PC-A 시작 화면에 나옴)");
            }
            if (network.IsListening || network.ShutdownInProgress)
                return Fail("이전 세션이 아직 끝나는 중입니다. 잠시 뒤 다시 시작하세요.");

            ending = false;
            mode = chosen;
            LastMode = chosen;
            if (chosen == SessionMode.Client) address = target;
            Remember();
            ApplyRoles();
            Subscribe();
            remotePilots.Clear();
            clientConnected = false;

            var config = network.NetworkConfig;
            config.ConnectionApproval = true;
            config.ConnectionData = SessionRules.ConnectionPayload();
            network.ConnectionApprovalCallback = OnApproval;

            bool ok;
            switch (chosen)
            {
                case SessionMode.Host:
                    // 같은 공유기·스위치의 PC-B가 들어오게 모든 랜카드에서 기다린다.
                    transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
                    ok = network.StartHost();
                    if (!ok) return Fail($"호스트 시작 실패(포트 {port}). 이 PC에서 다른 Unity 창이나 빌드가 돌고 있으면 끄고 다시 시작하세요.");
                    Debug.Log($"[Session] Host started (호스트·왼팔, 포트 {port}, 이 PC IP: {string.Join(", ", LocalAddresses())})", this);
                    break;
                case SessionMode.Client:
                    transport.SetConnectionData(target, port);
                    transport.ConnectTimeoutMS = 1000;
                    transport.MaxConnectAttempts = Mathf.Max(1, connectSeconds);
                    ok = network.StartClient();
                    if (!ok) return Fail($"클라이언트 시작 실패({target}:{port}).");
                    Debug.Log($"[Session] Client connecting → {target}:{port} (오른팔)", this);
                    break;
                default:
                    transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
                    ok = network.StartHost();
                    if (!ok) return Fail($"호스트 시작 실패(127.0.0.1:{port}). 다른 Unity 창이나 빌드가 포트 {port}을 쓰고 있으면 끄고 다시 Play하세요.");
                    Debug.Log($"[Session] Host started (1인 양팔, 127.0.0.1:{port})", this);
                    break;
            }
            SetProblem(string.Empty);
            SetWaiting(false);
            return true;
        }

        /// <summary>세션을 끝내고 시작 화면으로 돌아간다(운영자 "다시 고르기").</summary>
        public void End()
        {
            ending = true;
            if (network != null && network.IsListening) network.Shutdown();
            remotePilots.Clear();
            clientConnected = false;
            SetWaiting(true);
        }

        void ApplyRoles()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (seat == null) seat = FindAnyObjectByType<CockpitSeat>();
            var both = mode == SessionMode.SoloBothArms;
            var side = SessionRules.ArmSide(mode);
            if (seat != null) seat.Side = side;
            if (input == null) return;
            if (input.ControlsBothArms == both && input.ArmSide == side) return;
            input.ControlsBothArms = both;
            input.ArmSide = side;
            // 맡은 손이 바뀌면 보정도 그 손으로 처음부터.
            if (calibration != null) calibration.Restart();
            Debug.Log($"[Session] 이 PC가 맡는 팔: {(both ? "두 팔" : side == PilotSide.Left ? "왼팔" : "오른팔")}. 보정을 처음부터 합니다.", this);
        }

        void OnApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var self = request.ClientNetworkId == NetworkManager.ServerClientId;
            var result = SessionRules.Approve(mode, self, remotePilots.Count, SessionRules.ReadPayload(request.Payload));
            response.Approved = result.Approved;
            response.Reason = result.Reason;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (result.Approved && !self) remotePilots.Add(request.ClientNetworkId);
            if (!result.Approved) Debug.LogWarning($"[Session] 접속 거절(클라이언트 {request.ClientNetworkId}): {result.Reason}", this);
        }

        void OnClientConnected(ulong clientId)
        {
            if (network.IsServer)
            {
                if (clientId != NetworkManager.ServerClientId)
                    Debug.Log($"[Session] 오른쪽 조종사 접속(클라이언트 {clientId}). 조종사 {PilotCount}/{SessionRules.MaxPilots}", this);
            }
            else if (clientId == network.LocalClientId)
            {
                clientConnected = true;
                SetProblem(string.Empty);
                Debug.Log($"[Session] Client connected ({address}:{port}, 오른팔)", this);
            }
            StateChanged?.Invoke();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (ending) return;
            if (network.IsServer)
            {
                if (remotePilots.Remove(clientId))
                    Debug.LogWarning($"[Session] 오른쪽 조종사 나감(클라이언트 {clientId}). 조종사 {PilotCount}/{SessionRules.MaxPilots}, 다시 접속을 기다림", this);
                StateChanged?.Invoke();
                return;
            }
            // 클라이언트: 접속 실패·거절·호스트 꺼짐. NGO가 스스로 끝내므로 시작 화면으로 돌아간다.
            var reason = network.DisconnectReason;
            var wasConnected = clientConnected;
            clientConnected = false;
            if (string.IsNullOrEmpty(reason))
                reason = wasConnected ? "호스트(PC-A)와 연결이 끊겼습니다." : $"호스트({address}:{port})를 찾지 못했습니다. PC-A가 '호스트'로 시작했는지, IP·방화벽을 확인하세요.";
            Fail(reason);
            if (network.IsListening) network.Shutdown();
            SetWaiting(true);
        }

        void OnTransportFailure() => Fail("네트워크 전송이 끊겼습니다(포트 충돌 등). 다시 시작하세요.");

        void Subscribe()
        {
            if (subscribed || network == null) return;
            network.OnTransportFailure += OnTransportFailure;
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed || network == null) return;
            network.OnTransportFailure -= OnTransportFailure;
            network.OnClientConnectedCallback -= OnClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnected;
            subscribed = false;
        }

        void Remember()
        {
            PlayerPrefs.SetInt(PrefMode, (int)mode);
            PlayerPrefs.SetString(PrefAddress, address);
            PlayerPrefs.Save();
        }

        /// <summary>이 PC의 IPv4 주소(호스트 시작 화면에 띄워 PC-B 운영자가 받아 적는다).</summary>
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) list.Add(ip.Address.ToString());
                }
            }
            catch (Exception)
            {
                // 주소를 못 읽어도 접속에는 지장 없다(운영자가 ipconfig로 확인).
            }
            return list;
        }

        bool Fail(string reason)
        {
            SetProblem(reason);
            Debug.LogError("[Session] " + reason, this);
            StateChanged?.Invoke();
            return false;
        }

        void SetWaiting(bool value)
        {
            WaitingForOperator = value;
            StateChanged?.Invoke();
        }

        void SetProblem(string value)
        {
            if (Problem == value) return;
            Problem = value;
            ProblemChanged?.Invoke(value);
        }
    }
}
