using Anseong.Combat;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.Flow
{
    /// <summary>
    /// 운영자 재시작(M1-07): 보정·보스 HP·연계·부스트를 처음 상태로 돌린다. 이전 판 상태가 남지 않는다.
    /// 헤드셋을 쓴 채(키보드 없이): 두 스틱을 함께 눌러 1초 유지, 또는 조절 메뉴 "판" → "판 재시작". 키보드 R은 운영자용 예비.
    /// 시연 운영자 화면(M3)이 생기기 전까지 쓰는 최소판.
    /// </summary>
    public class SessionRestart : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("재시작 키(운영자 PC 키보드).")]
        Key restartKey = Key.R;

        [SerializeField, Tooltip("보정 흐름. 비어 있으면 시작할 때 한 번 찾는다.")]
        PilotCalibrationFlow calibration;

        [SerializeField, Tooltip("전투 판정(보스 HP·연계·부스트). 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField, Min(0.2f), Tooltip("두 스틱을 함께 누르고 이 시간(초) 유지하면 재시작(헤드셋용).")]
        float stickHoldSeconds = 1f;

        public int RestartCount { get; private set; }

        InputAction leftStick, rightStick;
        float heldSince = -1f;

        void Start()
        {
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
        }

        void OnEnable()
        {
            leftStick = new InputAction("RestartLeftStick", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
            rightStick = new InputAction("RestartRightStick", InputActionType.Button, "<XRController>{RightHand}/{Primary2DAxisClick}");
            leftStick.Enable();
            rightStick.Enable();
        }

        void OnDisable()
        {
            leftStick?.Dispose();
            rightStick?.Dispose();
            leftStick = rightStick = null;
        }

        void Update()
        {
            if (leftStick != null && leftStick.IsPressed() && rightStick.IsPressed())
            {
                if (heldSince < 0f) heldSince = Time.unscaledTime;
                else if (Time.unscaledTime - heldSince >= stickHoldSeconds)
                {
                    heldSince = float.PositiveInfinity; // 손을 뗄 때까지 한 번만
                    Restart();
                }
            }
            else heldSince = -1f;

            var keyboard = Keyboard.current;
            if (keyboard == null || restartKey == Key.None) return;
            var key = keyboard[restartKey];
            if (key != null && key.wasPressedThisFrame) Restart();
        }

        public void AddTuning(TuningList list)
        {
            const string g = "판";
            list.Describe(g, "두 스틱을 함께 1초 눌러도 재시작");
            // 값이 아니라 버튼처럼 쓴다: 켜는 순간 재시작하고 늘 "끔"으로 보인다(저장값도 끔).
            list.AddToggle(g, "session.restart", "판 재시작(켜면 바로: 보정·보스·HP 처음으로)", () => false, v => { if (v) Restart(); });
        }

        /// <summary>보정부터 다시. 보스 HP·연계·부스트를 채우고 비운다.</summary>
        [ContextMenu("Restart")]
        public void Restart()
        {
            if (director != null) director.ResetBoss();
            if (calibration != null) calibration.Restart();
            RestartCount++;
            Debug.Log($"[SessionRestart] 재시작 {RestartCount}번째: 보정·보스 HP·연계 초기화", this);
        }
    }
}
