using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class BodyFollowTests
    {
        const float Dt = 1f / 90f;
        static readonly Vector3 Head = new Vector3(0.2f, 1.6f, -0.3f);

        static void Step(BodyFollow follow, Vector3 head, float seconds, BodyFollowSettings settings)
        {
            var n = Mathf.RoundToInt(seconds / Dt);
            for (var i = 0; i < n; i++) follow.Update(head, Dt, settings);
        }

        [Test]
        public void SideStep_IsFollowed_AfterAFewSeconds()
        {
            var settings = new BodyFollowSettings();
            var follow = new BodyFollow();
            follow.SetReference(Head);
            var step = new Vector3(0.5f, 0f, 0.2f);
            Step(follow, Head + step, 3f, settings);
            Assert.Less(Vector3.Distance(follow.Offset, step), 0.01f, "걸어 옮긴 만큼 따라간다");
        }

        [Test]
        public void QuickLean_IsMostlyKept()
        {
            // 펀치 중 0.1초 몸을 숙이는 것은 거의 지우지 않는다(손이 앞으로 간 것으로 남는다).
            var settings = new BodyFollowSettings();
            var follow = new BodyFollow();
            follow.SetReference(Head);
            var lean = new Vector3(0f, 0f, 0.1f);
            Step(follow, Head + lean, 0.1f, settings);
            Assert.Less(follow.Offset.magnitude, lean.magnitude * 0.2f);
        }

        [Test]
        public void Disabled_OrNoReference_GivesZero()
        {
            var follow = new BodyFollow();
            Step(follow, Head + Vector3.right, 1f, new BodyFollowSettings());
            Assert.AreEqual(Vector3.zero, follow.Offset, "기준 없음");

            follow.SetReference(Head);
            Step(follow, Head + Vector3.right, 1f, new BodyFollowSettings { enabled = false });
            Assert.AreEqual(Vector3.zero, follow.Offset, "끄면 이전 동작");

            Step(follow, Head + Vector3.right, 1f, new BodyFollowSettings());
            follow.Clear();
            Assert.AreEqual(Vector3.zero, follow.Offset, "다시 보정하면 지운다");
            Assert.IsFalse(follow.HasReference);
        }

        [Test]
        public void HeightAndLimit_Settings()
        {
            var follow = new BodyFollow();
            follow.SetReference(Head);
            Step(follow, Head + new Vector3(0f, -0.4f, 0f), 3f, new BodyFollowSettings { followHeight = false });
            Assert.AreEqual(0f, follow.Offset.y, 1e-4f, "높이를 따르지 않음");

            follow.SetReference(Head);
            Step(follow, Head + new Vector3(10f, 0f, 0f), 5f, new BodyFollowSettings { followSeconds = 0f });
            Assert.AreEqual(2f, follow.Offset.magnitude, 1e-3f, "최대 거리에서 멈춤");
        }

        [Test]
        public void NaNHead_KeepsLastOffset()
        {
            var settings = new BodyFollowSettings();
            var follow = new BodyFollow();
            follow.SetReference(Head);
            Step(follow, Head + Vector3.forward * 0.3f, 2f, settings);
            var before = follow.Offset;
            follow.Update(new Vector3(float.NaN, 0f, 0f), Dt, settings);
            Assert.AreEqual(before, follow.Offset);
        }
    }
}
