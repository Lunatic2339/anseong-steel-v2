using System;

namespace Anseong.Core
{
    /// <summary>
    /// 측정한 높이를 목표 높이에 맞추는 균일 스케일 계산.
    /// </summary>
    public static class HeightFit
    {
        /// <summary>measuredHeight를 targetHeight로 만드는 배율.</summary>
        public static float ScaleForHeight(float measuredHeight, float targetHeight)
        {
            if (!(measuredHeight > 0f) || float.IsInfinity(measuredHeight))
                throw new ArgumentOutOfRangeException(nameof(measuredHeight), measuredHeight, "측정 높이는 0보다 커야 합니다.");
            if (!(targetHeight > 0f) || float.IsInfinity(targetHeight))
                throw new ArgumentOutOfRangeException(nameof(targetHeight), targetHeight, "목표 높이는 0보다 커야 합니다.");
            return targetHeight / measuredHeight;
        }

        /// <summary>
        /// 배율을 적용한 뒤 바닥(minY)이 groundY에 닿도록 루트에 더할 Y 이동량.
        /// boundsMinY와 pivotY는 배율 적용 전 월드 좌표.
        /// </summary>
        public static float GroundOffset(float boundsMinY, float pivotY, float scale, float groundY = 0f)
        {
            var scaledMinY = pivotY + (boundsMinY - pivotY) * scale;
            return groundY - scaledMinY;
        }
    }
}
