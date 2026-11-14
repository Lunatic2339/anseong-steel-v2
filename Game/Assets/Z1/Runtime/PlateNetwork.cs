using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Anseong.Z1
{
    [Serializable] public class JoinData { public int protocol = 1; public Seat seat; public Variant variant; }
    [Serializable] public class InputFrame
    {
        public int epoch; public long sequence; public int command;
        public Vector3 left, right, move, ray;
        public bool tracked, scan, fire;
        public int camera;
    }

    public sealed class PlateNetwork : MonoBehaviour
    {
        const string InputChannel = "z1/input/1", StateChannel = "z1/state/1";
        public Seat LocalSeat { get; private set; }
        public Variant Mode { get; private set; }
        public PlateState State { get; private set; } = new PlateState();
        public string Status { get; private set; } = "연결 전";
        public bool Connected => manager != null && manager.IsConnectedClient;
        public bool Started => manager != null && manager.IsListening;
        public bool Server => manager != null && manager.IsServer;
        PlateSession rules;
        NetworkManager manager;
        long sequence;
        float sendTimer;
        double lastSupportMove;
        double Now => Time.realtimeSinceStartupAsDouble;
        public static readonly Vector3 Target = new Vector3(8, 26, 8);
        public static Vector3 ControlPosition(int index) => index == 0 ? new Vector3(-14, 32, -14) : new Vector3(-12, 30, 2);

        public bool Connect(bool host, Seat seat, Variant variant, string address, ushort port)
        {
            if (Started || manager != null) { Status = "연결 종료 / 다시 선택을 먼저 누르세요."; return false; }
            LocalSeat = seat; Mode = variant; sequence = 0; State = new PlateState();
            var go = new GameObject("Z1 Network");
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData(address, port, host ? "0.0.0.0" : null);
            manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ConnectionApproval = true, ForceSamePrefabs = false, TickRate = 30 };
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new JoinData { seat = seat, variant = variant }));
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
            if (host) { rules = new PlateSession(variant); rules.Join(NetworkManager.ServerClientId, seat); State = rules.State; }
            var ok = host ? manager.StartHost() : manager.StartClient();
            if (ok)
            {
                manager.CustomMessagingManager.RegisterNamedMessageHandler(InputChannel, ReceiveInput);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(StateChannel, ReceiveState);
            }
            Status = ok ? "연결 대기 · 양쪽 준비 필요" : "시작 실패 · 포트/주소 확인";
            lastSupportMove = Now;
            return ok;
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Pending = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }
            try
            {
                var data = JsonUtility.FromJson<JoinData>(Encoding.UTF8.GetString(request.Payload));
                response.Approved = data != null && data.protocol == 1 && data.variant == Mode && rules.Join(request.ClientNetworkId, data.seat);
            }
            catch { response.Approved = false; }
            response.Reason = response.Approved ? "" : "같은 3/4안과 서로 다른 역할이 필요합니다 (최대 2명).";
        }
        void OnConnected(ulong id) { Status = "연결됨 · 준비를 눌러 시작"; }
        void OnDisconnected(ulong id)
        {
            if (manager == null) return;
            if (Server) { rules.Leave(id); State = rules.State; }
            Status = string.IsNullOrEmpty(manager.DisconnectReason) ? "연결 끊김 · 재접속 후 양쪽 준비" : manager.DisconnectReason;
        }
        public void Disconnect()
        {
            if (manager != null) { var go = manager.gameObject; manager.Shutdown(); manager = null; Destroy(go); }
            Status = "연결 종료";
        }
        void OnDestroy() { if (manager != null) manager.Shutdown(); }
        public void Send(InputFrame frame)
        {
            if (!Connected) return;
            frame.epoch = State.epoch; frame.sequence = ++sequence;
            if (Server) Apply(NetworkManager.ServerClientId, frame);
            else SendJson(InputChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(frame));
        }
        void SendJson(string channel, ulong id, string json)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                manager.CustomMessagingManager.SendNamedMessage(channel, id, writer, NetworkDelivery.ReliableSequenced);
            }
        }
        void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            if (!Server || reader.Length > 4096) return;
            try { reader.ReadValueSafe(out string json); Apply(sender, JsonUtility.FromJson<InputFrame>(json)); }
            catch (Exception e) { Debug.LogWarning("Rejected Z1 input: " + e.GetType().Name); }
        }
        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (Server || sender != NetworkManager.ServerClientId || reader.Length > 4096) return;
            try { reader.ReadValueSafe(out string json); var incoming = JsonUtility.FromJson<PlateState>(json); if (incoming != null && incoming.epoch >= State.epoch) State = incoming; }
            catch (Exception e) { Debug.LogWarning("Rejected Z1 snapshot: " + e.GetType().Name); }
        }
        void Apply(ulong sender, InputFrame f)
        {
            if (f == null || f.sequence < 0 || f.sequence > long.MaxValue / 2) return;
            if (f.command == 1 || f.command == 2)
            {
                rules.Command(sender, f.epoch, f.sequence * 2, f.command == 1 ? Request.Ready : Request.Reset);
                State = rules.State; return;
            }
            if (rules.Owns(sender, Seat.Mech))
                rules.Pose(sender, f.epoch, f.sequence * 2, f.left, f.right, f.tracked, Now);
            else if (rules.Owns(sender, Seat.Support))
            {
                if (f.epoch != rules.State.epoch || !PlateSession.Finite(f.move) || !PlateSession.Finite(f.ray)) return;
                var dt = (float)Math.Clamp(Now - lastSupportMove, 0, .1);
                lastSupportMove = Now;
                var pos = rules.State.drone;
                if (Mode == Variant.Drone && rules.State.Running)
                {
                    var delta = Vector3.ClampMagnitude(f.move, 1) * (6 * dt);
                    if (delta.sqrMagnitude > 0 && Physics.SphereCast(pos, .4f, delta.normalized, out var hit, delta.magnitude)) delta = delta.normalized * Mathf.Max(0, hit.distance - .05f);
                    pos += delta;
                    pos = new Vector3(Mathf.Clamp(pos.x,-25,25),Mathf.Clamp(pos.y,18,40),Mathf.Clamp(pos.z,-15,30));
                }
                var origin = Mode == Variant.Drone ? pos : ControlPosition(f.camera == 0 ? 0 : 1);
                var dir = f.ray.sqrMagnitude > .1f ? f.ray.normalized : Vector3.forward;
                var aimed = Physics.Raycast(origin, dir, out var targetHit, 80) && targetHit.collider.gameObject.name == "ReleaseTarget";
                if (Mode == Variant.Control && Vector3.Angle((Target - origin).normalized, dir) > 40) aimed = false;
                if (rules.Support(sender, f.epoch, f.sequence * 2, aimed, f.scan, Now))
                {
                    rules.State.drone = pos;
                    if (f.fire) rules.Command(sender, f.epoch, f.sequence * 2 + 1, Request.Release);
                }
            }
            State = rules.State;
        }
        void Update()
        {
            if (!Server) return;
            rules.Tick(Mathf.Min(Time.unscaledDeltaTime, .1f), Now); State = rules.State;
            sendTimer += Time.unscaledDeltaTime;
            if (sendTimer < .05f) return;
            sendTimer = 0;
            var json = JsonUtility.ToJson(State);
            foreach (var id in manager.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) SendJson(StateChannel, id, json);
        }
    }
}
