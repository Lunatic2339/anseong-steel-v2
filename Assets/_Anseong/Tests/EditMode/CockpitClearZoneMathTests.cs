using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class CockpitClearZoneMathTests
    {
        static readonly Vector3 BoxCenter = new Vector3(0f, 1.5f, 0f);
        static readonly Vector3 BoxSize = new Vector3(4f, 3f, 4f);

        [Test]
        public void InsideBox_Overlaps()
        {
            Assert.IsTrue(CockpitClearZoneMath.Overlaps(new Vector3(10f, 51f, 0f), Vector3.one * 0.1f,
                new Vector3(10f, 50f, 0f), Quaternion.identity, BoxCenter, BoxSize));
        }

        [Test]
        public void FarAway_DoesNotOverlap()
        {
            Assert.IsFalse(CockpitClearZoneMath.Overlaps(new Vector3(0f, 40f, 0f), Vector3.one,
                new Vector3(0f, 50f, 0f), Quaternion.identity, BoxCenter, BoxSize));
        }

        [Test]
        public void TouchingEdgeWithExtents_Overlaps()
        {
            // 상자 오른쪽 끝 x=2, 렌더러 중심 x=3, 반크기 1.5 → 겹침.
            Assert.IsTrue(CockpitClearZoneMath.Overlaps(new Vector3(3f, 1.5f, 0f), new Vector3(1.5f, 0.1f, 0.1f),
                Vector3.zero, Quaternion.identity, BoxCenter, BoxSize));
            Assert.IsFalse(CockpitClearZoneMath.Overlaps(new Vector3(3f, 1.5f, 0f), new Vector3(0.9f, 0.1f, 0.1f),
                Vector3.zero, Quaternion.identity, BoxCenter, BoxSize));
        }

        [Test]
        public void CockpitYawed_UsesCockpitAxes()
        {
            // 조종석이 90도 돌아 앞(+Z 로컬)이 월드 +X. 길쭉한 상자(앞으로 10 m).
            var longBox = new Vector3(2f, 3f, 20f);
            var yaw = Quaternion.Euler(0f, 90f, 0f);
            Assert.IsTrue(CockpitClearZoneMath.Overlaps(new Vector3(8f, 1.5f, 0f), Vector3.one * 0.1f, Vector3.zero, yaw, BoxCenter, longBox));
            Assert.IsFalse(CockpitClearZoneMath.Overlaps(new Vector3(0f, 1.5f, 8f), Vector3.one * 0.1f, Vector3.zero, yaw, BoxCenter, longBox));
        }
    }
}
