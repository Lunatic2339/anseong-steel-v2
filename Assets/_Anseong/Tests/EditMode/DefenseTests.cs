using System.Collections.Generic;
using System.Linq;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>M2-03: 대응별 결과, 한 팔만 막은 강공격, 회피 시간 창·방향 불일치, 위기 피해 감소 경계, 연타 4타.</summary>
    public class DefenseTests
    {
        const PilotSide L = PilotSide.Left;
        const PilotSide R = PilotSide.Right;

        // ── 대응 판정 ──

        [Test]
        public void SinglePunch_NeedsGuardOnIncomingSide()
        {
            Assert.AreEqual(DefenseResult.Blocked, DefenseRules.Judge(BossCounter.LeftGuard, L, true, false, false));
            Assert.AreEqual(DefenseResult.Hit, DefenseRules.Judge(BossCounter.LeftGuard, L, false, true, false), "반대 팔로 막으면 맞음");
            Assert.AreEqual(DefenseResult.Blocked, DefenseRules.Judge(BossCounter.RightGuard, R, false, true, false));
            Assert.AreEqual(DefenseResult.Hit, DefenseRules.Judge(BossCounter.RightGuard, R, true, false, false), "오른팔 고정 버그 아님: 왼팔 가드는 오른손 펀치를 못 막음");
            Assert.AreEqual(DefenseResult.Blocked, DefenseRules.Judge(BossCounter.RightGuard, R, true, true, false), "넓은 양팔 가드도 인정");
        }

        [Test]
        public void Slam_NeedsBothArms_OneArmIsPartialFail()
        {
            Assert.AreEqual(DefenseResult.Blocked, DefenseRules.Judge(BossCounter.BothGuard, R, true, true, false));
            Assert.AreEqual(DefenseResult.PartialBlock, DefenseRules.Judge(BossCounter.BothGuard, R, true, false, false));
            Assert.AreEqual(DefenseResult.Hit, DefenseRules.Judge(BossCounter.BothGuard, R, false, false, false));
            Assert.IsFalse(DefenseRules.IsSuccess(DefenseResult.PartialBlock));
        }

        [Test]
        public void Charge_OnlyDodgeWorks()
        {
            Assert.AreEqual(DefenseResult.Hit, DefenseRules.Judge(BossCounter.Dodge, R, true, true, false), "가드 불가");
            Assert.AreEqual(DefenseResult.Dodged, DefenseRules.Judge(BossCounter.Dodge, R, false, false, true));
        }

        // ── 회피 ──

        [Test]
        public void Dodge_BothSticksSameSide_WithinWindow()
        {
            var dodge = new DodgeRules();
            Assert.IsFalse(dodge.Update(L, new Vector2(-0.9f, 0f), 9.5));
            Assert.IsTrue(dodge.Update(R, new Vector2(-0.8f, 0.1f), 9.6), "두 번째 스틱이 맞는 순간이 회피 시작");
            Assert.AreEqual(-1, dodge.Direction);
            Assert.IsTrue(dodge.Dodged(10.0), "명중 0.4초 전 시작");
            Assert.IsTrue(dodge.Dodged(10.2), "0.6초 경계");
            Assert.IsFalse(dodge.Dodged(10.3), "0.6초보다 일찍 시작한 것은 안 됨");
            Assert.IsFalse(dodge.Dodged(9.55), "명중 뒤에 시작한 것은 안 됨");
        }

        [Test]
        public void Dodge_HeldEarly_DoesNotCount()
        {
            var dodge = new DodgeRules();
            dodge.Update(L, new Vector2(1f, 0f), 0.0);
            dodge.Update(R, new Vector2(1f, 0f), 0.0);
            // 계속 밀고 있어도 새 시작이 아니다.
            Assert.IsFalse(dodge.Update(L, new Vector2(0.95f, 0.05f), 5.0));
            Assert.IsFalse(dodge.Dodged(5.2));
        }

        [Test]
        public void Dodge_DirectionMismatch_Or_Weak_Or_Forward_IsNot()
        {
            var s = new DodgeSettings();
            Assert.IsFalse(DodgeRules.Agree(new Vector2(-1f, 0f), new Vector2(1f, 0f), s, out _), "반대 방향");
            Assert.IsFalse(DodgeRules.Agree(new Vector2(1f, 0f), new Vector2(0.3f, 0f), s, out _), "크기 0.5 미만");
            Assert.IsFalse(DodgeRules.Agree(new Vector2(1f, 0f), new Vector2(0.7f, 0.7f), s, out _), "45° 차이");
            Assert.IsFalse(DodgeRules.Agree(new Vector2(0f, 1f), new Vector2(0.1f, 1f), s, out _), "앞으로 미는 것은 회피 아님");
            Assert.IsTrue(DodgeRules.Agree(new Vector2(1f, 0.2f), new Vector2(0.9f, -0.1f), s, out var dir));
            Assert.AreEqual(1, dir);
        }

        // ── 조종사 HP ──

        [Test]
        public void CrisisReduction_Boundaries()
        {
            var hp = new PilotHealth();
            Assert.AreEqual(1f, hp.Multiplier(), 1e-5f, "100%");
            Assert.AreEqual(6f, hp.TakeHit(6f), 1e-4f);

            var mid = new PilotHealth();
            mid.TakeHit(50f); // HP 50 (배율 1)
            Assert.AreEqual(1f, mid.Multiplier(), 1e-5f, "50% 경계는 100%");
            mid.TakeHit(15f); // HP 35
            Assert.AreEqual(0.75f, mid.Multiplier(), 1e-4f, "35%는 50%와 20% 사이 가운데 → 0.75");

            var low = new PilotHealth();
            low.TakeHit(80f); // HP 20
            Assert.AreEqual(0.5f, low.Multiplier(), 1e-5f, "20% 이하 최소 배율");
            Assert.AreEqual(6f, low.TakeHit(12f), 1e-4f, "12 × 0.5");

            var off = new PilotHealth(new PilotHealthSettings { crisisReduction = false });
            off.TakeHit(90f);
            Assert.AreEqual(1f, off.Multiplier(), 1e-5f, "끄면 그대로");
        }

        [Test]
        public void PilotHealth_CanStillBeDefeated_AndRestore()
        {
            var hp = new PilotHealth();
            for (var i = 0; i < 100 && !hp.IsDefeated; i++) hp.TakeHit(15f);
            Assert.IsTrue(hp.IsDefeated, "최소 배율 > 0이라 패배 가능");
            Assert.IsFalse(hp.RestoreTo(60f), "쓰러진 뒤엔 회복 안 함(재시작은 Reset)");
            hp.Reset();
            hp.TakeHit(60f);
            Assert.IsTrue(hp.RestoreTo(60f));
            Assert.AreEqual(60f, hp.Current, 1e-4f);
            Assert.IsFalse(hp.RestoreTo(50f), "이미 높으면 그대로");
        }

        // ── 서버 묶음 ──

        [Test]
        public void Authority_HitBreaksCombo_BlockKeepsIt()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            server.SubmitHit(new HitReport { Side = R, Kind = PunchKind.Jab, StrikeId = 1 }, 0.0, events);
            Assert.AreEqual(1, server.Snapshot.ComboStep);

            server.SetGuard(R, true);
            var blocked = server.ResolveBossStrike(BossCounter.RightGuard, R, 6f, (int)BossAttackKind.PunchRight, 0.5, events);
            Assert.AreEqual(DefenseResult.Blocked, blocked);
            Assert.AreEqual(1, server.Snapshot.ComboStep, "막으면 연계 유지");
            Assert.AreEqual(100f, server.Snapshot.PilotHealth, 1e-4f);

            events.Clear();
            var hit = server.ResolveBossStrike(BossCounter.LeftGuard, L, 6f, (int)BossAttackKind.PunchLeft, 0.8, events);
            Assert.AreEqual(DefenseResult.Hit, hit);
            Assert.AreEqual(94f, server.Snapshot.PilotHealth, 1e-4f);
            Assert.AreEqual(0, server.Snapshot.ComboStep, "맞으면 연계 끝");
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.GuardFail), "반대 팔만 들고 있었음");
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.PilotHit && Mathf.Approximately(e.Damage, 6f)));
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.ComboBroken && e.Break == ComboBreak.Damaged));
        }

        [Test]
        public void AlternatingCombo_FourHits_EachJudgedOnItsSide()
        {
            var server = new CombatAuthority(new CombatResolver());
            var machine = new BossAttackStateMachine();
            var spec = BossAttackSpec.Default(BossAttackKind.AlternatingCombo);
            var events = new List<CombatEvent>();
            var results = new List<DefenseResult>();
            machine.Immediate = e =>
            {
                if (e.Type != BossEventType.Impact) return;
                // 오른팔만 들고 있음: 오른쪽 타(0·2)만 막힌다.
                server.SetGuard(R, true);
                server.SetGuard(L, false);
                var r = server.ResolveBossStrike(spec.counter, e.Side, spec.failDamage, (int)spec.kind, e.Time, events);
                results.Add(r);
                machine.ReportOutcome(DefenseRules.IsSuccess(r));
            };
            machine.Begin(spec, 0.0, null);
            machine.Update(10.0, false, null);
            CollectionAssert.AreEqual(new[] { DefenseResult.Blocked, DefenseResult.Hit, DefenseResult.Blocked, DefenseResult.Hit }, results);
            Assert.AreEqual(2, events.Count(e => e.Cue == CombatCue.PilotHit));
            Assert.AreEqual(92f, server.Snapshot.PilotHealth, 1e-3f, "타당 4 × 2타");
            Assert.IsFalse(machine.AllSucceeded);
        }

        [Test]
        public void Authority_StickOnset_EmitsDodgeStart_AndDodgesCharge()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            server.SetStick(L, new Vector2(1f, 0f), 1.7, events);
            server.SetStick(R, new Vector2(1f, 0f), 1.75, events);
            Assert.AreEqual(1, events.Count(e => e.Cue == CombatCue.DodgeStart));
            var result = server.ResolveBossStrike(BossCounter.Dodge, R, 15f, (int)BossAttackKind.Charge, 2.0, events);
            Assert.AreEqual(DefenseResult.Dodged, result);
            Assert.AreEqual(100f, server.Snapshot.PilotHealth, 1e-4f);
        }
    }
}
