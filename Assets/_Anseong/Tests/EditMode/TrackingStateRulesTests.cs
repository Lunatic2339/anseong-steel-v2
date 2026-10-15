using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine.XR;

namespace Anseong.Tests
{
    public class TrackingStateRulesTests
    {
        [Test]
        public void None_IsNotTracked()
        {
            Assert.IsFalse(TrackingStateRules.IsPoseTracked((int)InputTrackingState.None));
        }

        [Test]
        public void PositionOnly_IsNotTracked()
        {
            Assert.IsFalse(TrackingStateRules.IsPoseTracked((int)InputTrackingState.Position));
        }

        [Test]
        public void RotationOnly_IsNotTracked()
        {
            Assert.IsFalse(TrackingStateRules.IsPoseTracked((int)InputTrackingState.Rotation));
        }

        [Test]
        public void PositionAndRotation_IsTracked()
        {
            var state = InputTrackingState.Position | InputTrackingState.Rotation;
            Assert.IsTrue(TrackingStateRules.IsPoseTracked((int)state));
        }

        [Test]
        public void AllFlags_IsTracked()
        {
            Assert.IsTrue(TrackingStateRules.IsPoseTracked((int)InputTrackingState.All));
        }
    }
}
