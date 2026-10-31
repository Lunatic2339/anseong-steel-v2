using System;
using System.Collections.Generic;
using Anseong.Pilot;

namespace Anseong.Combat
{
    /// <summary>
    /// 명중 보고 → 중복 검사 → 피해 → 연계·부스트 갱신을 한 곳에서 한다(판정 순서: 01 "공격"). 순수 클래스.
    /// 결과는 <see cref="CombatEvent"/> 목록으로만 내보내고, 소리·애니메이션·안내판은 그 이벤트를 듣는다.
    /// M1은 한 PC에서 바로 부르고, M3에서는 서버가 같은 클래스를 쓴다(1인 모드 전용 규칙 없음).
    /// </summary>
    public sealed class CombatResolver
    {
        readonly ComboRules combo;
        readonly BoostWindow boost;
        readonly BossHealth boss;
        // 팔마다 이미 명중한 마지막 동작 번호. 동작 번호는 팔마다 1부터 오른다.
        readonly int[] lastHitStrike = { 0, 0 };

        /// <summary>
        /// 보스가 지금 피해를 받는지(빈틈·경직, M2-02). 비어 있으면 항상 받는다(M1과 같음).
        /// 거짓이면 명중은 <see cref="CombatCue.Blocked"/>(피해 0, 연계 차례 그대로)가 된다.
        /// </summary>
        public Func<bool> BossVulnerable;

        public CombatResolver(ComboSettings comboSettings = null, BoostSettings boostSettings = null, BossHealthSettings bossSettings = null)
        {
            combo = new ComboRules(comboSettings);
            boost = new BoostWindow(boostSettings);
            boss = new BossHealth(bossSettings);
        }

        public ComboRules Combo => combo;
        public BoostWindow Boost => boost;
        public BossHealth Boss => boss;

        public void Reset()
        {
            combo.Reset();
            boost.Close();
            boss.Reset();
            lastHitStrike[0] = lastHitStrike[1] = 0;
        }

        /// <summary>이 동작이 이미 명중했는지(때린 쪽이 같은 동작을 두 번 보내지 않게 미리 볼 때).</summary>
        public bool AlreadyHit(PilotSide side, int strikeId) => strikeId > 0 && strikeId <= lastHitStrike[(int)side];

        /// <summary>명중 보고 하나. 받아들였으면 true. 이벤트는 events에 더한다.</summary>
        public bool Resolve(HitReport report, double time, List<CombatEvent> events)
        {
            var h = (int)report.Side;
            if (boss.IsDefeated || report.Kind == PunchKind.None || report.StrikeId <= 0 || report.StrikeId <= lastHitStrike[h]) return false;
            lastHitStrike[h] = report.StrikeId;

            if (BossVulnerable != null && !BossVulnerable())
            {
                // 보스가 막음: 피해 0, 연계는 끊지도 올리지도 않는다(05 "보스 한 번의 공격 흐름").
                var blocked = Make(CombatCue.Blocked, report.Side, report.Kind, time);
                blocked.Quality = report.Quality;
                blocked.Zone = report.Zone;
                blocked.Point = report.Point;
                Add(events, blocked);
                return true;
            }

            // 다른 공격이 들어오면 열려 있던 부스트 창은 닫힌다.
            if (boost.IsOpen) Add(events, Make(CombatCue.BoostMissed, boost.Side, PunchKind.Hook, time));
            boost.Close();

            var damage = boss.Apply(boss.DamageOf(report.Kind));
            var outcome = combo.OnHit(report.Side, report.Kind, time);
            if (outcome.Broken != ComboBreak.None)
            {
                var broken = Make(CombatCue.ComboBroken, report.Side, report.Kind, time);
                broken.Break = outcome.Broken;
                Add(events, broken);
            }

            var hit = Make(CombatCue.Hit, report.Side, report.Kind, time);
            hit.Damage = damage;
            hit.ComboStep = outcome.Step;
            hit.Quality = report.Quality;
            hit.Zone = report.Zone;
            hit.Point = report.Point;
            Add(events, hit);

            if (outcome.Advanced)
            {
                var step = Make(CombatCue.ComboStep, report.Side, report.Kind, time);
                step.ComboStep = outcome.Step;
                Add(events, step);
            }
            if (boss.IsDefeated)
            {
                Add(events, Make(CombatCue.BossDefeated, report.Side, report.Kind, time));
                return true;
            }
            if (outcome.Finished)
            {
                boost.Open(report.Side, time);
                Add(events, Make(CombatCue.BoostReady, report.Side, PunchKind.Hook, time));
            }
            return true;
        }

        /// <summary>그립을 쥐고 종류가 인식된 공격이 명중 없이 끝남(헛침). 연계가 끊긴다.</summary>
        public void ReportMiss(PilotSide side, PunchKind kind, int strikeId, double time, List<CombatEvent> events)
        {
            if (boss.IsDefeated || AlreadyHit(side, strikeId)) return;
            Add(events, Make(CombatCue.Miss, side, kind, time));
            var broken = combo.OnMiss(time);
            if (broken == ComboBreak.None) return;
            var e = Make(CombatCue.ComboBroken, side, kind, time);
            e.Break = broken;
            Add(events, e);
        }

        /// <summary>프레임마다 팔 입력(부스트 창). triggerPressed = 이번 프레임에 새로 누름.</summary>
        public void UpdateInput(PilotSide side, bool gripHeld, bool triggerPressed, double time, List<CombatEvent> events)
        {
            var result = boost.Update(side, gripHeld, triggerPressed, time);
            if (result == BoostWindow.Result.Missed)
            {
                Add(events, Make(CombatCue.BoostMissed, side, PunchKind.Hook, time));
                return;
            }
            if (result != BoostWindow.Result.Boosted) return;
            if (BossVulnerable != null && !BossVulnerable())
            {
                Add(events, Make(CombatCue.Blocked, side, PunchKind.Hook, time));
                return;
            }
            var e = Make(CombatCue.Boost, side, PunchKind.Hook, time);
            e.Damage = boss.Apply(boss.BoostDamage);
            e.ComboStep = 3;
            Add(events, e);
            if (boss.IsDefeated) Add(events, Make(CombatCue.BossDefeated, side, PunchKind.Hook, time));
        }

        /// <summary>프레임마다 시간 초과 검사(연계·부스트 창).</summary>
        public void Tick(double time, List<CombatEvent> events)
        {
            var broken = combo.Tick(time);
            if (broken != ComboBreak.None)
            {
                var e = Make(CombatCue.ComboBroken, combo.NextSide, PunchKind.None, time);
                e.Break = broken;
                Add(events, e);
            }
            if (boost.Tick(time) == BoostWindow.Result.Missed)
                Add(events, Make(CombatCue.BoostMissed, boost.Side, PunchKind.Hook, time));
        }

        /// <summary>로봇이 직접 피해를 받음(M2 보스 공격에서 부른다).</summary>
        public void ReportRobotDamaged(double time, List<CombatEvent> events)
        {
            boost.Close();
            var broken = combo.OnDamaged(time);
            if (broken == ComboBreak.None) return;
            var e = Make(CombatCue.ComboBroken, combo.NextSide, PunchKind.None, time);
            e.Break = broken;
            Add(events, e);
        }

        CombatEvent Make(CombatCue cue, PilotSide side, PunchKind kind, double time)
        {
            return new CombatEvent { Cue = cue, Side = side, Kind = kind, Time = time, ComboStep = combo.Step, BossHealth = boss.Current };
        }

        void Add(List<CombatEvent> events, CombatEvent e)
        {
            e.BossHealth = boss.Current;
            events?.Add(e);
        }
    }
}
