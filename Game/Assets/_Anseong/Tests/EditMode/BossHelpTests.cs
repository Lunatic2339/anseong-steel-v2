using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>말이 안 통할 때(기획안 6절): 같은 특별한 공격을 연속으로 놓친 횟수를 세고, 한 번 맞히면 0으로 돌린다. 화면은 2 이상이면 답을 말해 준다.</summary>
    public class BossHelpTests
    {
        const float Dt = 0.02f;

        static SkirmishSession Only(GruntAttack attack)
        {
            var settings = SkirmishSettings.BossAct1();
            settings.grunts[0].spec.attacks = new[] { attack };
            settings.droneEvery = 0f;
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            return s;
        }

        /// <summary>P1이 가만히 있거나(still) 답을 아는 자동 조작으로(막기만, 치지는 않음) seconds 동안 돌린다.</summary>
        static void Run(SkirmishSession s, float seconds, bool still, System.Func<SkirmishState, bool> until = null)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                var mech = still ? default : SkirmishBots.Mech(s.State, s.Settings);
                mech.punchDamage = 0f;
                s.SetMech(mech);
                s.Tick(Dt);
                if (until != null && until(s.State)) return;
            }
        }

        [Test]
        public void 속임_베기를_연속으로_놓치면_세고_막으면_0으로_돌아간다()
        {
            var s = Only(GruntAttack.Feint);
            Run(s, 60f, true, st => st.missFeint >= 2);
            Assert.AreEqual(2, s.State.missFeint);
            Run(s, 30f, false, st => st.missFeint == 0);
            Assert.AreEqual(0, s.State.missFeint);
        }

        [Test]
        public void 큰_내려베기를_연속으로_놓치면_세고_막으면_0으로_돌아간다()
        {
            var s = Only(GruntAttack.BigSlam);
            Run(s, 60f, true, st => st.missBig >= 2 || st.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(2, s.State.missBig);
            Run(s, 30f, false, st => st.missBig == 0);
            Assert.AreEqual(0, s.State.missBig);
        }

        [Test]
        public void 돌진을_연속으로_맞으면_세고_비키면_0으로_돌아간다()
        {
            var s = Only(GruntAttack.Thrust);
            Run(s, 60f, true, st => st.missThrust >= 2 || st.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(2, s.State.missThrust);
            Run(s, 30f, false, st => st.missThrust == 0);
            Assert.AreEqual(0, s.State.missThrust);
        }

        [Test]
        public void 기본_공격은_세지_않는다()
        {
            var s = Only(GruntAttack.Slam);
            Run(s, 12f, true);
            Assert.AreEqual(0, s.State.missFeint + s.State.missBig + s.State.missThrust);
        }

        [Test]
        public void 닫힌_보스를_쏘면_포가_튕긴_것으로_적힌다()
        {
            var s = Only(GruntAttack.Slam);
            Run(s, 1f, false);
            Assert.IsFalse(s.State.cannonBounced);
            var aim = (SkirmishSession.GruntCenter(s.State, s.Settings) - s.Settings.gunnerPost).normalized;
            float hp = s.State.gruntHp;
            for (int i = 0; i < 30; i++)
            {
                s.SetGunner(new GunnerInput { fire = true, aim = aim, pointer = aim });
                s.Tick(Dt);
            }
            Assert.IsTrue(s.State.cannonBounced);
            Assert.AreEqual(hp, s.State.gruntHp, "닫힌 동안에는 안 깎인다");
            var copy = new SkirmishState();
            NetSkirmishState.From(s.State, Vector3.forward, Vector3.forward).CopyTo(copy);
            Assert.IsTrue(copy.cannonBounced);
        }
    }
}
