using System.Collections.Generic;
using Anseong.Combat;
using Anseong.Pilot;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Net
{
    /// <summary>
    /// 전투 서버(M2-01). 규칙(<see cref="CombatAuthority"/>)은 서버에서만 돌리고, 시각은 <c>NetworkManager.ServerTime</c>을 쓴다.
    /// 때린 쪽 PC의 <see cref="CombatDirector"/>는 <see cref="ICombatLink"/>로 보고만 한다. 호스트에서는 RPC가 같은 PC 안에서 바로 불리고,
    /// M3 PC-B에서는 같은 RPC가 네트워크를 탄다(1인 전용 경로 없음).
    /// 결과: 보스 HP·연계는 <see cref="NetworkVariable{T}"/>로, 사건은 모든 PC에 이벤트 RPC로. 조종사 한 명에게만 보내는 길
    /// (<see cref="SendToPilot"/>)도 열어 두었다(좌우 정보 비대칭: 교차 예고 등).
    /// 메뉴 'Anseong → Setup → Add Network Session'이 씬에 NetworkObject와 함께 붙인다.
    /// </summary>
    public class CombatServer : NetworkBehaviour, ICombatLink
    {
        [SerializeField, Tooltip("이 PC의 전투 판정(보고하는 쪽, 결과를 듣는 쪽). 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        readonly NetworkVariable<NetCombatSnapshot> state = new NetworkVariable<NetCombatSnapshot>();
        readonly List<CombatEvent> events = new List<CombatEvent>(16);
        // 좌/우 팔 주인 PC(M3-01). 1인 양팔은 둘 다 서버 자신, 호스트 모드는 왼팔 = 호스트, 오른팔 = 접속한 PC-B.
        ulong[] pilotClient = { Unity.Netcode.NetworkManager.ServerClientId, Unity.Netcode.NetworkManager.ServerClientId };
        readonly bool[] warnedNotOwner = new bool[2];
        CombatAuthority authority;
        bool watchingClients;

        /// <summary>서버에서: 그 팔의 주인 PC(<see cref="SessionRules.NoOwner"/> = 아직 없음).</summary>
        public ulong PilotClient(PilotSide side) => pilotClient[(int)side];

        /// <summary>서버에서만 있다. 보스 공격·페이즈(M2-02~)도 이것을 통해 같은 규칙에 닿는다.</summary>
        public CombatAuthority Authority => authority;
        public CombatDirector Director => director;

        /// <summary>판정 시각(초). 서버·클라이언트 모두 서버 시계 기준.</summary>
        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        public override void OnNetworkSpawn()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (IsServer)
            {
                authority = director != null ? director.CreateAuthority() : new CombatAuthority(new CombatResolver());
                state.Value = new NetCombatSnapshot(authority.Snapshot);
                var session = FindAnyObjectByType<SessionBootstrap>();
                pilotClient = SessionRules.InitialOwners(session != null ? session.Mode : SessionMode.SoloBothArms, Unity.Netcode.NetworkManager.ServerClientId);
                warnedNotOwner[0] = warnedNotOwner[1] = false;
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                watchingClients = true;
            }
            state.OnValueChanged += OnState;
            if (director == null)
            {
                Debug.LogWarning("[CombatServer] 씬에 CombatDirector가 없어 이 PC는 보고·결과를 받지 않습니다.", this);
                return;
            }
            director.ReceiveState(state.Value.Value);
            director.Connect(this);
        }

        public override void OnNetworkDespawn()
        {
            if (watchingClients && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            watchingClients = false;
            state.OnValueChanged -= OnState;
            if (director != null) director.Disconnect(this);
            authority = null;
        }

        void OnState(NetCombatSnapshot previous, NetCombatSnapshot current)
        {
            if (director != null) director.ReceiveState(current.Value);
        }

        void Update()
        {
            if (!IsServer || authority == null) return;
            authority.Tick(Now, events);
            Flush();
        }

        // ── 보고 (때린 쪽 → 서버) ──

        public void SubmitHit(HitReport report) => HitRpc(new NetHitReport(report));
        public void SubmitMiss(PilotSide side, PunchKind kind, int strikeId) => MissRpc((byte)side, (byte)kind, strikeId);
        public void SubmitArmInput(PilotSide side, bool gripHeld, bool triggerPressed) => ArmInputRpc((byte)side, gripHeld, triggerPressed);
        public void RequestReset() => ResetRpc();
        public void SubmitGuard(PilotSide side, bool guarding) => GuardRpc((byte)side, guarding);
        public void SubmitFinisherReady(PilotSide side, bool ready) => FinisherReadyRpc((byte)side, ready);

        [Rpc(SendTo.Server)]
        void FinisherReadyRpc(byte side, bool ready, RpcParams rpc = default)
        {
            authority?.SetFinisherReady((PilotSide)side, ready, Now);
        }
        public void SubmitStick(PilotSide side, Vector2 stick) => StickRpc((byte)side, Quantize(stick.x), Quantize(stick.y));

        static sbyte Quantize(float v) => (sbyte)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 127f);

        [Rpc(SendTo.Server)]
        void GuardRpc(byte side, bool guarding, RpcParams rpc = default)
        {
            authority?.SetGuard((PilotSide)side, guarding);
        }

        [Rpc(SendTo.Server)]
        void StickRpc(byte side, sbyte x, sbyte y, RpcParams rpc = default)
        {
            if (authority == null) return;
            authority.SetStick((PilotSide)side, new Vector2(x / 127f, y / 127f), Now, events);
            Flush();
        }

        // ── 팔 주인 (M3-01) ──

        void OnClientConnected(ulong clientId)
        {
            if (clientId == Unity.Netcode.NetworkManager.ServerClientId) return;
            if (SessionRules.AssignJoiner(pilotClient, clientId))
            {
                warnedNotOwner[(int)PilotSide.Right] = false;
                Debug.Log($"[CombatServer] 오른팔 주인 = 클라이언트 {clientId}", this);
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (SessionRules.ReleaseLeaver(pilotClient, clientId))
                Debug.LogWarning($"[CombatServer] 클라이언트 {clientId}가 나가 그 팔의 보고를 받지 않습니다(다시 접속하면 돌아옴).", this);
        }

        /// <summary>보낸 PC가 그 팔 주인인지. 아니면 버리고 팔마다 한 번만 경고.</summary>
        bool FromOwner(PilotSide side, in RpcParams rpc)
        {
            if (SessionRules.IsOwner(pilotClient, side, rpc.Receive.SenderClientId)) return true;
            if (!warnedNotOwner[(int)side])
            {
                warnedNotOwner[(int)side] = true;
                Debug.LogWarning($"[CombatServer] {side} 팔 주인이 아닌 PC(클라이언트 {rpc.Receive.SenderClientId})의 보고를 버립니다.", this);
            }
            return false;
        }

        [Rpc(SendTo.Server)]
        void HitRpc(NetHitReport report, RpcParams rpc = default)
        {
            if (authority == null || !FromOwner(report.Value.Side, rpc)) return;
            authority.SubmitHit(report.Value, Now, events);
            Flush();
        }

        [Rpc(SendTo.Server)]
        void MissRpc(byte side, byte kind, int strikeId, RpcParams rpc = default)
        {
            if (authority == null || !FromOwner((PilotSide)side, rpc)) return;
            authority.SubmitMiss((PilotSide)side, (PunchKind)kind, strikeId, Now, events);
            Flush();
        }

        [Rpc(SendTo.Server)]
        void ArmInputRpc(byte side, bool gripHeld, bool triggerPressed, RpcParams rpc = default)
        {
            if (authority == null || !FromOwner((PilotSide)side, rpc)) return;
            authority.SetArmInput((PilotSide)side, gripHeld, triggerPressed);
        }

        [Rpc(SendTo.Server)]
        void ResetRpc(RpcParams rpc = default)
        {
            if (authority == null) return;
            authority.Reset();
            ServerReset?.Invoke();
            Flush();
            Debug.Log("[CombatServer] 서버 초기화: 보스 HP·연계·부스트", this);
        }

        /// <summary>서버에서: 보내기 직전의 모든 사건(보스 두뇌가 부스트 → 밀려남 같은 반응을 붙인다).</summary>
        public event System.Action<CombatEvent> ServerCue;

        /// <summary>서버에서: 재시작 때 같이 초기화할 서버 쪽 규칙(보스 공격·페이즈 등)이 듣는다.</summary>
        public event System.Action ServerReset;

        // ── 결과 (서버 → PC) ──

        /// <summary>서버에서: 쌓인 사건을 모든 PC에 보내고 상태를 복제한다. 서버 규칙을 더 붙이면 사건을 Events에 넣고 이것을 부른다.</summary>
        public void Flush()
        {
            if (!IsServer || authority == null) return;
            // 서버 쪽 규칙이 사건을 듣고 새 사건을 더하면(부스트 → 밀려남 등) 그것까지 이어서 보낸다.
            for (var round = 0; round < 4 && events.Count > 0; round++)
            {
                // 보내는 중에 듣는 쪽이 다시 보고하면(호스트는 바로 돌아옴) 목록이 바뀌므로 복사해서 보낸다.
                var batch = events.ToArray();
                events.Clear();
                foreach (var e in batch) ServerCue?.Invoke(e);
                var snapshot = new NetCombatSnapshot(authority.Snapshot);
                foreach (var e in batch) CueRpc(new NetCombatEvent(e), snapshot);
            }
            var current = new NetCombatSnapshot(authority.Snapshot);
            if (!state.Value.Equals(current)) state.Value = current;
        }

        /// <summary>서버에서: 서버 규칙이 사건을 더할 목록. 더한 뒤 <see cref="Flush"/>.</summary>
        public List<CombatEvent> Events => events;

        /// <summary>서버에서: 한 조종사 PC에만 보내는 사건(교차 예고처럼 좌우가 다른 정보를 받을 때).</summary>
        public void SendToPilot(PilotSide side, CombatEvent e)
        {
            if (!IsServer || authority == null || pilotClient[(int)side] == SessionRules.NoOwner) return;
            PilotCueRpc(new NetCombatEvent(e), new NetCombatSnapshot(authority.Snapshot),
                RpcTarget.Single(pilotClient[(int)side], RpcTargetUse.Temp));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void CueRpc(NetCombatEvent e, NetCombatSnapshot snapshot)
        {
            if (director != null) director.ReceiveServer(e.Value, snapshot.Value);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PilotCueRpc(NetCombatEvent e, NetCombatSnapshot snapshot, RpcParams rpc)
        {
            if (director != null) director.ReceiveServer(e.Value, snapshot.Value);
        }
    }
}
