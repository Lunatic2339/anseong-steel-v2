using Anseong.Core;
using UnityEngine;

namespace Anseong.EditorTools
{
    /// <summary>
    /// 샌드박스 메뉴들이 같이 쓰는 로봇 배치 도우미.
    /// </summary>
    static class RobotSandboxUtility
    {
        public const string RobotModelPath = "Assets/_Anseong/Art/Robot/Source/BossUpdatedCompatible.fbx";
        public const float RobotHeight = 65f;

        /// <summary>
        /// 렌더러 bounds 높이를 재서 robot을 targetHeight로 균일 확대하고 발을 groundY에 맞춘다.
        /// boundsBefore는 확대 전 측정값.
        /// </summary>
        public static bool FitToHeight(GameObject robot, float targetHeight, out Bounds boundsBefore, out float scale, out float offset, float groundY = 0f)
        {
            scale = 1f;
            offset = 0f;
            if (!TryGetRendererBounds(robot, out boundsBefore))
                return false;

            scale = HeightFit.ScaleForHeight(boundsBefore.size.y, targetHeight);
            var pivotY = robot.transform.position.y;
            offset = HeightFit.GroundOffset(boundsBefore.min.y, pivotY, scale, groundY);
            robot.transform.localScale *= scale;
            robot.transform.position += Vector3.up * offset;
            return true;
        }

        public static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.size.y > 0f;
        }
    }
}
