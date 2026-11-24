using Anseong.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class CombatEffectSetTests
    {
        [Test]
        public void Entry_MatchesCueCodeAndKind()
        {
            var e = new CombatEffectSet.Entry { cue = CombatCue.Hit, kind = CombatCueSet.KindFilter.Hook };
            Assert.IsTrue(e.Matches(new CombatEvent { Cue = CombatCue.Hit, Kind = PunchKind.Hook }));
            Assert.IsFalse(e.Matches(new CombatEvent { Cue = CombatCue.Hit, Kind = PunchKind.Jab }));
            Assert.IsFalse(e.Matches(new CombatEvent { Cue = CombatCue.Miss, Kind = PunchKind.Hook }));
            var map = new CombatEffectSet.Entry { cue = CombatCue.MapEvent, matchCode = true, code = 1 };
            Assert.IsTrue(map.Matches(new CombatEvent { Cue = CombatCue.MapEvent, Code = 1 }));
            Assert.IsFalse(map.Matches(new CombatEvent { Cue = CombatCue.MapEvent, Code = 2 }));
        }

        [Test]
        public void MissingDefaults_OnlyAddsAbsentRows()
        {
            var set = ScriptableObject.CreateInstance<CombatEffectSet>();
            Assert.AreEqual(CombatEffectSet.DefaultEntries().Length, set.MissingDefaults().Length);
            set.SetEntries(CombatEffectSet.DefaultEntries());
            Assert.AreEqual(0, set.MissingDefaults().Length);
            Object.DestroyImmediate(set);
        }

        [Test]
        public void CueSet_HasDefaultsForScenarioCues()
        {
            var cues = new[] { CombatCue.DroneFire, CombatCue.DroneDown, CombatCue.CannonFire, CombatCue.HackStart, CombatCue.MapEvent };
            var defaults = CombatCueSet.DefaultEntries();
            foreach (var cue in cues)
                Assert.IsTrue(System.Array.Exists(defaults, d => d.cue == cue), cue.ToString());
        }
    }
}
