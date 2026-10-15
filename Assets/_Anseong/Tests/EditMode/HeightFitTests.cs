using System;
using Anseong.Core;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class HeightFitTests
    {
        [Test]
        public void Scale_MakesMeasuredHeightTarget()
        {
            var scale = HeightFit.ScaleForHeight(1.95f, 65f);
            Assert.AreEqual(65f, 1.95f * scale, 1e-4f);
        }

        [Test]
        public void Scale_SameHeight_IsOne()
        {
            Assert.AreEqual(1f, HeightFit.ScaleForHeight(65f, 65f), 1e-6f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Scale_InvalidMeasuredHeight_Throws(float measured)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightFit.ScaleForHeight(measured, 65f));
        }

        [TestCase(0f)]
        [TestCase(-65f)]
        public void Scale_InvalidTargetHeight_Throws(float target)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightFit.ScaleForHeight(2f, target));
        }

        [Test]
        public void GroundOffset_PivotAtFeet_IsZero()
        {
            Assert.AreEqual(0f, HeightFit.GroundOffset(0f, 0f, 30f), 1e-6f);
        }

        [Test]
        public void GroundOffset_FeetBelowPivot_LiftsByScaledDistance()
        {
            // 피벗 아래 0.1m에 발 → 30배 후 -3m → 3m 올린다.
            Assert.AreEqual(3f, HeightFit.GroundOffset(-0.1f, 0f, 30f), 1e-5f);
        }

        [Test]
        public void GroundOffset_RespectsGroundY()
        {
            Assert.AreEqual(-2f, HeightFit.GroundOffset(1f, 1f, 10f, -1f), 1e-6f);
        }
    }
}
