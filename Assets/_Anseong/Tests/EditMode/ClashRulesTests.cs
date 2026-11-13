using Anseong.Flow;
using NUnit.Framework;

namespace Anseong.Tests.EditMode
{
    public class ClashRulesTests
    {
        static ClashRules Started(out double now)
        {
            var r = new ClashRules();
            r.Begin(0);
            Assert.AreEqual(ClashStep.None, r.Update(1, true, true));
            Assert.AreEqual(ClashStep.Started, r.Update(r.Settings.delaySeconds, true, true));
            now = r.Settings.delaySeconds;
            return r;
        }

        static ClashStep Run(ClashRules r, ref double now, double seconds, bool left, bool right)
        {
            var end = now + seconds;
            while (now < end)
            {
                now += 0.05;
                var step = r.Update(now, left, right);
                if (step == ClashStep.Won || step == ClashStep.Lost) return step;
            }
            return ClashStep.None;
        }

        [Test]
        public void BothBracing_Wins()
        {
            var r = Started(out var now);
            Assert.AreEqual(ClashStep.Won, Run(r, ref now, 6, true, true));
            Assert.IsTrue(r.Done);
            Assert.IsTrue(r.Won);
        }

        [Test]
        public void OneBracing_Loses()
        {
            var r = Started(out var now);
            Assert.AreEqual(ClashStep.Lost, Run(r, ref now, 6, true, false));
            Assert.IsFalse(r.Won);
        }

        [Test]
        public void NobodyBracing_LosesFast()
        {
            var r = Started(out var now);
            Assert.AreEqual(ClashStep.Lost, Run(r, ref now, 1.5, false, false));
        }

        [Test]
        public void Timeout_DecidedByGauge()
        {
            var r = new ClashRules(new ClashSettings { riseBoth = 0.01f, maxSeconds = 3f });
            r.Begin(0);
            var now = (double)r.Settings.delaySeconds;
            r.Update(now, true, true);
            Assert.AreEqual(ClashStep.Lost, Run(r, ref now, 4, true, true)); // 시간 끝에 게이지가 절반 미만(0.43)이면 진다
        }

        [Test]
        public void EmitsTicksWhileHolding()
        {
            var r = Started(out var now);
            var ticks = 0;
            for (var i = 0; i < 30; i++)
            {
                now += 0.05;
                if (r.Update(now, true, true) == ClashStep.Tick) ticks++;
            }
            Assert.GreaterOrEqual(ticks, 2);
        }

        [Test]
        public void AfterDone_NoMoreSteps()
        {
            var r = Started(out var now);
            Run(r, ref now, 6, true, true);
            Assert.AreEqual(ClashStep.None, r.Update(now + 1, true, true));
        }
    }
}
