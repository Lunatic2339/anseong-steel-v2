using Anseong.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>M4-04: 스캐너·포 조준 각도로 드론 고르기, 달려드는 드론 위치.</summary>
    public class DroneRulesTests
    {
        [Test]
        public void PicksClosestAngleWithinLimit()
        {
            var targets = new[] { new Vector3(0, 0, 100), new Vector3(10, 0, 100), new Vector3(0, 100, 0) };
            Assert.AreEqual(0, DroneRules.Pick(Vector3.zero, Vector3.forward, targets, null, 10f));
            Assert.AreEqual(1, DroneRules.Pick(Vector3.zero, new Vector3(0.1f, 0, 1), targets, null, 10f));
            Assert.AreEqual(-1, DroneRules.Pick(Vector3.zero, Vector3.right, targets, null, 10f));
        }

        [Test]
        public void SkipsIneligible()
        {
            var targets = new[] { new Vector3(0, 0, 100), new Vector3(5, 0, 100) };
            Assert.AreEqual(1, DroneRules.Pick(Vector3.zero, Vector3.forward, targets, new[] { false, true }, 10f));
        }

        [Test]
        public void RushMovesToTarget()
        {
            Assert.AreEqual(new Vector3(5, 0, 0), DroneRules.RushPosition(Vector3.zero, new Vector3(10, 0, 0), 0.5f));
            Assert.AreEqual(new Vector3(10, 0, 0), DroneRules.RushPosition(Vector3.zero, new Vector3(10, 0, 0), 2f));
        }
    }
}
