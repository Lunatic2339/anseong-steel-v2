using System.Collections.Generic;
using System.Linq;
using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    /// <summary>M2-01 서버 전투 규칙: 중복 보고, 서버 시각 기준 시간 초과, 팔 입력 보고로 부스트, 재시작.</summary>
    public class CombatAuthorityTests
    {
        const PilotSide L = PilotSide.Left;
        const PilotSide R = PilotSide.Right;

        static HitReport Report(PilotSide side, PunchKind kind, int strike, double clientTime = 0)
            => new HitReport { Side = side, Kind = kind, StrikeId = strike, ClientTime = clientTime };

        [Test]
        public void SameReportTwice_CountsOnce()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            Assert.IsTrue(server.SubmitHit(Report(R, PunchKind.Jab, 1), 0.0, events));
            Assert.IsFalse(server.SubmitHit(Report(R, PunchKind.Jab, 1), 0.1, events), "같은 동작 번호는 버린다");
            Assert.AreEqual(1, events.Count(e => e.Cue == CombatCue.Hit));
            Assert.AreEqual(97f, server.Snapshot.BossHealth, 1e-4f, "잽 3% 한 번만");
        }

        [Test]
        public void ComboTimeout_UsesServerTime_NotClientTime()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            // 보낸 쪽 시계는 아무 값이어도 판정에 쓰지 않는다.
            server.SubmitHit(Report(R, PunchKind.Jab, 1, clientTime: 1000.0), 10.0, events);
            Assert.AreEqual(1, server.Snapshot.ComboStep);
            server.Tick(12.3, events);
            Assert.AreEqual(1, server.Snapshot.ComboStep, "서버 시각 2.3초: 아직 이어짐");
            events.Clear();
            // 보낸 쪽 시각이 첫 명중 직후처럼 보여도 서버 시각이 2.4초를 넘었으면 끊긴다.
            server.SubmitHit(Report(L, PunchKind.Jab, 1, clientTime: 1000.5), 12.6, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.ComboBroken && e.Break == ComboBreak.Timeout));
            Assert.AreEqual(1, server.Snapshot.ComboStep, "새 연계의 첫 잽");
        }

        [Test]
        public void TimeoutWithoutHits_BreaksOnTick()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            server.SubmitHit(Report(R, PunchKind.Jab, 1), 0.0, events);
            events.Clear();
            server.Tick(2.5, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.ComboBroken && e.Break == ComboBreak.Timeout));
            Assert.AreEqual(0, server.Snapshot.ComboStep);
        }

        [Test]
        public void Boost_FromReportedArmInput()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            server.SetArmInput(R, true, false);
            server.SubmitHit(Report(R, PunchKind.Jab, 1), 0.0, events);
            server.SubmitHit(Report(L, PunchKind.Jab, 1), 0.5, events);
            server.SubmitHit(Report(R, PunchKind.Hook, 2), 1.0, events);
            Assert.IsTrue(server.Snapshot.BoostOpen);
            server.Tick(1.05, events);
            Assert.IsTrue(server.Snapshot.BoostOpen, "트리거 전에는 열려 있음");
            server.SetArmInput(R, true, true);
            server.Tick(1.1, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.Boost));
            Assert.IsFalse(server.Snapshot.BoostOpen);
            events.Clear();
            server.Tick(1.2, events);
            Assert.IsFalse(events.Any(e => e.Cue == CombatCue.Boost), "누름은 한 번만 쓴다");
        }

        [Test]
        public void Boost_GripReleaseReported_Misses()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            server.SetArmInput(L, true, false);
            server.SubmitHit(Report(L, PunchKind.Jab, 1), 0.0, events);
            server.SubmitHit(Report(R, PunchKind.Jab, 1), 0.5, events);
            server.SubmitHit(Report(L, PunchKind.Hook, 2), 1.0, events);
            events.Clear();
            server.SetArmInput(L, false, false);
            server.Tick(1.1, events);
            Assert.IsTrue(events.Any(e => e.Cue == CombatCue.BoostMissed));
        }

        [Test]
        public void Reset_RestoresHealthAndCombo_AcceptsOldStrikeIdsAgain()
        {
            var server = new CombatAuthority(new CombatResolver());
            var events = new List<CombatEvent>();
            server.SubmitHit(Report(R, PunchKind.Hook, 5), 0.0, events);
            server.Reset();
            var s = server.Snapshot;
            Assert.AreEqual(s.BossMax, s.BossHealth);
            Assert.AreEqual(0, s.ComboStep);
            Assert.IsFalse(s.BoostOpen);
            Assert.IsTrue(server.SubmitHit(Report(R, PunchKind.Jab, 6), 1.0, events));
        }
    }
}
