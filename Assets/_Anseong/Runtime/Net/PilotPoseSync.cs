using Anseong.Core;
using Anseong.Pilot;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Anseong.Net
{
    /// <summary>네트워크 포장(M3-02). 칸을 더하면 여기도 더한다.</summary>
    public struct NetPilotPose : INetworkSerializable
    {
        public byte Side;
        public PilotPose Value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Side);
            s.SerializeValue(ref Value.Time);
            s.SerializeValue(ref Value.Upper);
            s.SerializeValue(ref Value.Lower);
            s.SerializeValue(ref Value.Hand);
            s.SerializeValue(ref Value.HeadPosition);
            s.SerializeValue(ref Value.HeadRotation);
            s.SerializeValue(ref Value.LeftHandPosition);
            s.SerializeValue(ref Value.LeftHandRotation);
            s.SerializeValue(ref Value.RightHandPosition);
            s.SerializeValue(ref Value.RightHandRotation);
        }
    }

    /// <summary>
    /// 상대 조종사 팔·머리·양손 동기화(M3-02, 02 "머리·양손 자세: 비신뢰 60Hz → 상대 PC가 100ms 버퍼로 보간").
    /// 자기 팔은 로컬 입력으로 바로 그리고(RobotArmDriver) 여기서는 보내기만 한다. 상대 팔은 받은 뼈 회전을 100ms 늦게 보간해
    /// 팔 IK 뒤에 덮어쓴다. 1초에 60번, 끊기면(0.5초) 그 팔은 대기 자세로 돌아간다. 1인 양팔은 보내지도 받지도 않는다.
    /// 상대 조종사 머리·양손은 조종석 기준으로 받아 <see cref="remoteHead"/> 등에 놓는다(비어 있으면 안 그림, 조종사 아트는 나중).
    /// 메뉴 'Add Network Session'이 Combat Server 오브젝트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class PilotPoseSync : NetworkBehaviour, ITunable
    {
        /// <summary>팔 IK(100)·가드(150) 뒤, 전투 판정(200) 앞.</summary>
        public const int ExecutionOrder = 160;

        [SerializeField, Tooltip("네트워크 세션(모드 읽기). 비어 있으면 한 번 찾는다.")]
        SessionBootstrap session;

        [SerializeField, Tooltip("왼팔·오른팔 IK(뼈를 읽고 쓴다). 비어 있으면 한 번 찾는다.")]
        RobotArmDriver[] arms = new RobotArmDriver[0];

        [SerializeField, Tooltip("조종석(머리·양손 기준). 비어 있으면 한 번 찾는다.")]
        CockpitSeat cockpit;

        [SerializeField, Tooltip("이 PC XR Origin(머리·손 읽기). 비어 있으면 한 번 찾는다.")]
        XROrigin xrOrigin;

        [SerializeField, Tooltip("이 PC 조종사 손 입력. 비어 있으면 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Range(10, 90), Tooltip("보내는 횟수(초당). 02: 60.")]
        int sendRate = 60;

        [SerializeField, Range(0f, 0.3f), Tooltip("받은 자세를 이만큼(초) 늦게 그린다. 02: 0.1. 키우면 끊김에 강하고 늦어 보인다.")]
        float interpolationDelay = 0.1f;

        [SerializeField, Range(0.1f, 2f), Tooltip("이 시간(초) 동안 새 자세가 안 오면 끊김으로 보고 상대 팔을 대기 자세로 둔다.")]
        float staleSeconds = 0.5f;

        [Header("상대 조종사 표시 (비면 안 그림)")]
        [SerializeField, Tooltip("상대 조종사 머리 표시(조종석 안). 메뉴가 임시 구를 만든다.")]
        Transform remoteHead;
        [SerializeField] Transform remoteLeftHand;
        [SerializeField] Transform remoteRightHand;
        [SerializeField, Tooltip("상대 조종사 머리·손 표시를 켠다.")]
        bool showRemotePilot = true;

        readonly PoseBuffer[] buffers = { new PoseBuffer(), new PoseBuffer() };
        readonly RobotArmDriver[] armBySide = new RobotArmDriver[2];
        readonly bool[] receiving = new bool[2];
        double nextSend;
        bool found;

        /// <summary>그 팔을 상대 PC 자세로 그리는 중(끊기지 않음).</summary>
        public bool IsReceiving(PilotSide side) => receiving[(int)side];

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        void FindOnce()
        {
            if (found) return;
            found = true;
            if (session == null) session = FindAnyObjectByType<SessionBootstrap>();
            if (cockpit == null) cockpit = FindAnyObjectByType<CockpitSeat>();
            if (xrOrigin == null) xrOrigin = FindAnyObjectByType<XROrigin>();
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (arms == null || arms.Length == 0) arms = FindObjectsByType<RobotArmDriver>(FindObjectsSortMode.None);
            foreach (var arm in arms)
                if (arm != null) armBySide[(int)arm.Side] = arm;
        }

        public override void OnNetworkSpawn()
        {
            FindOnce();
            buffers[0].Clear();
            buffers[1].Clear();
            nextSend = 0d;
        }

        public override void OnNetworkDespawn()
        {
            receiving[0] = receiving[1] = false;
            ShowRemote(false);
        }

        SessionMode Mode => session != null ? session.Mode : SessionMode.SoloBothArms;

        void LateUpdate()
        {
            if (!IsSpawned || Mode == SessionMode.SoloBothArms)
            {
                receiving[0] = receiving[1] = false;
                ShowRemote(false);
                return;
            }
            var now = Now;
            if (now >= nextSend)
            {
                nextSend = now + 1d / Mathf.Max(1, sendRate);
                Send(now);
            }
            Apply(now - interpolationDelay);
        }

        // ── 보내기 (자기 팔) ──

        void Send(double now)
        {
            var side = SessionRules.ArmSide(Mode);
            var arm = armBySide[(int)side];
            if (arm == null || arm.RobotRoot == null || arm.UpperArmBone == null) return;
            var root = Quaternion.Inverse(arm.RobotRoot.rotation);
            var pose = new PilotPose
            {
                Time = now,
                Upper = root * arm.UpperArmBone.rotation,
                Lower = root * arm.ForeArmBone.rotation,
                Hand = root * arm.HandBone.rotation,
                HeadRotation = Quaternion.identity,
                LeftHandRotation = Quaternion.identity,
                RightHandRotation = Quaternion.identity,
            };
            var space = cockpit != null ? cockpit.transform : null;
            if (space != null && xrOrigin != null)
            {
                var cam = xrOrigin.Camera != null ? xrOrigin.Camera.transform : null;
                if (cam != null)
                {
                    pose.HeadPosition = space.InverseTransformPoint(cam.position);
                    pose.HeadRotation = Quaternion.Inverse(space.rotation) * cam.rotation;
                }
                var tracking = xrOrigin.CameraFloorOffsetObject != null ? xrOrigin.CameraFloorOffsetObject.transform : xrOrigin.transform;
                if (input != null)
                {
                    var l = input.Get(PilotSide.Left);
                    var r = input.Get(PilotSide.Right);
                    pose.LeftHandPosition = space.InverseTransformPoint(tracking.TransformPoint(l.Position));
                    pose.LeftHandRotation = Quaternion.Inverse(space.rotation) * tracking.rotation * l.Rotation;
                    pose.RightHandPosition = space.InverseTransformPoint(tracking.TransformPoint(r.Position));
                    pose.RightHandRotation = Quaternion.Inverse(space.rotation) * tracking.rotation * r.Rotation;
                }
            }
            PoseRpc(new NetPilotPose { Side = (byte)side, Value = pose });
        }

        // 클라이언트 → (서버가 전달) → 호스트, 호스트 → 클라이언트. 늦게 온 것은 버퍼가 버린다.
        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        void PoseRpc(NetPilotPose pose)
        {
            if (pose.Side > 1) return;
            var side = (PilotSide)pose.Side;
            // 이 PC가 맡은 팔이면 받지 않는다(자기 팔은 로컬 입력으로만).
            if (SessionRules.DrivesArm(Mode, side)) return;
            buffers[pose.Side].Add(pose.Value);
        }

        // ── 받기 (상대 팔) ──

        void Apply(double renderTime)
        {
            var anyRemote = false;
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                receiving[h] = false;
                if (SessionRules.DrivesArm(Mode, side)) continue;
                var buffer = buffers[h];
                if (!buffer.TrySample(renderTime, staleSeconds, out var pose)) continue;
                buffer.TrimBefore(renderTime);
                receiving[h] = true;
                var arm = armBySide[h];
                if (arm != null && arm.RobotRoot != null && arm.UpperArmBone != null)
                {
                    var root = arm.RobotRoot.rotation;
                    arm.UpperArmBone.rotation = root * pose.Upper;
                    arm.ForeArmBone.rotation = root * pose.Lower;
                    arm.HandBone.rotation = root * pose.Hand;
                }
                if (showRemotePilot && cockpit != null)
                {
                    var space = cockpit.transform;
                    Place(remoteHead, space, pose.HeadPosition, pose.HeadRotation);
                    Place(remoteLeftHand, space, pose.LeftHandPosition, pose.LeftHandRotation);
                    Place(remoteRightHand, space, pose.RightHandPosition, pose.RightHandRotation);
                    anyRemote = true;
                }
            }
            ShowRemote(anyRemote);
        }

        static void Place(Transform target, Transform space, Vector3 position, Quaternion rotation)
        {
            if (target == null) return;
            target.SetPositionAndRotation(space.TransformPoint(position), space.rotation * rotation);
        }

        void ShowRemote(bool on)
        {
            SetActive(remoteHead, on);
            SetActive(remoteLeftHand, on);
            SetActive(remoteRightHand, on);
        }

        static void SetActive(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        public void AddTuning(TuningList list)
        {
            const string g = "2인 동기화";
            list.Describe(g, "할 것: 두 PC에서 각자 팔 휘두르기\n볼 것: 상대 팔이 끊김 없이 따라옴, 내 팔은 그대로 즉시");
            list.AddFloat(g, "net.interpDelay", "상대 팔 늦게 그리기(초)", () => interpolationDelay, v => interpolationDelay = v, 0f, 0.3f, 0.02f, 2);
            list.AddToggle(g, "net.showPilot", "상대 조종사 머리·손 표시", () => showRemotePilot, v => showRemotePilot = v);
            list.AddCheck(g, "net.arm.smooth", "상대 팔이 끊김 없이 움직임");
            list.AddCheck(g, "net.arm.mine", "내 팔은 M1처럼 즉시 따라옴");
            list.AddCheck(g, "net.pilot", "상대 조종사 머리·손 위치가 맞음");
        }
    }
}
