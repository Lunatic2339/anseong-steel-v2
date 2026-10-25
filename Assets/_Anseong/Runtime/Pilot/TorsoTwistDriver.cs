using Anseong.Core;
using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>
    /// 사람 허리 회전(어림값)을 로봇 허리 뼈(Spine) yaw로 덮어쓴다. 팔 드라이버보다 먼저 돌아서 어깨가 같이 돈 뒤 팔 IK가 풀린다.
    /// 1인 양팔 모드에서만 쓴다(2인 모드는 두 조종사의 손이 다른 PC에 있어 M3에서 정한다). 보정 전이나 끄면 0으로 돌아온다.
    /// <para>조종석은 머리 뼈 위치를 따라가므로(CockpitSeat followWeight) 허리를 돌리면 시점도 조금 옮겨진다.</para>
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class TorsoTwistDriver : MonoBehaviour, ITunable
    {
        /// <summary>Animator 평가 뒤, 팔 드라이버(100) 전.</summary>
        public const int ExecutionOrder = 90;

        [SerializeField, Tooltip("로봇 정면 기준(배율 1 루트). 이 루트의 위쪽 축으로 돌린다.")]
        Transform robotRoot;

        [SerializeField, Tooltip("돌릴 허리 뼈(mixamorig:Spine). 메뉴가 채운다.")]
        Transform spine;

        [SerializeField, Tooltip("손 입력. 비어 있으면 시작할 때 씬에서 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Tooltip("개인 보정. 비어 있으면 시작할 때 씬에서 한 번 찾는다.")]
        PilotCalibrationFlow calibration;

        [SerializeField, Tooltip("허리 회전을 쓴다.")]
        bool twistEnabled = true;

        [SerializeField, Tooltip("허리 회전 추정 수치.")]
        TorsoTwistSettings settings = new TorsoTwistSettings();

        float yaw;
        float targetYaw;
        Quaternion baseLocal, lastWritten;
        bool written;

        /// <summary>지금 적용 중인 허리 yaw(도, 오른쪽 +).</summary>
        public float Yaw => yaw;

        public bool TwistEnabled
        {
            get => twistEnabled;
            set => twistEnabled = value;
        }

        void Start()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (robotRoot == null || spine == null)
            {
                Debug.LogWarning("[TorsoTwistDriver] 루트나 허리 뼈 참조가 비었습니다. Build Player Robot Prefab 메뉴를 다시 실행하세요.", this);
                enabled = false;
            }
        }

        void LateUpdate()
        {
            targetYaw = 0f;
            if (twistEnabled && input != null && calibration != null && calibration.IsComplete && input.ControlsBothArms)
            {
                var left = calibration.Get(PilotSide.Left);
                var right = calibration.Get(PilotSide.Right);
                var headYaw = calibration.TryGetHeadRotation(out var head) ? TorsoTwist.HeadYawFromBody(head, left.BodyYawDegrees) : 0f;
                targetYaw = TorsoTwist.EstimateYaw(left, right,
                    calibration.CompensateHand(input.Get(PilotSide.Left).Position), calibration.CompensateHand(input.Get(PilotSide.Right).Position),
                    headYaw, settings);
            }

            var dt = Time.deltaTime;
            yaw = settings.responseSeconds > 0f
                ? Mathf.Lerp(yaw, targetYaw, 1f - Mathf.Exp(-dt / settings.responseSeconds))
                : targetYaw;
            // 클립이 허리를 매 프레임 다시 쓰면 그 값을, 안 쓰면(지난 프레임 우리 값 그대로) 저장해 둔 원래 값을 기준으로 돌린다.
            // 그래서 어느 쪽이든 회전이 쌓이지 않는다.
            var current = spine.localRotation;
            if (written && Quaternion.Angle(current, lastWritten) < 0.001f) current = baseLocal;
            baseLocal = current;
            spine.localRotation = current;
            if (Mathf.Abs(yaw) >= 0.01f)
                spine.rotation = Quaternion.AngleAxis(yaw, robotRoot.up) * spine.rotation;
            lastWritten = spine.localRotation;
            written = true;
        }

        /// <summary>팔 매핑용: 이 허리 회전에서 side 쪽 사람 어깨가 움직인 양(트래킹 공간, m).</summary>
        public Vector3 HumanShoulderShift(PilotSide side)
        {
            if (calibration == null || Mathf.Abs(yaw) < 0.01f) return Vector3.zero;
            var left = calibration.Get(PilotSide.Left);
            var right = calibration.Get(PilotSide.Right);
            return TorsoTwist.ShoulderShift(side == PilotSide.Left ? left : right, left, right, yaw);
        }

        /// <summary>인게임 조절 메뉴: 허리 회전.</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "허리";
            list.AddToggle(g, "torso.enabled", "허리 회전", () => twistEnabled, v => twistEnabled = v);
            if (settings == null) return;
            var s = settings;
            list.AddFloat(g, "torso.handWeight", "손 비율", () => s.handWeight, v => s.handWeight = v, 0f, 1f, 0.05f);
            list.AddFloat(g, "torso.headWeight", "고개 비율", () => s.headWeight, v => s.headWeight = v, 0f, 1f, 0.05f);
            list.AddFloat(g, "torso.maxDegrees", "최대 각도(도)", () => s.maxDegrees, v => s.maxDegrees = v, 0f, 60f, 5f, 0);
            list.AddFloat(g, "torso.responseSeconds", "따라가는 시간(초)", () => s.responseSeconds, v => s.responseSeconds = v, 0f, 0.5f, 0.02f);
        }
    }
}
