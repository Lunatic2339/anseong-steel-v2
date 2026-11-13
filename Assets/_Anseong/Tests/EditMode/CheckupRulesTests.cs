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

        static ShieldDrillSettings DrillSettings() => new ShieldDrillSettings
        {
            blocksNeeded = 2, maxThrows = 4, firstDelay = 1f, aimSeconds = 1.5f, flightSeconds = 2f, restSeconds = 1f, maxSameSide = 2,
        };

        [Test]
        public void DrillImpactTimeIsFixedWhenAimStarts()
        {
            var drill = new ShieldDrill(DrillSettings());
            drill.Begin(10d);
            Assert.AreEqual(DrillStep.None, drill.Update(10.5d, 0.1f));
            Assert.AreEqual(DrillStep.Aim, drill.Update(11d, 0.1f));
            Assert.AreEqual(PilotSide.Left, drill.Side);
            Assert.AreEqual(12.5d, drill.ReleaseAt, 1e-9);
            Assert.AreEqual(14.5d, drill.ImpactAt, 1e-9);
            // 닿기 전에는 아무 일도 없고, 닿는 시각에 판정을 요구한다.
            Assert.AreEqual(DrillStep.None, drill.Update(14.4d, 0.9f));
            Assert.AreEqual(DrillStep.Impact, drill.Update(14.5d, 0.9f));
        }

        [Test]
        public void DrillEndsAfterEnoughBlocksAndARest()
        {
            var drill = new ShieldDrill(DrillSettings());
            drill.Begin(0d);
            Assert.AreEqual(DrillStep.Aim, drill.Update(1d, 0.1f));
            Assert.AreEqual(DrillStep.Impact, drill.Update(4.5d, 0.1f));
            Assert.IsFalse(drill.Report(true, 4.5d));
            Assert.AreEqual(DrillStep.None, drill.Update(5d, 0.9f));
            Assert.AreEqual(DrillStep.Aim, drill.Update(5.5d, 0.9f));
            Assert.AreEqual(PilotSide.Right, drill.Side);
            Assert.AreEqual(DrillStep.Impact, drill.Update(9d, 0.9f));
            Assert.IsTrue(drill.Report(true, 9d));
            Assert.AreEqual(2, drill.Blocks);
            // 쉼이 지난 뒤 한 번만 끝을 알린다.
            Assert.AreEqual(DrillStep.None, drill.Update(9.5d, 0.5f));
            Assert.AreEqual(DrillStep.Done, drill.Update(10d, 0.5f));
            Assert.AreEqual(DrillStep.None, drill.Update(11d, 0.5f));
        }

        [Test]
        public void DrillGivesUpAfterMaxThrowsSoNobodyGetsStuck()
        {
            var drill = new ShieldDrill(DrillSettings());
            drill.Begin(0d);
            var now = 0d;
            var finished = false;
            for (var i = 0; i < 4; i++)
            {
                while (drill.Update(now, 0.1f) != DrillStep.Aim) now += 0.25d;
                while (drill.Update(now, 0.1f) != DrillStep.Impact) now += 0.25d;
                finished = drill.Report(false, now);
            }
            Assert.IsTrue(finished);
            Assert.AreEqual(0, drill.Blocks);
            Assert.AreEqual(4, drill.Throws);
        }

        [Test]
        public void DrillNeverThrowsSameSideMoreThanAllowed()
        {
            var drill = new ShieldDrill(new ShieldDrillSettings { blocksNeeded = 99, maxThrows = 0, firstDelay = 0f, aimSeconds = 1.5f, flightSeconds = 1f, restSeconds = 0.5f, maxSameSide = 2 });
            drill.Begin(0d);
            var now = 0d;
            var run = 0;
            PilotSide? last = null;
            for (var i = 0; i < 12; i++)
            {
                // 난수가 늘 왼쪽이어도 세 번째는 오른쪽으로 바뀐다.
                while (drill.Update(now, 0.1f) != DrillStep.Aim) now += 0.25d;
                run = last.HasValue && last.Value == drill.Side ? run + 1 : 1;
                last = drill.Side;
                Assert.LessOrEqual(run, 2);
                while (drill.Update(now, 0.1f) != DrillStep.Impact) now += 0.25d;
                drill.Report(false, now);
            }
        }
    }
}
