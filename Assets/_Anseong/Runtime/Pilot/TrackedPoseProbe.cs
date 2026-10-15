using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Anseong.Pilot
{
    /// <summary>
    /// 컨트롤러 하나의 위치·회전을 Input System 액션으로 읽어 자기 Transform(로컬)에 적용한다.
    /// 개발 빌드에서는 추적이 끊기면 자식 렌더러 색을 바꾼다.
    /// </summary>
    public class TrackedPoseProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("컨트롤러 위치(Vector3) 액션.")]
        InputActionReference positionAction;

        [SerializeField, Tooltip("컨트롤러 회전(Quaternion) 액션.")]
        InputActionReference rotationAction;

        [SerializeField, Tooltip("컨트롤러 trackingState(Integer) 액션. 비워 두면 항상 추적 중으로 본다.")]
        InputActionReference trackingStateAction;

        // 직렬화 레이아웃을 빌드 종류와 무관하게 맞추려고 색 필드는 #if 밖에 둔다. 사용은 개발 빌드에서만.
#pragma warning disable 0414
        [SerializeField, Tooltip("추적 중일 때 자식 렌더러 색(에디터·개발 빌드 전용).")]
        Color trackedColor = new Color(0.2f, 0.9f, 0.3f);

        [SerializeField, Tooltip("추적이 끊겼을 때 자식 렌더러 색(에디터·개발 빌드 전용).")]
        Color lostColor = new Color(0.95f, 0.2f, 0.2f);
#pragma warning restore 0414

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] debugRenderers;
        MaterialPropertyBlock debugBlock;
        bool? shownTracked;
#endif

        bool enabledPosition;
        bool enabledRotation;
        bool enabledTrackingState;

        /// <summary>위치와 회전이 둘 다 추적 중이면 true.</summary>
        public bool IsTracked { get; private set; }

        public InputActionReference PositionAction => positionAction;

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugRenderers = GetComponentsInChildren<Renderer>(true);
            debugBlock = new MaterialPropertyBlock();
#endif
        }

        void OnEnable()
        {
            enabledPosition = EnableIfNeeded(positionAction);
            enabledRotation = EnableIfNeeded(rotationAction);
            enabledTrackingState = EnableIfNeeded(trackingStateAction);
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            if (enabledPosition) positionAction.action.Disable();
            if (enabledRotation) rotationAction.action.Disable();
            if (enabledTrackingState) trackingStateAction.action.Disable();
            enabledPosition = enabledRotation = enabledTrackingState = false;
        }

        void Update()
        {
            ApplyPose();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shownTracked != IsTracked)
            {
                shownTracked = IsTracked;
                debugBlock.SetColor(BaseColorId, IsTracked ? trackedColor : lostColor);
                foreach (var r in debugRenderers)
                    r.SetPropertyBlock(debugBlock);
            }
#endif
        }

        // 렌더 직전에 한 번 더 갱신해 손 표시 지연을 줄인다.
        void OnBeforeRender()
        {
            ApplyPose();
        }

        void ApplyPose()
        {
            var state = ReadTrackingState();
            IsTracked = TrackingStateRules.IsPoseTracked((int)state);

            if ((state & InputTrackingState.Position) != 0 && positionAction != null)
                transform.localPosition = positionAction.action.ReadValue<Vector3>();
            if ((state & InputTrackingState.Rotation) != 0 && rotationAction != null)
                transform.localRotation = rotationAction.action.ReadValue<Quaternion>();
        }

        InputTrackingState ReadTrackingState()
        {
            if (trackingStateAction == null)
                return InputTrackingState.Position | InputTrackingState.Rotation;
            return (InputTrackingState)trackingStateAction.action.ReadValue<int>();
        }

        static bool EnableIfNeeded(InputActionReference reference)
        {
            if (reference == null || reference.action == null || reference.action.enabled)
                return false;
            reference.action.Enable();
            return true;
        }
    }
}
