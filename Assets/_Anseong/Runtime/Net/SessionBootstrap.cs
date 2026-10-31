using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Anseong.Net
{
    public enum SessionMode
    {
        /// <summary>한 PC가 두 팔을 모두 맡는다. 호스트로 실행(오프라인 전용 경로 없음).</summary>
        SoloBothArms,
        /// <summary>PC-A: 호스트 + 왼쪽 조종사(M3).</summary>
        Host,
        /// <summary>PC-B: 클라이언트 + 오른쪽 조종사(M3).</summary>
        Client,
    }

    /// <summary>
    /// 씬 시작 시 네트워크 세션을 연다(M2-01). 1인 양팔도 NGO 호스트(127.0.0.1:7777)로 돌려서 M2 서버 규칙을 그대로 쓰게 한다.
    /// 시작에 실패하면 이유를 <see cref="Problem"/>에 두고 안내판이 띄운다. Host·Client(PC 2대)는 M3 접속 작업에서 채운다.
    /// NetworkManager와 같은 오브젝트에 메뉴('Anseong → Setup → Add Network Session')가 붙인다.
    /// </summary>
    public class SessionBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("실행 방식. 지금은 1인 양팔만 된다(Host·Client는 M3).")]
        SessionMode mode = SessionMode.SoloBothArms;

        [SerializeField, Tooltip("네트워크 관리자. 비어 있으면 같은 오브젝트에서 찾는다.")]
        NetworkManager network;

        [SerializeField, Tooltip("접속 주소. 1인 양팔은 127.0.0.1(이 PC 안).")]
        string address = "127.0.0.1";

        [SerializeField, Tooltip("포트(CLAUDE.md 고정 7777).")]
        ushort port = 7777;

        [SerializeField, Tooltip("Play하면 바로 시작.")]
        bool startOnPlay = true;

        public SessionMode Mode => mode;
        /// <summary>시작하지 못한 이유(비면 문제 없음). 안내판이 읽는다.</summary>
        public string Problem { get; private set; } = string.Empty;
        public bool Running => network != null && network.IsListening;
        public event Action<string> ProblemChanged;

        void Start()
        {
            if (startOnPlay) Begin();
        }

        void OnDestroy()
        {
            if (network != null) network.OnTransportFailure -= OnTransportFailure;
        }

        /// <summary>세션 시작. 이미 돌고 있으면 그대로.</summary>
        public bool Begin()
        {
            if (network == null) network = GetComponent<NetworkManager>();
            if (network == null) network = NetworkManager.Singleton;
            if (network == null) return Fail("NetworkManager가 없습니다. 'Anseong → Setup → Add Network Session'을 실행하세요.");
            if (network.IsListening) return true;
            var transport = network.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null) return Fail("NetworkManager에 UnityTransport가 연결되지 않았습니다. 메뉴를 다시 실행하세요.");
            network.OnTransportFailure -= OnTransportFailure;
            network.OnTransportFailure += OnTransportFailure;

            if (mode != SessionMode.SoloBothArms)
                Debug.LogWarning($"[Session] {mode} 모드는 M3에서 만든다. 지금은 1인 양팔(호스트)로 시작합니다.", this);
            transport.SetConnectionData(address, port);
            if (!network.StartHost())
                return Fail($"호스트 시작 실패({address}:{port}). 다른 Unity 창이나 빌드가 포트 {port}을 쓰고 있으면 끄고 다시 Play하세요.");
            SetProblem(string.Empty);
            Debug.Log($"[Session] Host started (1인 양팔, {address}:{port})", this);
            return true;
        }

        void OnTransportFailure() => Fail("네트워크 전송이 끊겼습니다(포트 충돌 등). 다시 Play하세요.");

        bool Fail(string reason)
        {
            SetProblem(reason);
            Debug.LogError("[Session] " + reason, this);
            return false;
        }

        void SetProblem(string value)
        {
            if (Problem == value) return;
            Problem = value;
            ProblemChanged?.Invoke(value);
        }
    }
}
