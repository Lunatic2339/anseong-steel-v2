using Anseong.Combat;
using Anseong.Pilot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.Flow
{
    /// <summary>
    /// 운영자 재시작(M1-07): 키보드 한 키로 보정·보스 HP·연계·부스트를 처음 상태로 돌린다. 이전 판 상태가 남지 않는다.
    /// 시연 운영자 화면(M3)이 생기기 전까지 쓰는 최소판.
    /// </summary>
    public class SessionRestart : MonoBehaviour
    {
        [SerializeField, Tooltip("재시작 키(운영자 PC 키보드).")]
        Key restartKey = Key.R;

        [SerializeField, Tooltip("보정 흐름. 비어 있으면 시작할 때 한 번 찾는다.")]
        PilotCalibrationFlow calibration;

        [SerializeField, Tooltip("전투 판정(보스 HP·연계·부스트). 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        public int RestartCount { get; private set; }

        void Start()
        {
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || restartKey == Key.None) return;
            var key = keyboard[restartKey];
            if (key != null && key.wasPressedThisFrame) Restart();
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
