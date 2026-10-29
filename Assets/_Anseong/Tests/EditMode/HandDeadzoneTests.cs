using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class HandDeadzoneTests
    {
        const float Eps = 1e-4f;

        static HandDeadzoneSettings Settings(bool enabled = true) =>
            new HandDeadzoneSettings { enabled = enabled, radiusMeters = 0.02f, angleDegrees = 3f };

        [Test]
        public void SmallJitter_IsIgnored()
        {
            var zone = new HandDeadzone();
            var s = Settings();
            zone.Filter(Vector3.zero, Quaternion.identity, s);
            var (p, r) = zone.Filter(new Vector3(0.015f, -0.01f, 0f), Quaternion.Euler(0f, 2f, 0f), s);
            Assert.AreEqual(0f, p.magnitude, Eps);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, r), 0.01f);
        }

        [Test]
        public void BigMove_FollowsAndStopsRadiusBehind()
        {
            var zone = new HandDeadzone();
            var s = Settings();
            zone.Filter(Vector3.zero, Quaternion.identity, s);
            var p = zone.Filter(new Vector3(0f, 0f, 0.5f), Quaternion.identity, s).position;
            Assert.AreEqual(0.48f, p.z, Eps);
            // 되돌아오는 작은 떨림은 다시 무시.
            var q = zone.Filter(new Vector3(0f, 0f, 0.49f), Quaternion.identity, s).position;
            Assert.AreEqual(0.48f, q.z, Eps);
        }

        [Test]
        public void BigTurn_FollowsMinusThreshold()
        {
            var zone = new HandDeadzone();
            var s = Settings();
            zone.Filter(Vector3.zero, Quaternion.identity, s);
            var r = zone.Filter(Vector3.zero, Quaternion.Euler(0f, 30f, 0f), s).rotation;
            Assert.AreEqual(27f, Quaternion.Angle(Quaternion.identity, r), 0.05f);
        }

        [Test]
        public void Disabled_PassesThrough()
        {
            var zone = new HandDeadzone();
            var s = Settings(false);
            zone.Filter(Vector3.zero, Quaternion.identity, s);
            var hand = new Vector3(0.01f, 0f, 0f);
            Assert.AreEqual(hand, zone.Filter(hand, Quaternion.identity, s).position);
        }
    }
}
