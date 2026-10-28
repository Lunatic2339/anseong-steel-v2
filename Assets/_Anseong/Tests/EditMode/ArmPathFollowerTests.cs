using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class ArmPathFollowerTests
    {
        const float Length = 1f;
        const float Dt = 1f / 90f;

        [Test]
        public void StartsSlow_ThenAccelerates()
        {
            var s = new ArmPathSettings();
            var f = new ArmPathFollower();
            f.Reset(Vector3.zero);
            var target = new Vector3(0f, 0f, 0.8f);
            var first = f.Step(target, Length, Dt, s).z;
            float before = first, gainEarly = 0f, gainLate = 0f;
            for (var i = 1; i < 20; i++)
            {
                var z = f.Step(target, Length, Dt, s).z;
                if (i == 1) gainEarly = z - before;
                gainLate = z - before;
                before = z;
            }
            Assert.Less(first, 0.02f, "처음엔 느림");
            Assert.Greater(gainLate, gainEarly * 2f, "점점 빨라짐");
        }

        [Test]
        public void CatchesUp_ThenNextStartIsSlowAgain()
        {
            var s = new ArmPathSettings();
            var f = new ArmPathFollower();
            f.Reset(Vector3.zero);
            var target = new Vector3(0f, 0f, 0.5f);
            for (var i = 0; i < 200; i++) f.Step(target, Length, Dt, s);
            Assert.AreEqual(0.5f, f.Position.z, 1e-4f);
            Assert.AreEqual(0f, f.Speed);
            var z = f.Step(new Vector3(0f, 0f, 0.9f), Length, Dt, s).z;
            Assert.Less(z - 0.5f, 0.02f);
        }

        [Test]
        public void FollowsCurvedPath_NotStraightCut()
        {
            // 손이 옆으로 갔다가 앞으로 꺾음: 주먹은 그 모서리를 거쳐 간다.
            var s = new ArmPathSettings();
            var f = new ArmPathFollower();
            f.Reset(Vector3.zero);
            f.Step(new Vector3(0.5f, 0f, 0f), Length, Dt, s);
            var corner = false;
            for (var i = 0; i < 300; i++)
            {
                var p = f.Step(new Vector3(0.5f, 0f, 0.5f), Length, Dt, s);
                if (p.x > 0.49f && p.z < 0.05f) corner = true;
            }
            Assert.IsTrue(corner, "모서리(0.5, 0, 0)를 지나감");
            Assert.AreEqual(0.5f, f.Position.z, 1e-4f);
        }
    }
}
