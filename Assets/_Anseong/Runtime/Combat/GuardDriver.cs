using System;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 팔별 가드 판정(M1-06). 사람 손(몸 이동 보정 적용)과 보정값으로 <see cref="GuardRules"/>를 돌리고, 바뀔 때 <see cref="GuardChanged"/>를 낸다.
    /// 표시등·안내판은 이 이벤트와 <see cref="State"/>만 본다. M2의 막기 소리·진동·외골격도 같은 이벤트를 듣는 컴포넌트로 붙인다.
    /// 로봇 전완은 팔 IK(LateUpdate 100) 뒤에 읽는다.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public class GuardDriver : MonoBehaviour, ITunable
    {
        public const int ExecutionOrder = 150;

        [Header("연결 (메뉴가 채운다. 비어 있으면 시작할 때 한 번 찾는다)")]
        [SerializeField] PilotHandInput input;
        [SerializeField] PilotCalibrationFlow calibration;
        [SerializeField, Tooltip("왼팔·오른팔 IK. 로봇 기준 전완 각도를 읽는다.")]
        RobotArmDriver[] arms = new RobotArmDriver[0];

        [Header("판정 (01 초기값)")]
        [SerializeField] GuardSettings settings = new GuardSettings();

        [Header("개발용")]
        [SerializeField, Tooltip("가드 판정을 CSV로 남긴다(에디터·개발 빌드, persistentDataPath/logs/guard_<날짜시각>.csv).")]
        bool writeCsv = true;
        [SerializeField, Min(0.02f), Tooltip("CSV 기록 간격(초). 0.1 = 10Hz.")]
        float logInterval = 0.1f;
        [SerializeField, Tooltip("가드가 켜지고 꺼질 때 Console에 한 줄.")]
        bool logToConsole = true;

        readonly GuardResult[] results = new GuardResult[2];
        readonly RobotArmDriver[] armBySide = new RobotArmDriver[2];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GuardLog log;
        double nextLog;
#endif

        /// <summary>팔의 가드가 켜지거나(true) 꺼짐(false).</summary>
        public event Action<PilotSide, bool> GuardChanged;

        public GuardSettings Settings => settings;
        public GuardResult State(PilotSide side) => results[(int)side];
        public bool IsGuarding(PilotSide side) => results[(int)side].IsGuarding;
        public bool BothGuarding => GuardRules.BothGuarding(results[0], results[1]);
        /// <summary>이 손이 지금 로봇 팔을 맡는다(1인 양팔이면 둘 다).</summary>
        public bool UsesHand(PilotSide side) => input != null && input.IsArmHand(side);

        void Start()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (arms == null || arms.Length == 0) arms = FindObjectsByType<RobotArmDriver>(FindObjectsSortMode.None);
            foreach (var arm in arms)
                if (arm != null) armBySide[(int)arm.Side] = arm;
            if (input == null || calibration == null)
            {
                Debug.LogWarning("[GuardDriver] PilotHandInput·PilotCalibrationFlow를 찾지 못해 가드 판정을 끕니다. " +
                                 "'Anseong → Setup → Rebuild Robot + Pilot Input'을 실행하세요.", this);
                enabled = false;
            }
        }

        void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            log?.Dispose();
#endif
        }

        void LateUpdate()
        {
            var calibrated = calibration.IsComplete;
            var hasHead = calibration.TryGetHeadPosition(out var head);
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                var previous = results[h].IsGuarding;
                var result = input.IsArmHand(side) ? Evaluate(side, calibrated && hasHead, head, previous) : default;
                results[h] = result;
                if (result.IsGuarding == previous) continue;
                if (logToConsole)
                    Debug.Log($"[Guard] {side} {(result.IsGuarding ? "가드" : "풀림")} ({result.Reason}) 사람 {result.HumanTilt:F0}° 로봇 {result.RobotTilt:F0}°", this);
                GuardChanged?.Invoke(side, result.IsGuarding);
            }
            WriteDebug(calibrated);
        }

        GuardResult Evaluate(PilotSide side, bool ready, Vector3 head, bool wasGuarding)
        {
            var sample = input.Get(side);
            var arm = calibration.Get(side);
            var calibrated = ready && arm != null && arm.IsComplete;
            var body = calibrated ? arm.BodyRotation : Quaternion.identity;
            var robot = armBySide[(int)side];
            var data = new GuardInput
            {
                Calibrated = calibrated,
                Tracked = sample.Tracked,
                Hand = calibration.CompensateHand(sample.Position),
                Guard = calibrated ? arm.Guard : Vector3.zero,
                Head = calibration.CompensateHand(head),
                Body = body,
                HumanForearm = calibrated ? GuardRules.HumanForearm(sample.Rotation, arm.ReachRotation, body) : Vector3.zero,
                RobotForearm = robot != null ? robot.ForearmDirection : Vector3.zero,
                WasGuarding = wasGuarding,
            };
            return GuardRules.Evaluate(data, settings);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void WriteDebug(bool calibrated)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!writeCsv || !calibrated) return;
            var now = Time.timeAsDouble;
            if (now < nextLog) return;
            nextLog = now + logInterval;
            if (log == null)
            {
                log = new GuardLog(GuardLog.DefaultPath(Application.persistentDataPath, DateTime.Now));
                Debug.Log($"[GuardDriver] 가드 기록: {log.Path}", this);
            }
            for (var i = 0; i < 2; i++)
                if (input.IsArmHand((PilotSide)i)) log.Add(now, (PilotSide)i, results[i]);
            log.Pump();
#endif
        }

        /// <summary>인게임 조절 메뉴: 가드 판정.</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "가드";
            var s = settings;
            list.AddFloat(g, "guard.zoneX", "가드 영역 좌우 ±(m)", () => s.zoneHalfSize.x, v => s.zoneHalfSize.x = v, 0.05f, 0.6f, 0.01f);
            list.AddFloat(g, "guard.zoneY", "가드 영역 위아래 ±(m)", () => s.zoneHalfSize.y, v => s.zoneHalfSize.y = v, 0.05f, 0.6f, 0.01f);
            list.AddFloat(g, "guard.zoneZ", "가드 영역 앞뒤 ±(m)", () => s.zoneHalfSize.z, v => s.zoneHalfSize.z = v, 0.05f, 0.6f, 0.01f);
            list.AddFloat(g, "guard.minFrontOfFace", "얼굴보다 앞 최소(m)", () => s.minFrontOfFace, v => s.minFrontOfFace = v, -0.2f, 0.4f, 0.01f);
            list.AddFloat(g, "guard.maxForearmTilt", "전완 기울기 최대(도)", () => s.maxForearmTiltDegrees, v => s.maxForearmTiltDegrees = v, 5f, 90f, 5f, 0);
            list.AddChoice(g, "guard.forearmSource", "전완 기준", () => (int)s.forearmSource, v => s.forearmSource = (ForearmSource)v, new[] { "사람(컨트롤러)", "로봇(화면)", "둘 중 하나" });
            list.AddFloat(g, "guard.releaseMargin", "풀림 여유(m)", () => s.releaseMargin, v => s.releaseMargin = v, 0f, 0.1f, 0.01f);
            list.AddFloat(g, "guard.releaseDegrees", "풀림 여유(도)", () => s.releaseDegrees, v => s.releaseDegrees = v, 0f, 20f, 1f, 0);
        }
    }
}
