using System.Linq;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>결과 화면에 쓰는 기록(기획안 "결과 화면"): 걸린 시간, P1이 막은 횟수와 받은 피해, P2가 떨어뜨린 드론과 넣은 피해.</summary>
    public class BossResultTests
    {
        const float Dt = 0.02f;

        static SkirmishSession Play(bool mech, bool gunner, float seconds, System.Func<SkirmishState, bool> until = null)
        {
            var settings = SkirmishSettings.BossAct1();
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            for (float t = 0f; t < seconds; t += Dt)
            {
                s.SetMech(mech ? SkirmishBots.Mech(s.State, settings) : default);
                s.SetGunner(gunner ? SkirmishBots.Gunner(s.State, settings) : default);
                s.Tick(Dt);
                if (until != null && until(s.State)) break;
            }
            return s;
        }

        [Test]
        public void 끝까지_하면_기록이_남는다()
        {
            var s = Play(true, true, 900f, st => st.phase != SkirmishPhase.Fighting);
            var st = s.State;
            Assert.AreEqual(SkirmishPhase.Cleared, st.phase);
            Assert.Greater(st.statSeconds, 10f);
            Assert.Greater(st.statBlocks, 0, "자동 조작은 막는다");
            Assert.LessOrEqual(st.statPerfect, st.statBlocks);
            Assert.Greater(st.statGunnerDamage, 0f, "P2가 틈에 쏜다");
        }

        [Test]
        public void 안_막으면_받은_피해가_쌓이고_막은_횟수는_0이다()
        {
            var s = Play(false, false, 20f);
            Assert.AreEqual(0, s.State.statBlocks);
            Assert.Greater(s.State.statTaken, 0f);
            Assert.AreEqual(0f, s.State.statGunnerDamage);
        }

        [Test]
        public void 걸린_시간은_느린_시간과_상관없이_실제_시간이다()
        {
            var settings = SkirmishSettings.BossAct1();
            settings.finaleAfterSeconds = 1f;
            settings.finaleAutoSeconds = 0f;
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            for (int i = 0; i < 500; i++) s.Tick(Dt);
            Assert.AreNotEqual(FinaleStep.None, s.State.finale, "마무리(느린 시간) 중");
            Assert.AreEqual(10f, s.State.statSeconds, 0.05f);
            Assert.Less(s.State.elapsed, 9f, "규칙의 시간은 느리게 갔다");
        }

        [Test]
        public void 기록은_두_PC에_같이_간다()
        {
            var s = Play(true, true, 60f);
            var copy = new SkirmishState();
            NetSkirmishState.From(s.State, Vector3.forward, Vector3.forward).CopyTo(copy);
            Assert.AreEqual(s.State.statBlocks, copy.statBlocks);
            Assert.AreEqual(s.State.statPerfect, copy.statPerfect);
            Assert.AreEqual(s.State.statDrones, copy.statDrones);
            Assert.AreEqual(s.State.statTaken, copy.statTaken);
            Assert.AreEqual(s.State.statGunnerDamage, copy.statGunnerDamage);
            Assert.AreEqual(s.State.statSeconds, copy.statSeconds);
        }
    }
}
