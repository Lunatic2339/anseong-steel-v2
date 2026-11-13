using Anseong.Combat;
using NUnit.Framework;

namespace Anseong.Tests.EditMode
{
    public class GrabSlashTests
    {
        [Test]
        public void HoldingLongEnoughGrabs_ShortReachDoesNot()
        {
            var rules = new GrabSlashRules();
            Assert.AreEqual(GrabStep.None, rules.Update(0.0, true, true));
            Assert.AreEqual(GrabStep.None, rules.Update(0.2, true, false), "잽처럼 바로 거두면 안 잡힌다");
            Assert.AreEqual(GrabStep.None, rules.Update(0.3, true, true));
            Assert.AreEqual(GrabStep.Started, rules.Update(0.7, true, true));
            Assert.IsTrue(rules.Held);
        }

        [Test]
        public void NotGrabbable_NeverGrabs()
        {
            var rules = new GrabSlashRules();
            rules.Update(0.0, false, true);
            Assert.AreEqual(GrabStep.None, rules.Update(2.0, false, true));
            Assert.IsFalse(rules.Held);
        }

        [Test]
        public void SlashWhileHeld_EndsGrabAndStartsCooldown()
        {
            var rules = new GrabSlashRules();
            rules.Update(0.0, true, true);
            rules.Update(0.5, true, true);
            Assert.IsTrue(rules.TrySlash(1.0));
            Assert.IsFalse(rules.Held);
            Assert.IsFalse(rules.TrySlash(1.1), "한 번 잡으면 한 번만 벤다");
            rules.Update(2.0, true, true);
            Assert.AreEqual(GrabStep.None, rules.Update(3.0, true, true), "쉬는 시간 안에는 다시 못 잡는다");
            var after = 1.0 + rules.Settings.cooldownSeconds;
            rules.Update(after, true, true);
            Assert.AreEqual(GrabStep.Started, rules.Update(after + 0.5, true, true));
        }

        [Test]
        public void ReleaseOrTimeout_BreaksGrab()
        {
            var rules = new GrabSlashRules();
            rules.Update(0.0, true, true);
            rules.Update(0.5, true, true);
            Assert.AreEqual(GrabStep.Broken, rules.Update(1.0, true, false));

            rules = new GrabSlashRules();
            rules.Update(0.0, true, true);
            rules.Update(0.5, true, true);
            Assert.AreEqual(GrabStep.None, rules.Update(1.0, true, true));
            Assert.AreEqual(GrabStep.TimedOut, rules.Update(0.5 + rules.Settings.maxHoldSeconds, true, true));
        }

        [Test]
        public void SlashWithoutGrab_IsNothing()
        {
            Assert.IsFalse(new GrabSlashRules().TrySlash(1.0));
        }
    }
}
