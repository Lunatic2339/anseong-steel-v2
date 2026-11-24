using System;
using System.Reflection;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>차폐판을 어떻게 쓸지. 헤드셋으로 둘 다 해 보고 정한다.</summary>
    public enum ShieldMode
    {
        /// <summary>늘 펴 둔다. 왼 트리거만 쥐면 막는다.</summary>
        AlwaysOpen,
        /// <summary>오른손 A(키보드 F8)로 펴고 접는다. 다 펴진 동안에만 막을 수 있다.</summary>
        Button,
    }

    /// <summary>
    /// 교전 시험 장면의 로봇 팔(임시 연결). 헤드셋을 쓴 P1의 PC에서는 두 손을 따라가고,
    /// 그 밖(키보드, 자동 조작, P2의 PC)에서는 교전 상태에 맞는 자세(대기 / 막기 / 주먹)를 잡는다.
    /// 개인 보정 없이 팔 길이를 어림값으로 쓴다. 본 게임에서는 보정을 거치는 <see cref="RobotArmDriver"/>로 바꾼다.
    /// 손목은 건드리지 않는다(성재 11/24: 손목이 움직이면 손이 차폐판을 뚫고 나온다, 고정하면 괜찮다).
    /// 뼈 참조는 메뉴 'Anseong → Sandbox → Build Scene 1 Duel'이 채운다.
    /// </summary>
    [DefaultExecutionOrder(RobotArmDriver.ExecutionOrder)]
    public class SkirmishMechArms : MonoBehaviour
    {
        [Serializable]
        public class Arm
        {
            public Transform upper, fore, hand;
            [Tooltip("손 뼈 로컬 기준 주먹 정면·손등. 메뉴가 손가락 위치로 잰다.")]
            public Vector3 fistForwardLocal = Vector3.up, fistBackLocal = Vector3.forward;
            [NonSerialized] public TwoBoneArmSolver solver;
            [NonSerialized] public Vector3 local;
            [NonSerialized] public bool started;
        }

        [SerializeField, Tooltip("로봇 정면 기준(정면 = +Z).")] Transform body;
        [SerializeField] Arm left = new Arm(), right = new Arm();

        [Header("손 따라가기(헤드셋)")]
        [SerializeField, Min(0.3f), Tooltip("사람 팔 길이 어림값(m). 보정이 없어서 모두에게 같은 값을 쓴다.")] float humanReach = 0.6f;
        [SerializeField, Tooltip("눈에서 어깨까지(m, 몸 정면 기준). X는 바깥쪽.")] Vector3 eyeToShoulder = new Vector3(0.18f, -0.22f, -0.05f);

        [Header("자세(헤드셋 없이 볼 때). 어깨 기준, 팔 길이 배수. X = 몸 안쪽(+), Y = 위, Z = 앞")]
        [SerializeField] Vector3 readyPose = new Vector3(0.05f, -0.35f, 0.6f);
        [SerializeField, Tooltip("왼팔: 왼쪽에서 오는 공격을 막는 자리.")] Vector3 guardLeftPose = new Vector3(-0.1f, 0.15f, 0.75f);
        [SerializeField, Tooltip("왼팔: 오른쪽에서 오는 공격을 막는 자리.")] Vector3 guardRightPose = new Vector3(0.55f, 0.1f, 0.65f);
        [SerializeField, Tooltip("오른팔: 주먹을 뻗은 자리.")] Vector3 punchPose = new Vector3(0.2f, 0.05f, 0.97f);
        [SerializeField, Min(0.1f), Tooltip("자세가 바뀌는 빠르기(팔 길이/초).")] float poseSpeed = 5f;

        [Header("차폐판")]
        [SerializeField] ShieldMode shieldMode = ShieldMode.Button;
        [SerializeField, Tooltip("성재 전달본의 ShieldController. 다른 어셈블리라 이름으로 찾는다.")] Component shield;

        readonly ArmSolverSettings settings = new ArmSolverSettings();
        bool handsTracked, guarding, punching, localPilot;
        PilotSide guardSide;
        Vector3 leftHandLocal, rightHandLocal;
        FieldInfo shieldLocalInput;
        PropertyInfo shieldProgress;
        MethodInfo shieldSetDeployed;

        public ShieldMode ShieldMode { get => shieldMode; set => shieldMode = value; }

        /// <summary>차폐판으로 막을 수 있는 상태(다 펴짐). 차폐판이 없는 로봇이면 늘 참.</summary>
        public bool ShieldReady => shield == null || shieldProgress == null || (float)shieldProgress.GetValue(shield) >= 0.99f;

        public void Bind(Transform bodyRoot, Arm leftArm, Arm rightArm, Component shieldController)
        {
            body = bodyRoot; left = leftArm; right = rightArm; shield = shieldController;
        }

        /// <summary>이 PC의 사람이 P1으로 직접 조종하는지. 아니면 차폐판 버튼을 받지 않고 펴 둔다.</summary>
        public void SetLocalPilot(bool value) => localPilot = value;

        /// <summary>헤드셋의 두 손(월드)과 눈 위치, 몸 정면(XR Origin 회전).</summary>
        public void SetHands(Vector3 eye, Quaternion facing, Vector3 leftHand, Vector3 rightHand)
        {
            var inverse = Quaternion.Inverse(facing);
            leftHandLocal = inverse * (leftHand - eye) - new Vector3(-eyeToShoulder.x, eyeToShoulder.y, eyeToShoulder.z);
            rightHandLocal = inverse * (rightHand - eye) - eyeToShoulder;
            handsTracked = true;
        }

        public void ClearHands() => handsTracked = false;

        /// <summary>교전 상태에서 온 자세.</summary>
        public void SetPose(bool isGuarding, PilotSide side, bool isPunching)
        {
            guarding = isGuarding; guardSide = side; punching = isPunching;
        }

        void Awake() => Prepare();

        void Prepare()
        {
            if (left.solver != null || body == null) return;
            Prepare(left, PilotSide.Left);
            Prepare(right, PilotSide.Right);
            if (shield == null) return;
            var type = shield.GetType();
            shieldLocalInput = type.GetField("enableLocalInput");
            shieldProgress = type.GetProperty("Progress");
            shieldSetDeployed = type.GetMethod("SetDeployed");
        }

        void Prepare(Arm arm, PilotSide side)
        {
            if (arm.upper == null || arm.fore == null || arm.hand == null) return;
            var bind = ArmBind.FromPose(arm.upper.position, arm.fore.position, arm.hand.position,
                arm.upper.rotation, arm.fore.rotation, arm.hand.rotation, arm.fistForwardLocal, arm.fistBackLocal, side, body.rotation);
            if (bind.IsValid) arm.solver = new TwoBoneArmSolver(bind, settings);
        }

        void LateUpdate() => Apply(Time.deltaTime, false);

        /// <param name="snap">자세를 바로 잡는다(에디터에서 화면을 찍을 때).</param>
        public void Apply(float dt, bool snap)
        {
            Prepare();
            if (shield != null)
            {
                bool byButton = shieldMode == ShieldMode.Button && localPilot;
                shieldLocalInput?.SetValue(shield, byButton);
                if (!byButton) shieldSetDeployed?.Invoke(shield, new object[] { true });
            }
            Solve(left, -1f, handsTracked ? leftHandLocal : default,
                guarding ? (guardSide == PilotSide.Left ? guardLeftPose : guardRightPose) : readyPose, dt, snap);
            Solve(right, 1f, handsTracked ? rightHandLocal : default, punching ? punchPose : readyPose, dt, snap);
        }

        void Solve(Arm arm, float sideSign, Vector3 handLocal, Vector3 pose, float dt, bool snap)
        {
            if (arm.solver == null) return;
            float length = arm.solver.Bind.Total;
            Vector3 target;
            if (handsTracked) target = handLocal * (length / humanReach);
            else
            {
                // 자세의 X는 몸 안쪽이 +이므로 왼팔은 +X, 오른팔은 −X가 안쪽이다.
                var goal = new Vector3(-sideSign * pose.x, pose.y, pose.z) * length;
                target = arm.started && !snap ? Vector3.MoveTowards(arm.local, goal, poseSpeed * length * dt) : goal;
            }
            if (snap || !arm.started) arm.solver.Reset();
            arm.local = target;
            arm.started = true;

            var facing = body.rotation;
            var fist = facing * Quaternion.LookRotation(target.sqrMagnitude > 1e-6f ? target.normalized : Vector3.forward, Vector3.up);
            var solved = arm.solver.Solve(arm.upper.position, arm.upper.position + facing * target, fist, facing, 1f, 0f, snap ? 1f : dt);
            arm.upper.rotation = solved.Upper;
            arm.fore.rotation = solved.Lower;
            // 손 뼈는 그대로 둔다(손목 고정).
        }
    }
}
