using UnityEngine.XR;

namespace Anseong.Pilot
{
    /// <summary>
    /// XR 장치의 trackingState 값(InputTrackingState 플래그)을 해석하는 규칙.
    /// </summary>
    public static class TrackingStateRules
    {
        const InputTrackingState PoseFlags = InputTrackingState.Position | InputTrackingState.Rotation;

        /// <summary>위치와 회전이 둘 다 추적 중이면 true.</summary>
        public static bool IsPoseTracked(int trackingState)
        {
            return ((InputTrackingState)trackingState & PoseFlags) == PoseFlags;
        }
    }
}
