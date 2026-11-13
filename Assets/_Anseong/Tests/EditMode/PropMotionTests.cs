using Anseong.Flow;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>맵 소품 움직임 수학: 공 포물선, 코일 낙하(맞는 시각·땅에 닿는 자리), 가장 가까운 구조물, 크레인 주행.</summary>
    public class PropMotionTests
    {
        [Test]
        public void ArcStartsAndEndsOnThePointsAndPeaksInTheMiddle()
        {
            var from = new Vector3(0f, 44f, 56f);
            var to = new Vector3(-6f, 50f, 13f);
            Assert.Less(Vector3.Distance(from, PropMotion.Arc(from, to, 7f, 0f)), 1e-4f);
            Assert.Less(Vector3.Distance(to, PropMotion.Arc(from, to, 7f, 1f)), 1e-4f);
            Assert.AreEqual(47f + 7f, PropMotion.Arc(from, to, 7f, 0.5f).y, 1e-3f);
            // 범위를 넘으면 끝점에 머문다(결과 사건이 늦게 와도 공이 방패 자리를 지나치지 않는다).
            Assert.Less(Vector3.Distance(to, PropMotion.Arc(from, to, 7f, 1.4f)), 1e-4f);
        }

        [Test]
        public void HeavyAndSettleEndAtOne()
        {
            Assert.AreEqual(0f, PropMotion.Heavy(0f), 1e-6f);
            Assert.AreEqual(1f, PropMotion.Heavy(1f), 1e-6f);
            Assert.AreEqual(1f, PropMotion.Heavy(3f), 1e-6f);
            Assert.Less(PropMotion.Heavy(0.25f), 0.25f);   // 천천히 출발
            Assert.AreEqual(0f, PropMotion.Settle(0f), 1e-6f);
            Assert.AreEqual(1f, PropMotion.Settle(1f), 1e-6f);
            Assert.AreEqual(1f, PropMotion.Settle(2f), 1e-6f);
        }

        [Test]
        public void NearestUsesGroundDistance()
        {
            var points = new[] { new Vector3(0f, 0f, 10f), new Vector3(0f, 500f, 52f), new Vector3(0f, 0f, 90f) };
            Assert.AreEqual(1, PropMotion.Nearest(points, new Vector3(3f, 0f, 50f)));
            Assert.AreEqual(2, PropMotion.Nearest(points, new Vector3(0f, 0f, 200f)));
            Assert.AreEqual(-1, PropMotion.Nearest(new Vector3[0], Vector3.zero));
            Assert.AreEqual(-1, PropMotion.Nearest(null, Vector3.zero));
        }

        [Test]
        public void TravelStaysInsideTheRails()
        {
            Assert.AreEqual(105f, PropMotion.Travel(100f, 300f, -20f, 262f, 10f, 0.5f), 1e-4f);
            Assert.AreEqual(262f, PropMotion.Travel(260f, 300f, -20f, 262f, 10f, 0.5f), 1e-4f);
            Assert.AreEqual(-20f, PropMotion.Travel(-19f, -80f, -20f, 262f, 10f, 0.5f), 1e-4f);
        }

        static CoilDrop Drop() => new CoilDrop
        {
            From = new Vector3(4f, 88f, 60f), Impact = new Vector3(0f, 69f, 62f), RollDirection = Vector3.right,
            Gravity = 16f, BounceSpeed = 14f, BounceUp = 9f, GroundY = 7f, RollMeters = 34f, RollSeconds = 4f,
        };

        [Test]
        public void CoilHitsTheBossExactlyAtImpactTime()
        {
            var drop = Drop();
            Assert.AreEqual(Mathf.Sqrt(2f * 19f / 16f), drop.ImpactTime, 1e-4f);
            Assert.Less(Vector3.Distance(drop.From, drop.Position(0f)), 1e-4f);
            Assert.Less(Vector3.Distance(drop.Impact, drop.Position(drop.ImpactTime)), 1e-2f);
            // 떨어지는 동안 높이는 계속 내려간다.
            var previous = drop.From.y;
            for (var t = 0.1f; t < drop.ImpactTime; t += 0.1f)
            {
                var y = drop.Position(t).y;
                Assert.Less(y, previous);
                previous = y;
            }
        }

        [Test]
        public void CoilBouncesAsideLandsOnTheGroundAndRollsToAStop()
        {
            var drop = Drop();
            Assert.Greater(drop.LandTime, drop.ImpactTime);
            var mid = drop.Position((drop.ImpactTime + drop.LandTime) * 0.5f);
            Assert.Greater(mid.x, drop.Impact.x);            // 옆으로 튕김
            var land = drop.Position(drop.LandTime);
            Assert.AreEqual(7f, land.y, 1e-2f);               // 땅에 닿음(통과하지 않음)
            Assert.Less(Vector3.Distance(drop.LandPoint, land), 1e-2f);
            var end = drop.Position(drop.EndTime);
            Assert.AreEqual(7f, end.y, 1e-3f);
            Assert.AreEqual(drop.LandPoint.x + 34f, end.x, 1e-2f);
            Assert.Less(Vector3.Distance(end, drop.Position(drop.EndTime + 10f)), 1e-4f);   // 멈춘 뒤 그대로
            Assert.AreEqual(0f, drop.Rolled(drop.LandTime), 1e-4f);
            Assert.AreEqual(34f, drop.Rolled(drop.EndTime), 1e-3f);
        }
    }
}
