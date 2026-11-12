using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.UI
{
    /// <summary>
    /// 헤드셋 없이 PC에서 로봇 조종석 눈높이로 맵을 둘러보는 개발용 카메라(선우 10/28, 윤주 맵 확인용).
    /// 마우스 = 둘러보기(왼쪽 클릭으로 커서 잠금, Esc로 풀기), WASD = 이동, Q/E = 내려가기/올라가기, Shift = 빠르게, R = 처음 자리.
    /// 'Anseong → Sandbox → Add PC Robot Eye View' 메뉴가 열린 씬에 붙인다. 게임 조종석(XR Origin)과는 무관하다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PcEyeViewCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("이동 속도(m/s). 65m 로봇 기준으로 넉넉하게.")]
        float moveSpeed = 15f;

        [SerializeField, Min(1f), Tooltip("Shift를 누르는 동안 이동 속도 배수.")]
        float fastMultiplier = 4f;

        [SerializeField, Tooltip("마우스 감도(도 / 마우스 이동 1픽셀).")]
        float lookSensitivity = 0.12f;

        [SerializeField, Range(0f, 89f), Tooltip("위아래로 볼 수 있는 최대 각도(도).")]
        float maxPitch = 85f;

        [SerializeField, Tooltip("켜면 Q/E로 높이를 바꿀 수 있다. 끄면 시작 높이(로봇 눈높이)에 고정.")]
        bool allowHeightChange = true;

        [SerializeField, Range(45f, 100f), Tooltip("PC 맵 확인용 세로 시야각. VR 카메라에는 적용하지 않는다.")]
        float previewFieldOfView = 80f;

        [SerializeField, Min(1000f), Tooltip("PC 미리보기에서 표시할 최대 거리(m). 원경 지형까지 포함한다.")]
        float previewFarClip = 40000f;

        void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var previewCamera = GetComponent<Camera>();
            previewCamera.fieldOfView = previewFieldOfView;
            previewCamera.farClipPlane = previewFarClip;
#endif
        }
        float yaw, pitch;
        Vector3 startPosition;
        Quaternion startRotation;

        void Start()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            ReadAngles();
        }

        void ReadAngles()
        {
            var euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            if (keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            if (keyboard.rKey.wasPressedThisFrame)
            {
                transform.SetPositionAndRotation(startPosition, startRotation);
                ReadAngles();
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = mouse.delta.ReadValue() * lookSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // 앞뒤·좌우는 바닥과 평행하게 움직인다(위를 보고 W를 눌러도 높이는 그대로).
            var flatForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var flatRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += flatForward;
            if (keyboard.sKey.isPressed) move -= flatForward;
            if (keyboard.dKey.isPressed) move += flatRight;
            if (keyboard.aKey.isPressed) move -= flatRight;
            if (allowHeightChange)
            {
                if (keyboard.eKey.isPressed) move += Vector3.up;
                if (keyboard.qKey.isPressed) move -= Vector3.up;
            }
            if (move.sqrMagnitude > 1f) move.Normalize();
            var speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
            transform.position += move * (speed * Time.deltaTime);
#endif
        }

        void OnDisable()
        {
            SetCursorLocked(false);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
