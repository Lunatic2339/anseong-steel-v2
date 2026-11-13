using Anseong.Flow;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class MatchFlowTests
    {
        static MatchFlowRules NewRules(out MatchFlowSettings s)
        {
            s = new MatchFlowSettings
            {
                entranceSeconds = 10f, droneHuntPlaceholderSeconds = 8f, lossSeconds = 8f, rebootSeconds = 4f, resultSeconds = 15f, rebootsAllowed = 1,
            };
            var rules = new MatchFlowRules(s);
            rules.Reset(0);
            return rules;
        }

        [Test]
        public void FullRunVisitsEveryScene()
        {
            var r = NewRules(out _);
            Assert.AreEqual(MatchScene.Waiting, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.Start, 1));
            Assert.AreEqual(MatchScene.Checkup, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.CheckupDone, 2));
            Assert.AreEqual(MatchScene.Entrance, r.Scene);
            Assert.IsFalse(r.Update(11.9));
            Assert.IsTrue(r.Update(12));
            Assert.AreEqual(MatchScene.DroneHunt, r.Scene);
            Assert.IsTrue(r.Update(20));
            Assert.AreEqual(MatchScene.Approach, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.ReachedMelee, 25));
            Assert.AreEqual(MatchScene.FightCannon, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.BossWeaponBroken, 60));
            Assert.AreEqual(MatchScene.LostGun, r.Scene);
            Assert.IsTrue(r.Update(68));
            Assert.AreEqual(MatchScene.FightArmor, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.BossWeaponBroken, 100));
            Assert.AreEqual(MatchScene.Clash, r.Scene);
            Assert.IsFalse(r.Signal(MatchSignal.PilotDefeated, 100)); // 힘겨루기 중엔 안 쓰러진다
            Assert.IsFalse(r.Update(100));
            Assert.IsTrue(r.Signal(MatchSignal.ClashDone, 100));
            Assert.AreEqual(MatchScene.LostSword, r.Scene);
            Assert.IsTrue(r.Update(108));
            Assert.AreEqual(MatchScene.FightCore, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.BossKneel, 150));
            Assert.AreEqual(MatchScene.Finisher, r.Scene);
            Assert.IsTrue(r.Signal(MatchSignal.BossDefeated, 160));
            Assert.AreEqual(MatchScene.Result, r.Scene);
            Assert.IsTrue(r.Won);
        }

        [Test]
        public void SignalsOutOfOrderAreIgnored()
        {
            var r = NewRules(out _);
            Assert.IsFalse(r.Signal(MatchSignal.CheckupDone, 1));
            Assert.IsFalse(r.Signal(MatchSignal.BossWeaponBroken, 1));
            Assert.IsFalse(r.Signal(MatchSignal.PilotDefeated, 1));
            r.Signal(MatchSignal.Start, 1);
            // 점검 중 이동은 출격이지만 보스 무기 신호는 무시.
            Assert.IsFalse(r.Signal(MatchSignal.BossKneel, 2));
            Assert.AreEqual(MatchScene.Checkup, r.Scene);
        }

        [Test]
        public void FirstDefeatRebootsIntoSameSegment_SecondEndsInResult()
        {
            var r = NewRules(out _);
            r.Signal(MatchSignal.Skip, 0); // 점검
            r.Signal(MatchSignal.Skip, 0); // 등장
            r.Signal(MatchSignal.Skip, 0); // 드론
            r.Signal(MatchSignal.Skip, 0); // 접근
            r.Signal(MatchSignal.Skip, 0); // 팔뚝 포
            r.Signal(MatchSignal.BossWeaponBroken, 1);
            r.Update(9);
            Assert.AreEqual(MatchScene.FightArmor, r.Scene);

            Assert.IsTrue(r.Signal(MatchSignal.PilotDefeated, 20));
            Assert.AreEqual(MatchScene.Rebooting, r.Scene);
            Assert.AreEqual(MatchScene.FightArmor, r.ResumeScene);
            Assert.IsFalse(r.Update(23.9));
            Assert.IsTrue(r.Update(24));
            Assert.AreEqual(MatchScene.FightArmor, r.Scene);
            Assert.AreEqual(MatchScene.Rebooting, r.Previous);
            Assert.AreEqual(24, r.SceneStart, 1e-9);

            Assert.IsTrue(r.Signal(MatchSignal.PilotDefeated, 30));
            Assert.AreEqual(MatchScene.Result, r.Scene);
            Assert.IsFalse(r.Won);
            Assert.AreEqual(1, r.RebootsUsed);
        }

        [Test]
        public void SkipDoesNotPassFightScenes()
        {
            var r = NewRules(out _);
            for (var i = 0; i < 5; i++) r.Signal(MatchSignal.Skip, 0);
            Assert.AreEqual(MatchScene.FightCannon, r.Scene);
            Assert.IsFalse(r.Signal(MatchSignal.Skip, 1));
            Assert.AreEqual(MatchScene.FightCannon, r.Scene);
        }

        [Test]
        public void ResultStaysUntilResetAndResetClearsEverything()
        {
            var r = NewRules(out _);
            for (var i = 0; i < 5; i++) r.Signal(MatchSignal.Skip, 0);
            r.Signal(MatchSignal.PilotDefeated, 1);
            r.Update(5);
            r.Signal(MatchSignal.BossDefeated, 10);
            Assert.AreEqual(MatchScene.Result, r.Scene);
            Assert.IsFalse(r.Update(100));
            Assert.AreEqual(MatchScene.Result, r.Scene);
            Assert.IsFalse(r.ResultExpired(24.9));
            Assert.IsTrue(r.ResultExpired(25));

            r.Reset(200);
            Assert.AreEqual(MatchScene.Waiting, r.Scene);
            Assert.AreEqual(0, r.RebootsUsed);
            Assert.IsFalse(r.Won);
            Assert.AreEqual(200, r.SceneStart, 1e-9);
        }

        [Test]
        public void DronePlaceholderOffWaitsForSignal()
        {
            var r = NewRules(out var s);
            s.droneHuntPlaceholderSeconds = 0f;
            for (var i = 0; i < 3; i++) r.Signal(MatchSignal.Skip, 0);
            Assert.AreEqual(MatchScene.DroneHunt, r.Scene);
            Assert.IsFalse(r.Update(1000));
            Assert.IsTrue(r.Signal(MatchSignal.DronesCleared, 1001));
            Assert.AreEqual(MatchScene.Approach, r.Scene);
        }

        [Test]
        public void BossHoldsFollowScene()
        {
            Assert.IsFalse(MatchFlowRules.BossMayMove(MatchScene.Checkup));
            Assert.IsFalse(MatchFlowRules.BossMayAttack(MatchScene.Approach));
            Assert.IsTrue(MatchFlowRules.BossMayMove(MatchScene.Approach));
            Assert.IsTrue(MatchFlowRules.BossMayAttack(MatchScene.FightCore));
            Assert.IsFalse(MatchFlowRules.BossMayAttack(MatchScene.Finisher));
            Assert.IsFalse(MatchFlowRules.BossMayAttack(MatchScene.Rebooting));
        }

        [Test]
        public void StatsCountBlocksCombosAndTime()
        {
            var stats = new MatchStats();
            Assert.AreEqual(-1, stats.Finish(0, false, 0).BlockPercent);
            stats.Begin(10);
            stats.Begin(50); // 재기동해도 시작 시각은 그대로
            stats.Blocked();
            stats.Blocked();
            stats.Blocked();
            stats.Hit();
            stats.Combo(2);
            stats.Combo(3);
            stats.Combo(1);
            stats.DroneDown();
            var result = stats.Finish(130, true, 1);
            Assert.IsTrue(result.Won);
            Assert.AreEqual(120f, result.Seconds, 1e-4f);
            Assert.AreEqual(75, result.BlockPercent);
            Assert.AreEqual(3, result.BestCombo);
            Assert.AreEqual(1, result.Drones);
            Assert.AreEqual(1, result.Reboots);
            stats.Reset();
            Assert.IsFalse(stats.Running);
        }

        [Test]
        public void MatchLogRow_FormatsOneLine()
        {
            var r = new MatchResult { Won = true, Seconds = 412.34f, BlockPercent = -1, Drones = 5, BestCombo = 3, Reboots = 1 };
            var row = MatchLogRow.Format(r, new System.DateTime(2026, 11, 27, 14, 5, 9));
            Assert.AreEqual("2026-11-27 14:05:09,1,412.3,,5,3,1", row);
            Assert.AreEqual(MatchLogRow.Header.Split(',').Length, row.Split(',').Length);
        }

        [Test]
        public void OpeningAndEndingVideosWrapTheMatchWhenSet()
        {
            var r = NewRules(out var s);
            s.openingSeconds = 20f;
            s.endingSeconds = 30f;
            Assert.IsTrue(r.Signal(MatchSignal.Start, 0));
            Assert.AreEqual(MatchScene.Opening, r.Scene);
            Assert.IsFalse(r.Signal(MatchSignal.PilotDefeated, 1));
            Assert.IsFalse(r.Update(19.9));
            Assert.IsTrue(r.Update(20));
            Assert.AreEqual(MatchScene.Checkup, r.Scene);
            r.Signal(MatchSignal.CheckupDone, 21);
            r.Update(31); r.Update(39);
            r.Signal(MatchSignal.ReachedMelee, 40);
            r.Signal(MatchSignal.BossWeaponBroken, 50); r.Update(58);
            r.Signal(MatchSignal.BossWeaponBroken, 70); r.Update(78);
            r.Signal(MatchSignal.BossKneel, 90);
            Assert.IsTrue(r.Signal(MatchSignal.BossDefeated, 95));
            Assert.AreEqual(MatchScene.Ending, r.Scene);
            Assert.IsTrue(r.Won);
            Assert.IsFalse(r.Signal(MatchSignal.BossDefeated, 96));
            Assert.IsTrue(r.Update(125));
            Assert.AreEqual(MatchScene.Result, r.Scene);
        }

        [Test]
        public void NoVideosMeansNoVideoScenesAndSkipJumpsVideos()
        {
            var r = NewRules(out var s);
            r.Signal(MatchSignal.Start, 0);
            Assert.AreEqual(MatchScene.Checkup, r.Scene);
            r.Reset(1);
            s.openingSeconds = 20f;
            r.Signal(MatchSignal.Start, 2);
            Assert.IsTrue(r.Signal(MatchSignal.Skip, 3));
            Assert.AreEqual(MatchScene.Checkup, r.Scene);
        }
    }
}
