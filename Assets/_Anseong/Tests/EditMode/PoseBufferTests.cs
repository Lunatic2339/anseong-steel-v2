using Anseong.Net;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class PoseBufferTests
    {
        static PilotPose Pose(double time, float x)
        {
            return new PilotPose
            {
                Time = time,
                Upper = Quaternion.Euler(0f, x * 90f, 0f),
                Lower = Quaternion.identity,
                Hand = Quaternion.identity,
                HeadPosition = new Vector3(x, 0f, 0f),
                HeadRotation = Quaternion.identity,
                LeftHandRotation = Quaternion.identity,
                RightHandRotation = Quaternion.identity,
            };
        }

        [Test]
        public void Empty_ReturnsFalse()
        {
            Assert.IsFalse(new PoseBuffer().TrySample(1.0, 0.5, out _));
        }

        [Test]
        public void Interpolates_BetweenSamples()
        {
            var b = new PoseBuffer();
            b.Add(Pose(1.0, 0f));
            b.Add(Pose(1.1, 1f));
            Assert.IsTrue(b.TrySample(1.05, 0.5, out var p));
            Assert.AreEqual(0.5f, p.HeadPosition.x, 1e-4f);
            Assert.AreEqual(45f, p.Upper.eulerAngles.y, 0.1f);
        }

        [Test]
        public void AfterLast_HoldsLast_NoExtrapolation()
        {
            var b = new PoseBuffer();
            b.Add(Pose(1.0, 0f));
            b.Add(Pose(1.1, 1f));
            Assert.IsTrue(b.TrySample(1.3, 0.5, out var p));
            Assert.AreEqual(1f, p.HeadPosition.x, 1e-4f);
        }

        [Test]
        public void BeforeFirst_UsesFirst()
        {
            var b = new PoseBuffer();
            b.Add(Pose(1.0, 2f));
            b.Add(Pose(1.1, 3f));
            Assert.IsTrue(b.TrySample(0.5, 0.5, out var p));
            Assert.AreEqual(2f, p.HeadPosition.x, 1e-4f);
        }

        [Test]
        public void Stale_ReturnsFalse()
        {
            var b = new PoseBuffer();
            b.Add(Pose(1.0, 0f));
            Assert.IsTrue(b.TrySample(1.4, 0.5, out _));
            Assert.IsFalse(b.TrySample(1.6, 0.5, out _), "0.5초 넘게 새 자세 없음 = 끊김");
        }

        [Test]
        public void LateOrDuplicate_IsDropped()
        {
            var b = new PoseBuffer();
            Assert.IsTrue(b.Add(Pose(1.0, 0f)));
            Assert.IsTrue(b.Add(Pose(1.2, 1f)));
            Assert.IsFalse(b.Add(Pose(1.1, 5f)), "순서가 바뀌어 늦게 온 것");
            Assert.IsFalse(b.Add(Pose(1.2, 5f)), "같은 시각");
            Assert.AreEqual(2, b.Count);
            Assert.IsTrue(b.TrySample(1.1, 0.5, out var p));
            Assert.AreEqual(0.5f, p.HeadPosition.x, 1e-4f);
        }

        [Test]
        public void Capacity_DropsOldest()
        {
            var b = new PoseBuffer(4);
            for (var i = 0; i < 10; i++) b.Add(Pose(i * 0.1, i));
            Assert.AreEqual(4, b.Count);
            Assert.IsTrue(b.TrySample(0.0, 5.0, out var p));
            Assert.AreEqual(6f, p.HeadPosition.x, 1e-4f, "가장 오래 남은 것 = 6번째");
        }

        [Test]
        public void TrimBefore_KeepsOneEarlierForInterpolation()
        {
            var b = new PoseBuffer();
            for (var i = 0; i < 5; i++) b.Add(Pose(1.0 + i * 0.1, i));
            b.TrimBefore(1.25);
            Assert.AreEqual(3, b.Count, "1.2·1.3·1.4가 남는다");
            Assert.IsTrue(b.TrySample(1.25, 0.5, out var p));
            Assert.AreEqual(2.5f, p.HeadPosition.x, 1e-4f);
        }
    }
}
