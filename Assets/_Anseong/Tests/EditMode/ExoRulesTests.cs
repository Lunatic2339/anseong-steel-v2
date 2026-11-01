using Anseong.Combat;
using Anseong.Haptics;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class ExoRulesTests
    {
        static CombatEvent Event(CombatCue cue, PilotSide side) => new CombatEvent { Cue = cue, Side = side };

        [Test]
        public void Protocol_FormatsCommands()
        {
            Assert.AreEqual("P,400", ExoProtocol.Prepare(400));
            Assert.AreEqual("R,140,120", ExoProtocol.Resist(140, 120));
            Assert.AreEqual("X", ExoProtocol.Release);
            Assert.AreEqual("H", ExoProtocol.Heartbeat);
        }

        [Test]
        public void Protocol_ClampsStrengthAndDuration()
        {
            Assert.AreEqual("R,255,500", ExoProtocol.Resist(999, 2000));
            Assert.AreEqual("R,0,1", ExoProtocol.Resist(-5, 0));
            Assert.AreEqual("P,3000", ExoProtocol.Prepare(10000));
        }

        [Test]
        public void Protocol_ParsesStatus()
        {
            Assert.IsTrue(ExoProtocol.TryParseStatus("S,RESIST,180,0\r", out var s));
            Assert.AreEqual("RESIST", s.State);
            Assert.AreEqual(180, s.Output);
            Assert.AreEqual(0, s.Error);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("S,IDLE,0")]
        [TestCase("S,IDLE,a,0")]
        [TestCase("Q,IDLE,0,0")]
        [TestCase("S,,0,0")]
        public void Protocol_RejectsBadStatus(string line)
        {
            Assert.IsFalse(ExoProtocol.TryParseStatus(line, out _));
        }

        [Test]
        public void Hit_OnOwnArm_Resists()
        {
            var s = new ExoCueSettings { hitStrength = 150, hitMs = 120 };
            Assert.AreEqual("R,150,120", ExoCueRules.CommandFor(Event(CombatCue.Hit, PilotSide.Left), PilotSide.Left, s));
        }

        [Test]
        public void OtherArmEvents_AreIgnored()
        {
            var s = new ExoCueSettings();
            foreach (var cue in new[] { CombatCue.Hit, CombatCue.StrikeLaunch, CombatCue.Miss, CombatCue.Boost })
                Assert.IsNull(ExoCueRules.CommandFor(Event(cue, PilotSide.Right), PilotSide.Left, s), cue.ToString());
        }

        [Test]
        public void StrikeLaunch_Prepares_OnlyWhenOn()
        {
            var s = new ExoCueSettings { strikePrepareMs = 300 };
            Assert.AreEqual("P,300", ExoCueRules.CommandFor(Event(CombatCue.StrikeLaunch, PilotSide.Right), PilotSide.Right, s));
            s.prepareOnStrike = false;
            Assert.IsNull(ExoCueRules.CommandFor(Event(CombatCue.StrikeLaunch, PilotSide.Right), PilotSide.Right, s));
        }

        [Test]
        public void MissAndBoost_Release()
        {
            var s = new ExoCueSettings();
            Assert.AreEqual("X", ExoCueRules.CommandFor(Event(CombatCue.Miss, PilotSide.Left), PilotSide.Left, s));
            Assert.AreEqual("X", ExoCueRules.CommandFor(Event(CombatCue.Boost, PilotSide.Left), PilotSide.Left, s));
            s.releaseOnMiss = false;
            Assert.IsNull(ExoCueRules.CommandFor(Event(CombatCue.Miss, PilotSide.Left), PilotSide.Left, s));
        }

        [Test]
        public void BossDefeated_ReleasesBothArms()
        {
            var s = new ExoCueSettings();
            Assert.AreEqual("X", ExoCueRules.CommandFor(Event(CombatCue.BossDefeated, PilotSide.Left), PilotSide.Right, s));
            Assert.AreEqual("X", ExoCueRules.CommandFor(Event(CombatCue.BossDefeated, PilotSide.Left), PilotSide.Left, s));
        }

        [Test]
        public void NonExoCues_SendNothing()
        {
            var s = new ExoCueSettings();
            foreach (var cue in new[] { CombatCue.GripLock, CombatCue.GripRelease, CombatCue.ComboStep, CombatCue.ComboBroken, CombatCue.BoostReady, CombatCue.BoostMissed })
                Assert.IsNull(ExoCueRules.CommandFor(Event(cue, PilotSide.Left), PilotSide.Left, s), cue.ToString());
        }

        [Test]
        public void PortFromCommandLine_ReadsValueAfterKey()
        {
            var args = new[] { "game.exe", "-exoRight", "COM4", "-exoLeft", "COM3" };
            Assert.AreEqual("COM3", ExoArmLink.PortFromCommandLine(args, "-exoLeft"));
            Assert.AreEqual("COM4", ExoArmLink.PortFromCommandLine(args, "-EXORIGHT"));
            Assert.IsNull(ExoArmLink.PortFromCommandLine(new[] { "game.exe", "-exoLeft" }, "-exoLeft"));
            Assert.IsNull(ExoArmLink.PortFromCommandLine(null, "-exoLeft"));
        }
    }
}
