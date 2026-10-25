using System.Collections.Generic;
using System.Linq;
using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class CombatRulesTests
    {
        const PilotSide L = PilotSide.Left;
        const PilotSide R = PilotSide.Right;

        // ── 연계 ──

        [Test]
        public void JabJabHook_FinishesCombo_EitherStartingArm()
        {
            foreach (var a in new[] { R, L })
            {
                var b = a == R ? L : R;
                var combo = new ComboRules();
                Assert.AreEqual(1, combo.OnHit(a, PunchKind.Jab, 0.0).Step);
                Assert.AreEqual(2, combo.OnHit(b, PunchKind.Jab, 0.5).Step);
                var done = combo.OnHit(a, PunchKind.Hook, 1.0);
                Assert.IsTrue(done.Finished, "잽(A)–잽(B)–훅(A)");
                Assert.AreEqual(3, done.Step);
                Assert.AreEqual(0, combo.Step, "완성 뒤 처음부터");
            }
        }

        [Test]
        public void WrongTurn_EndsCombo_AndJabStartsNewOne()
        {
            var combo = new ComboRules();
            combo.OnHit(R, PunchKind.Jab, 0.0);
            var again = combo.OnHit(R, PunchKind.Jab, 0.5);
            Assert.AreEqual(ComboBreak.WrongTurn, again.Broken, "같은 팔 잽 두 번은 차례 위반");
            Assert.AreEqual(1, again.Step, "새 연계의 첫 잽");

            combo.OnHit(L, PunchKind.Jab, 1.0);
            var hookWrongArm = combo.OnHit(L, PunchKind.Hook, 1.5);
            Assert.IsFalse(hookWrongArm.Finished, "훅은 첫 팔(오른)이 쳐야 함");
            Assert.AreEqual(ComboBreak.WrongTurn, hookWrongArm.Broken);
            Assert.AreEqual(0, combo.Step);
        }

        [Test]
        public void Timeout_EndsCombo_After24Seconds()
        {
            var combo = new ComboRules();
            combo.OnHit(R, PunchKind.Jab, 0.0);
            Assert.AreEqual(ComboBreak.None, combo.Tick(2.3));
            Assert.AreEqual(ComboBreak.Timeout, combo.Tick(2.5));
            var late = combo.OnHit(L, PunchKind.Jab, 2.6);
            Assert.AreEqual(1, late.Step, "시간이 지나면 다음 잽은 새 연계");
        }

        [Test]
        public void SimultaneousJabs_DoNotAdvanceTwice()
        {
            var combo = new ComboRules();
            combo.OnHit(R, PunchKind.Jab, 0.0);
            var tooSoon = combo.OnHit(L, PunchKind.Jab, 0.05);
            Assert.AreEqual(1, tooSoon.Step, "0.15초 안의 두 번째 잽은 다음 단계가 아님");
        }

        [Test]
        public void Uppercut_KeepsCombo_ByDefault_ToggleBreaks()
        {
            var combo = new ComboRules();
            combo.OnHit(R, PunchKind.Jab, 0.0);
            var up = combo.OnHit(L, PunchKind.Uppercut, 0.4);
            Assert.AreEqual(ComboBreak.None, up.Broken);
            Assert.AreEqual(1, combo.Step);

            var strict = new ComboRules(new ComboSettings { uppercutKeepsCombo = false });
            strict.OnHit(R, PunchKind.Jab, 0.0);
            Assert.AreEqual(ComboBreak.WrongTurn, strict.OnHit(L, PunchKind.Uppercut, 0.4).Broken);
        }

        [Test]
        public void Miss_AndDamage_EndCombo()
        {
            var combo = new ComboRules();
            Assert.AreEqual(ComboBreak.None, combo.OnMiss(0.0), "연계 없을 때 헛침은 아무 일 없음");
            combo.OnHit(R, PunchKind.Jab, 0.1);
            Assert.AreEqual(ComboBreak.Miss, combo.OnMiss(0.5));
            combo.OnHit(R, PunchKind.Jab, 1.0);
            Assert.AreEqual(ComboBreak.Damaged, combo.OnDamaged(1.2));
        }

        // ── 부스트 ──

        [Test]
        public void Boost_TriggerWithinWindow_WhileGripHeld()
        {
            var boost = new BoostWindow();
            boost.Open(R, 1.0);
            Assert.AreEqual(BoostWindow.Result.None, boost.Update(L, true, true, 1.1), "다른 팔 트리거는 무시");
            Assert.AreEqual(BoostWindow.Result.None, boost.Update(R, true, false, 1.2));
            Assert.AreEqual(BoostWindow.Result.Boosted, boost.Update(R, true, true, 1.4));
            Assert.IsFalse(boost.IsOpen);
        }

        [Test]
        public void Boost_LateOrGripReleased_Misses()
        {
            var boost = new BoostWindow();
            boost.Open(R, 1.0);
            Assert.AreEqual(BoostWindow.Result.Missed, boost.Update(R, true, true, 1.5), "0.45초 지남");

            boost.Open(R, 2.0);
            Assert.AreEqual(BoostWindow.Result.Missed, boost.Update(R, false, true, 2.1), "그립 놓으면 취소");

            boost.Open(R, 3.0);
            Assert.AreEqual(BoostWindow.Result.Missed, boost.Tick(3.5), "입력 없이도 시간이 지나면 닫힘");
        }

        // ── 보스 HP ──

        [Test]
        public void BossHealth_DamageTable_AndStopsAtZero()
        {
            var boss = new BossHealth();
            Assert.AreEqual(3f, boss.Apply(boss.DamageOf(PunchKind.Jab)), 1e-4f);
            Assert.AreEqual(5f, boss.Apply(boss.DamageOf(PunchKind.Hook)), 1e-4f);
            Assert.AreEqual(4f, boss.Apply(boss.DamageOf(PunchKind.Uppercut)), 1e-4f);
            Assert.AreEqual(0f, boss.DamageOf(PunchKind.None));
            Assert.AreEqual(88f, boss.Current, 1e-4f);

            boss.Apply(200f);
            Assert.IsTrue(boss.IsDefeated);
            Assert.AreEqual(0f, boss.Current);
            Assert.AreEqual(0f, boss.Apply(3f), "0이면 더 깎이지 않음");
        }

        // ── 판정 묶음 ──

        static HitReport Report(PilotSide side, PunchKind kind, int strike) =>
            new HitReport { Side = side, Kind = kind, StrikeId = strike, Quality = HitQuality.Assisted };

        [Test]
        public void Resolver_SameStrikeHitsOnce()
        {
            var resolver = new CombatResolver();
            var events = new List<CombatEvent>();
            Assert.IsTrue(resolver.Resolve(Report(R, PunchKind.Jab, 1), 0.0, events));
            Assert.IsFalse(resolver.Resolve(Report(R, PunchKind.Jab, 1), 0.05, events), "같은 동작 두 번째 접촉");
            Assert.IsFalse(resolver.Resolve(Report(R, PunchKind.None, 2), 0.1, events), "종류 없음");
            Assert.IsTrue(resolver.Resolve(Report(L, PunchKind.Jab, 1), 0.5, events), "다른 팔은 번호가 따로");
            Assert.AreEqual(94f, resolver.Boss.Current, 1e-4f);
            Assert.AreEqual(2, events.Count(e => e.Cue == CombatCue.Hit));
        }

        [Test]
        public void Resolver_FullCombo_ThenBoost_Events()
        {
            var resolver = new CombatResolver();
            var events = new List<CombatEvent>();
            resolver.Resolve(Report(R, PunchKind.Jab, 1), 0.0, events);
            resolver.Resolve(Report(L, PunchKind.Jab, 1), 0.5, events);
            resolver.Resolve(Report(R, PunchKind.Hook, 2), 1.0, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.BoostReady && e.Side == R));
            Assert.AreEqual("1,2,3", string.Join(",", events.Where(e => e.Cue == CombatCue.ComboStep).Select(e => e.ComboStep)));

            events.Clear();
            resolver.UpdateInput(R, true, true, 1.3, events);
            var boost = events.Single(e => e.Cue == CombatCue.Boost);
            Assert.AreEqual(6f, boost.Damage, 1e-4f);
            Assert.AreEqual(100f - 3f - 3f - 5f - 6f, resolver.Boss.Current, 1e-4f);
        }

        [Test]
        public void Resolver_StandaloneHook_DamagesWithoutBoost()
        {
            var resolver = new CombatResolver();
            var events = new List<CombatEvent>();
            resolver.Resolve(Report(R, PunchKind.Hook, 1), 0.0, events);
            Assert.AreEqual(95f, resolver.Boss.Current, 1e-4f);
            Assert.IsFalse(events.Any(e => e.Cue == CombatCue.BoostReady), "단독 훅에는 부스트 없음");
            Assert.IsFalse(resolver.Boost.IsOpen);
        }

        [Test]
        public void Resolver_Miss_BreaksCombo_AndTimeoutEvent()
        {
            var resolver = new CombatResolver();
            var events = new List<CombatEvent>();
            resolver.Resolve(Report(R, PunchKind.Jab, 1), 0.0, events);
            resolver.ReportMiss(L, PunchKind.Jab, 1, 0.4, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.Miss));
            Assert.AreEqual(ComboBreak.Miss, events.Single(e => e.Cue == CombatCue.ComboBroken).Break);

            events.Clear();
            resolver.Resolve(Report(R, PunchKind.Jab, 2), 1.0, events);
            resolver.Tick(3.5, events);
            Assert.AreEqual(ComboBreak.Timeout, events.Single(e => e.Cue == CombatCue.ComboBroken).Break);
        }

        [Test]
        public void Resolver_MissAfterHitOfSameStrike_IsIgnored()
        {
            var resolver = new CombatResolver();
            var events = new List<CombatEvent>();
            resolver.Resolve(Report(R, PunchKind.Jab, 1), 0.0, events);
            events.Clear();
            resolver.ReportMiss(R, PunchKind.Jab, 1, 0.3, events);
            Assert.AreEqual(0, events.Count, "맞힌 동작은 끝나도 헛침이 아님");
        }

        [Test]
        public void Resolver_DefeatedBoss_IgnoresHits()
        {
            var resolver = new CombatResolver(bossSettings: new BossHealthSettings { jabDamage = 60f });
            var events = new List<CombatEvent>();
            resolver.Resolve(Report(R, PunchKind.Jab, 1), 0.0, events);
            resolver.Resolve(Report(L, PunchKind.Jab, 1), 0.5, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.BossDefeated));
            Assert.IsFalse(resolver.Resolve(Report(R, PunchKind.Jab, 2), 1.0, events));
        }

        // ── 공격 보정 ──

        [Test]
        public void Assist_InRange_ByDistanceAndAngle()
        {
            var settings = new AttackAssistSettings();
            var shoulder = new Vector3(0f, 50f, 0f);
            var body = Quaternion.Euler(0f, 90f, 0f); // 로봇이 +X를 본다
            const float arm = 19f;
            Assert.IsTrue(AttackAssist.InRange(shoulder, body, shoulder + new Vector3(25f, -5f, 3f), arm, settings), "정면 25m");
            Assert.IsFalse(AttackAssist.InRange(shoulder, body, shoulder + new Vector3(30f, 0f, 0f), arm, settings), "사거리 밖(19×1.4=26.6m)");
            Assert.IsFalse(AttackAssist.InRange(shoulder, body, shoulder + new Vector3(5f, 0f, 15f), arm, settings), "옆 70°");
            Assert.IsFalse(AttackAssist.InRange(shoulder, body, shoulder + new Vector3(20f, 0f, 0f), arm, new AttackAssistSettings { enabled = false }), "끄면 보정 없음");
        }

        [Test]
        public void Assist_PullsOverTime_ThenReleases()
        {
            var settings = new AttackAssistSettings();
            var assist = new AttackAssist(settings);
            var hand = Vector3.zero;
            var zone = new Vector3(0f, 0f, 10f);
            assist.Begin(zone, Vector3.zero);
            assist.Track(new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 0.5f));
            assist.Update(settings.pullSeconds * 0.5f);
            var half = assist.Apply(hand);
            Assert.Greater(half.z, 0.5f);
            Assert.Less(half.z, 9.5f, "한 번에 붙지 않음(묵직하게)");
            assist.Update(settings.pullSeconds);
            Assert.AreEqual(10f, assist.Apply(hand).z, 1e-3f);

            assist.Release();
            assist.Update(settings.releaseSeconds * 2f);
            Assert.AreEqual(0f, assist.Weight);
            Assert.AreEqual(hand, assist.Apply(hand));
        }

        [Test]
        public void Assist_PullFollowsRobotFistProgress()
        {
            // 사람 손은 다 뻗었는데 로봇 주먹은 반만 왔으면 끌림도 반만. 주먹이 보스에 먼저 붙지 않는다.
            var settings = new AttackAssistSettings();
            var assist = new AttackAssist(settings);
            var zone = new Vector3(0f, 0f, 10f);
            assist.Begin(zone, new Vector3(0f, 0f, 0.3f));
            assist.Update(settings.pullSeconds * 2f);
            Assert.AreEqual(0f, assist.Apply(Vector3.zero).z, 1e-4f, "아직 안 움직임");

            assist.Track(new Vector3(0f, 0f, 0.6f), new Vector3(0f, 0f, 0.9f));
            Assert.AreEqual(0.5f, assist.Progress, 1e-4f);
            Assert.AreEqual(5f, assist.Apply(Vector3.zero).z, 1e-3f);

            assist.Track(new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 0.9f));
            Assert.AreEqual(1f, assist.Progress, 1e-4f);
            assist.Track(new Vector3(0f, 0f, 0.8f), new Vector3(0f, 0f, 0.5f));
            Assert.AreEqual(1f, assist.Progress, 1e-4f, "거둬들여도 줄지 않음");
        }

        [Test]
        public void Assist_BeginWhilePulling_KeepsStartAndProgress()
        {
            var assist = new AttackAssist();
            assist.Begin(new Vector3(0f, 0f, 10f), Vector3.zero);
            assist.Track(new Vector3(0f, 0f, 0.4f), new Vector3(0f, 0f, 0.8f));
            assist.Begin(new Vector3(1f, 0f, 10f), new Vector3(0f, 0f, 0.4f));
            Assert.AreEqual(0.5f, assist.Progress, 1e-4f);
            Assert.AreEqual(new Vector3(1f, 0f, 10f), assist.Point);
        }

        // ── 주먹 접촉 ──

        static Vector3 SphereSurface(Vector3 p, Vector3 center, float radius)
        {
            var offset = p - center;
            return offset.magnitude <= radius ? p : center + offset.normalized * radius;
        }

        [Test]
        public void FistContact_FastFistPassingThrough_IsCaught()
        {
            var center = new Vector3(0f, 0f, 10f);
            // 한 프레임에 구(반지름 2)를 앞뒤로 뚫고 지나감: 양 끝은 둘 다 표면에서 멀다.
            var hit = FistContact.Sweep(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f), 0.5f,
                p => SphereSurface(p, center, 2f), out var point);
            Assert.IsTrue(hit);
            Assert.Less(point.z, center.z);
        }

        [Test]
        public void FistContact_PassingBeside_Misses()
        {
            var center = new Vector3(0f, 0f, 10f);
            var hit = FistContact.Sweep(new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 20f), 0.5f,
                p => SphereSurface(p, center, 2f), out _);
            Assert.IsFalse(hit);
        }
    }
}
