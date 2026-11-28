using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class BossMotionTests
    {
        [Test]
        public void WithoutClipsItDoesNothingAndLeavesArmsToThePuppet()
        {
            var go = new GameObject("Boss motion test");
            try
            {
                var motion = go.AddComponent<BossMotion>();
                var settings = SkirmishSettings.BossAct1();
                var session = new SkirmishSession(settings);
                Assert.IsFalse(motion.Ready);
                Assert.DoesNotThrow(() => motion.Show(session.State, settings, 0.02f));
                Assert.IsFalse(motion.DrivesArms);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
