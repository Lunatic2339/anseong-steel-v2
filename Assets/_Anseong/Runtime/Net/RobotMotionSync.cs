using Anseong.Boss;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Pilot;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Net
{
    /// <summary>복제용 이동 한 번(서버 → 모든 PC).</summary>
    public struct NetMoveSegment : INetworkSerializable, System.IEquatable<NetMoveSegment>
    {
        public MoveSegment Value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var kind = (byte)Value.Kind;
            s.SerializeValue(ref kind);
            s.SerializeValue(ref Value.From);
            s.SerializeValue(ref Value.To);
            s.SerializeValue(ref Value.Start);
            s.SerializeValue(ref Value.Duration);
            if (s.IsReader) Value.Kind = (MoveKind)kind;
        }

        public bool Equals(NetMoveSegment o)
        {
            return Value.Kind == o.Value.Kind && Value.From == o.Value.From && Value.To == o.Value.To &&
                   Value.Start.Equals(o.Value.Start) && Value.Duration.Equals(o.Value.Duration);
        }
    }

    /// <summary>
    /// 협동 이동(M3-03). 서버가 두 조종사 보조 스틱(전투 서버가 이미 받는 회피 스틱)으로 접근·후퇴·회피를 정하고
    /// "언제 어디서 어디로" 한 번을 복제한다. 모든 PC가 같은 서버 시각으로 같은 곡선을 재생하므로 조종석(카메라)이 끊기지 않는다.
    /// 몸 자동 회전(보스가 정면 ±20° 밖이면 초당 30°)은 서버가 계산하고 각도를 복제, 클라이언트는 같은 속도로 따라 돈다.
    /// 이동 중에는 플레이어 로봇 Animator의 "Walking"을 켠다(파라미터가 있을 때만, 다리 층). 1인 양팔도 같은 길(호스트).
    /// 재시작(R)하면 처음 자리로. 메뉴 'Add Network Session'이 Combat Server 오브젝트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class RobotMotionSync : NetworkBehaviour, ITunable
    {
        /// <summary>팔 IK(100)·조종석(10000)보다 먼저 루트를 옮긴다.</summary>
        public const int ExecutionOrder = -50;
        static readonly int WalkingHash = Animator.StringToHash("Walking");

        [SerializeField, Tooltip("전투 서버(스틱·사건). 비어 있으면 같은 오브젝트에서 찾는다.")]
        CombatServer combat;

        [SerializeField, Tooltip("플레이어 로봇 루트. 비어 있으면 PlayerHitFeedback이 붙은 루트를 한 번 찾는다.")]
        Transform playerRoot;

        [SerializeField, Tooltip("보스. 비어 있으면 한 번 찾는다.")]
        BossTarget boss;

        [SerializeField] CoopMoveSettings settings = new CoopMoveSettings();

        [SerializeField, Tooltip("몸 자동 회전을 쓴다.")]
        bool autoTurn = true;

        [SerializeField, Tooltip("이동 중 걷기 클립(다리)을 켠다.")]
        bool walkAnimation = true;

        readonly NetworkVariable<NetMoveSegment> segment = new NetworkVariable<NetMoveSegment>();
        readonly NetworkVariable<float> yaw = new NetworkVariable<float>();

        Animator animator;
        bool hasWalking, bound, turning;
        double nextAllowed, lastDodgeOnset = double.NegativeInfinity;
        Vector3 startPosition;
        float startYaw, shownYaw;

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;
        public MoveSegment Current => segment.Value.Value;

        public override void OnNetworkSpawn()
        {
            FindOnce();
            if (playerRoot == null)
            {
                Debug.LogWarning("[RobotMotionSync] 플레이어 로봇 루트를 찾지 못해 협동 이동을 끕니다.", this);
                return;
            }
            startPosition = playerRoot.position;
            startYaw = playerRoot.eulerAngles.y;
            shownYaw = startYaw;
            if (IsServer)
            {
                segment.Value = new NetMoveSegment { Value = new MoveSegment { Kind = MoveKind.None, From = startPosition, To = startPosition, Start = Now } };
                yaw.Value = startYaw;
            }
            else shownYaw = yaw.Value;
        }

        public override void OnNetworkDespawn()
        {
            if (bound && combat != null) combat.ServerReset -= OnReset;
            bound = false;
            SetWalking(false);
        }

        void FindOnce()
        {
            if (combat == null) combat = GetComponent<CombatServer>();
            if (playerRoot == null)
            {
                var feedback = FindAnyObjectByType<PlayerHitFeedback>();
                if (feedback != null) playerRoot = feedback.transform;
                else
                {
                    var seat = FindAnyObjectByType<CockpitSeat>();
                    if (seat != null) playerRoot = seat.transform.root;
                }
            }
            if (boss == null) boss = FindAnyObjectByType<BossTarget>();
            if (playerRoot != null)
            {
                // 두 곳이 같이 옮기지 않게 M2-03 임시 미끄러짐은 끈다.
                var feedback = playerRoot.GetComponent<PlayerHitFeedback>();
                if (feedback != null) feedback.SlideEnabled = false;
                animator = playerRoot.GetComponentInChildren<Animator>();
                hasWalking = false;
                if (animator != null && animator.runtimeAnimatorController != null)
                    foreach (var p in animator.parameters)
                        if (p.nameHash == WalkingHash && p.type == AnimatorControllerParameterType.Bool) hasWalking = true;
            }
        }

        void Update()
        {
            if (!IsSpawned || playerRoot == null) return;
            var now = Now;
            if (IsServer) ServerTick(now);
            Apply(now);
        }

        void ServerTick(double now)
        {
            var authority = combat != null ? combat.Authority : null;
            if (authority == null) return;
            if (!bound)
            {
                combat.ServerReset += OnReset;
                bound = true;
            }
            var bossPosition = boss != null ? boss.transform.position : playerRoot.position + playerRoot.forward * 100f;

            // 회피: 두 스틱이 같은 좌/우로 "새로" 맞은 순간(M2-03 DodgeRules가 판정, 서버만).
            var dodge = authority.Dodge;
            var dodgeDirection = 0;
            if (dodge.OnsetTime > lastDodgeOnset)
            {
                lastDodgeOnset = dodge.OnsetTime;
                dodgeDirection = dodge.Direction;
            }
            var held = CoopMoveRules.ForwardBack(dodge.Stick(PilotSide.Left), dodge.Stick(PilotSide.Right), settings);
            var current = segment.Value.Value;
            if (!current.IsMoving(now) &&
                CoopMoveRules.TryPlan(now, held, dodgeDirection, playerRoot.position, playerRoot.rotation, bossPosition, settings, ref nextAllowed, out var next))
            {
                segment.Value = new NetMoveSegment { Value = next };
                if (next.Kind == MoveKind.Approach || next.Kind == MoveKind.Retreat)
                {
                    combat.Events.Add(new CombatEvent { Cue = CombatCue.MoveStart, Amount = (int)next.Kind, Time = now, Point = playerRoot.position });
                    combat.Flush();
                }
            }

            if (autoTurn)
            {
                var y = CoopMoveRules.TurnYaw(yaw.Value, playerRoot.position, bossPosition, Time.deltaTime, settings, ref turning);
                if (Mathf.Abs(Mathf.DeltaAngle(y, yaw.Value)) > 0.01f) yaw.Value = y;
            }
        }

        void Apply(double now)
        {
            var current = segment.Value.Value;
            var position = current.Kind == MoveKind.None ? current.To : current.Evaluate(now);
            if ((playerRoot.position - position).sqrMagnitude > 1e-8f) playerRoot.position = position;

            // 서버는 계산값 그대로, 클라이언트는 복제값을 같은 최대 속도로 따라간다(틱 사이 끊김 없이).
            var target = yaw.Value;
            if (IsServer) shownYaw = target;
            else
            {
                var diff = Mathf.DeltaAngle(shownYaw, target);
                shownYaw = Mathf.Abs(diff) > 45f ? target : Mathf.MoveTowardsAngle(shownYaw, target, settings.turnDegreesPerSecond * 1.5f * Time.deltaTime);
            }
            if (Mathf.Abs(Mathf.DeltaAngle(playerRoot.eulerAngles.y, shownYaw)) > 1e-3f)
                playerRoot.rotation = Quaternion.Euler(0f, shownYaw, 0f);

            SetWalking(walkAnimation && (current.Kind == MoveKind.Approach || current.Kind == MoveKind.Retreat) && current.IsMoving(now));
        }

        void SetWalking(bool on)
        {
            if (!hasWalking || animator == null) return;
            if (animator.GetBool(WalkingHash) != on) animator.SetBool(WalkingHash, on);
        }

        void OnReset()
        {
            if (!IsServer || playerRoot == null) return;
            nextAllowed = 0d;
            turning = false;
            lastDodgeOnset = combat != null && combat.Authority != null ? combat.Authority.Dodge.OnsetTime : double.NegativeInfinity;
            segment.Value = new NetMoveSegment { Value = new MoveSegment { Kind = MoveKind.None, From = startPosition, To = startPosition, Start = Now } };
            yaw.Value = startYaw;
        }

        public void AddTuning(TuningList list)
        {
            const string g = "협동 이동";
            list.Describe(g, "할 것: 두 스틱 같이 앞·뒤·옆으로, 한쪽만 밀기\n볼 것: 둘이 같이 밀 때만 묵직하게 한 걸음씩, 멀미 없음");
            list.AddFloat(g, "move.stepMeters", "한 걸음 거리(m)", () => settings.stepMeters, v => settings.stepMeters = v, 2f, 20f, 1f, 0);
            list.AddFloat(g, "move.stepSeconds", "한 걸음 시간(초)", () => settings.stepSeconds, v => settings.stepSeconds = v, 0.2f, 1.5f, 0.1f, 1);
            list.AddToggle(g, "move.autoTurn", "몸 자동 회전", () => autoTurn, v => autoTurn = v);
            list.AddToggle(g, "move.walkClip", "걷기 클립", () => walkAnimation, v => walkAnimation = v);
            list.AddCheck(g, "move.both", "둘이 같은 방향일 때만 움직임");
            list.AddCheck(g, "move.sick", "움직일 때 멀미 없음");
            list.AddCheck(g, "move.turn", "보스가 옆으로 가면 몸이 천천히 돎");
        }
    }
}
