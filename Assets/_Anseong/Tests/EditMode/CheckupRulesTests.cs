using Anseong.Flow;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M4-05: 관절 시험 좌우 번갈아 세기, 점검 단계별 무장.</summary>
    public class CheckupRulesTests
    {
        [Test]
        public void AlternationRestartsOnSameSide()
        {
            var c = new AlternationCounter();
            Assert.AreEqual(1, c.Strike(PilotSide.Left));
            Assert.AreEqual(2, c.Strike(PilotSide.Right));
            Assert.AreEqual(1, c.Strike(PilotSide.Right));
            Assert.AreEqual(2, c.Strike(PilotSide.Left));
            Assert.AreEqual(3, c.Strike(PilotSide.Right));
            Assert.IsTrue(c.Done(3));
        }

        [Test]
        public void CheckupStepsEquipWeapons()
        {
            Assert.AreEqual(Loadout.Bare, WeaponRules.For(MatchScene.Checkup, false, MatchScene.Waiting, (int)CheckupStep.JointTest));
            Assert.AreEqual(Loadout.Melee, WeaponRules.For(MatchScene.Checkup, false, MatchScene.Waiting, (int)CheckupStep.ShieldDrill));
            Assert.AreEqual(Loadout.Ranged, WeaponRules.For(MatchScene.Checkup, false, MatchScene.Waiting, (int)CheckupStep.Sortie));
        }
    }
}
