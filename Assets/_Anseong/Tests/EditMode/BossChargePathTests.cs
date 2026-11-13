using Anseong.Boss;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>돌진 경로: 물러나 자세 → 달려옴(가속) → 안전한 쪽 반대편으로 스침 → 미끄러져 멈춤.</summary>
    public class BossChargePathTests
    {
        const float Home = 30f, Telegraph = 2f;

        static BossChargeSettings Settings() => new BossChargeSettings
        {
            windUpMeters = 5f, runSeconds = 0.9f, closeDistance = 20f, sideOffset = 10f, skidMeters = 3f, skidSeconds = 0.5f,
        };

        [Test]
        public void WindUp_BacksOff_ThenRunsIn()
        {
            var s = Settings();
            var start = BossChargePath.At(s, Home, Home, Telegraph, 0f, -1);
            Assert.AreEqual(0f, start.x, 1e-4f);
            Assert.AreEqual(Home, start.y, 1e-4f, "시작은 지금 자리");
            var ready = BossChargePath.At(s, Home, Home, Telegraph, Telegraph - 0.9f, -1);
            Assert.AreEqual(Home + 5f, ready.y, 1e-3f, "달리기 직전: 뒤로 물러나 있다");
            Assert.AreEqual(0f, ready.x, 1e-4f);
            var impact = BossChargePath.At(s, Home, Home, Telegraph, Telegraph, -1);
            Assert.AreEqual(20f, impact.y, 1e-3f, "명중 순간: 조종사 코앞");
            Assert.Less(impact.y, Home, "실제로 다가온다(뒤로 가지 않는다)");
        }

        [Test]
        public void Run_Accelerates()
        {
            var s = Settings();
            var a = BossChargePath.At(s, Home, Home, Telegraph, 1.1f, 1).y;
            var b = BossChargePath.At(s, Home, Home, Telegraph, 1.55f, 1).y;
            var c = BossChargePath.At(s, Home, Home, Telegraph, 2.0f, 1).y;
            Assert.Greater(a, b);
            Assert.Greater(b, c);
            Assert.Greater(b - c, a - b, "뒤쪽 절반에 더 많이 온다(가속)");
        }

        [Test]
        public void PassesOnTheSideOppositeToTheSafeSide()
        {
            var s = Settings();
            Assert.AreEqual(10f, BossChargePath.At(s, Home, Home, Telegraph, Telegraph, -1).x, 1e-3f, "왼쪽이 안전 → 보스는 조종사 오른쪽(+)으로");
            Assert.AreEqual(-10f, BossChargePath.At(s, Home, Home, Telegraph, Telegraph, 1).x, 1e-3f, "오른쪽이 안전 → 보스는 왼쪽(-)으로");
        }

        [Test]
        public void Skid_KeepsGoingAndStops()
        {
            var s = Settings();
            var impact = BossChargePath.At(s, Home, Home, Telegraph, Telegraph, -1);
            var mid = BossChargePath.At(s, Home, Home, Telegraph, Telegraph + 0.25f, -1);
            var end = BossChargePath.At(s, Home, Home, Telegraph, Telegraph + 0.5f, -1);
            var later = BossChargePath.At(s, Home, Home, Telegraph, Telegraph + 3f, -1);
            Assert.AreEqual(3f, Vector2.Distance(impact, end), 1e-3f, "미끄러진 거리");
            Assert.Greater(Vector2.Distance(impact, mid), 1.5f, "처음에 많이, 끝에 조금(감속)");
            Assert.Less(end.y, impact.y, "달리던 방향으로");
            Assert.AreEqual(end.x, later.x, 1e-4f);
            Assert.AreEqual(end.y, later.y, 1e-4f, "멈춘 뒤엔 그대로");
            Assert.AreEqual(Telegraph + 0.5f, BossChargePath.EndSeconds(s, Telegraph), 1e-5f);
        }

        [Test]
        public void FarStart_RunsFromWhereItIs_ShortTelegraphStillFits()
        {
            var s = Settings();
            // 밀려나 있던 보스(45 m): 더 물러나지 않고 그 자리에서 달려온다.
            Assert.AreEqual(45f, BossChargePath.At(s, 45f, Home, Telegraph, 1.0f, -1).y, 1e-3f);
            Assert.AreEqual(20f, BossChargePath.At(s, 45f, Home, Telegraph, Telegraph, -1).y, 1e-3f);
            // 연속기로 줄어든 예고(1초): 달리는 시간은 예고의 60%까지.
            Assert.AreEqual(0.6f, BossChargePath.RunSeconds(s, 1f), 1e-5f);
            Assert.AreEqual(20f, BossChargePath.At(s, Home, Home, 1f, 1f, -1).y, 1e-3f);
        }
    }
}
