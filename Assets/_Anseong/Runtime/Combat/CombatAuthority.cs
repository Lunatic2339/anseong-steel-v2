using System.Collections.Generic;
using Anseong.Boss;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 서버에서만 도는 전투 규칙 묶음(M2-01~). 보고(명중·헛침·팔 입력·가드·회피 스틱)를 받아 서버 시각으로 규칙을 돌린다.
    /// 보낸 쪽 시각(<see cref="HitReport.ClientTime"/>)은 판정에 쓰지 않는다. 순수 클래스(네트워크 모름) → EditMode 테스트.
    /// M2-03: 보스 공격 명중 순간의 대응 판정(<see cref="ResolveBossStrike"/>)과 조종사 로봇 HP.
    /// </summary>
    public sealed class CombatAuthority
    {
        readonly CombatResolver resolver;
        readonly PilotHealth pilot;
        readonly DodgeRules dodge;
        readonly FinisherRules finisher;
        readonly GrabSlashRules grab;
        readonly bool[] gripHeld = new bool[2];
        readonly bool[] triggerPressed = new bool[2];
        readonly bool[] guarding = new bool[2];

        public CombatAuthority(CombatResolver resolver, PilotHealthSettings pilotSettings = null, DodgeSettings dodgeSettings = null,
            FinisherSettings finisherSettings = null, GrabSlashSettings grabSettings = null)
        {
            this.resolver = resolver ?? new CombatResolver();
            pilot = new PilotHealth(pilotSettings);
            dodge = new DodgeRules(dodgeSettings);
            finisher = new FinisherRules(finisherSettings);
            grab = new GrabSlashRules(grabSettings);
        }

        /// <summary>잡고 베기(11/13). 왼팔 "뻗고 그립"은 필살기 준비 보고(뻗음)와 그립 보고를 그대로 쓴다.</summary>
        public GrabSlashRules Grab => grab;

        /// <summary>보스가 지금 붙잡을 수 있는 상태(휘청·빈틈·붙잡힘)인지. 보스 두뇌가 매 프레임 쓴다.</summary>
        public bool BossGrabbable { get; set; }

        /// <summary>합동 필살기(M2-05). 보스 두뇌가 무릎 때 켠다.</summary>
        public FinisherRules Finisher => finisher;

        /// <summary>필살기 준비 보고.</summary>
        public void SetFinisherReady(PilotSide side, bool ready, double serverTime) => finisher.ArmReady(side, ready, serverTime);

        public CombatResolver Resolver => resolver;
        public PilotHealth Pilot => pilot;
        public DodgeRules Dodge => dodge;
        public CombatSnapshot Snapshot => CombatSnapshot.Of(resolver, pilot, BossPhase);

        /// <summary>보스 페이즈(1·2·3, 무릎 4). 보스 두뇌가 쓰고 상태로 복제된다(HUD M4).</summary>
        public int BossPhase { get; set; } = 1;

        /// <summary>명중 보고. 같은 동작은 한 번만 받아들인다.</summary>
        /// 보스가 무릎 꿇은 동안(필살기 차례)은 펀치를 받지 않는다(M2-06, 선우 11/04: 팔을 뻗는 게 펀치로 잡히지 않게).
        public bool SubmitHit(HitReport report, double serverTime, List<CombatEvent> events)
        {
            if (finisher.Enabled || !resolver.Resolve(report, serverTime, events)) return false;
            // 잡고 베기: 왼팔이 붙잡은 동안 오른팔 명중 한 방에 큰 추가 피해.
            if (report.Side == PilotSide.Right && grab.TrySlash(serverTime))
            {
                var slash = Make(CombatCue.GrabSlash, PilotSide.Right, serverTime, 0, grab.Settings.reelSeconds);
                slash.Damage = resolver.Boss.Apply(grab.Settings.slashDamage);
                slash.BossHealth = resolver.Boss.Current;
                slash.Point = report.Point;
                events?.Add(slash);
            }
            return true;
        }

        public void SubmitMiss(PilotSide side, PunchKind kind, int strikeId, double serverTime, List<CombatEvent> events)
        {
            if (finisher.Enabled) return;
            resolver.ReportMiss(side, kind, strikeId, serverTime, events);
        }

        /// <summary>팔 입력 보고. 트리거 누름은 다음 <see cref="Tick"/>에서 한 번 쓴다.</summary>
        public void SetArmInput(PilotSide side, bool grip, bool pressed)
        {
            var h = (int)side;
            gripHeld[h] = grip;
            if (pressed) triggerPressed[h] = true;
        }

        public bool GripHeld(PilotSide side) => gripHeld[(int)side];

        /// <summary>가드 보고(그 팔 주인 PC가 사람 손으로 판정한 값).</summary>
        public void SetGuard(PilotSide side, bool value) => guarding[(int)side] = value;

        readonly PilotSide[] cover = { PilotSide.Left, PilotSide.Right };

        /// <summary>방패 하나로 양쪽 막기(M4-03, 확정 사항 11). 끄면 들어오는 쪽 팔이 막는 옛 규칙.</summary>
        public bool ShieldBothSides { get; set; } = true;

        /// <summary>그 팔이 얼굴 앞 어느 쪽을 덮는지(방패).</summary>
        public void SetGuardCover(PilotSide side, PilotSide value) => cover[(int)side] = value;
        public bool Guarding(PilotSide side) => guarding[(int)side];

        /// <summary>회피 스틱 보고. 두 스틱이 같은 방향으로 새로 맞으면 DodgeStart.</summary>
        public void SetStick(PilotSide side, Vector2 stick, double serverTime, List<CombatEvent> events)
        {
            if (!dodge.Update(side, stick, serverTime)) return;
            events?.Add(Make(CombatCue.DodgeStart, side, serverTime, amount: dodge.Direction));
        }

        /// <summary>
        /// 보스 공격 명중 순간(서버). 가드·회피를 판정해 사건을 더하고, 맞으면 조종사 HP를 깎고 연계를 끊는다.
        /// 결과가 성공(막음·피함)이면 보스 빈틈이 길어진다(상태기계에 넘김).
        /// </summary>
        public DefenseResult ResolveBossStrike(BossCounter counter, PilotSide incoming, float failDamage, int attackCode, double serverTime, List<CombatEvent> events)
        {
            var result = ShieldBothSides
                ? DefenseRules.JudgeShield(counter, incoming, guarding[0], cover[0], guarding[1], dodge.Dodged(serverTime))
                : DefenseRules.Judge(counter, incoming, guarding[0], guarding[1], dodge.Dodged(serverTime));
            switch (result)
            {
                case DefenseResult.Blocked:
                    events?.Add(Make(CombatCue.GuardBlock, incoming, serverTime, attackCode));
                    return result;
                case DefenseResult.Dodged:
                    events?.Add(Make(CombatCue.Dodge, incoming, serverTime, attackCode, dodge.Direction));
                    return result;
                case DefenseResult.PartialBlock:
                    events?.Add(Make(CombatCue.GuardFail, incoming, serverTime, attackCode));
                    break;
                default:
                    if (counter != BossCounter.Dodge && (guarding[0] || guarding[1]))
                        events?.Add(Make(CombatCue.GuardFail, incoming, serverTime, attackCode));
                    break;
            }
            if (pilot.IsDefeated) return result;
            var damage = pilot.TakeHit(failDamage);
            var hit = Make(CombatCue.PilotHit, incoming, serverTime, attackCode, pilot.Percent);
            hit.Damage = damage;
            events?.Add(hit);
            resolver.ReportRobotDamaged(serverTime, events);
            if (pilot.IsDefeated) events?.Add(Make(CombatCue.PilotDefeated, incoming, serverTime, attackCode));
            return result;
        }

        /// <summary>서버 프레임마다: 부스트 입력과 시간 초과(연계·부스트 창).</summary>
        public void Tick(double serverTime, List<CombatEvent> events)
        {
            for (var h = 0; h < 2; h++)
            {
                // 필살기 차례엔 부스트 입력도 받지 않는다(트리거를 쥐어도 부스트 소리가 나지 않게).
                if (!finisher.Enabled) resolver.UpdateInput((PilotSide)h, gripHeld[h], triggerPressed[h], serverTime, events);
                triggerPressed[h] = false;
            }
            resolver.Tick(serverTime, events);
            if (finisher.Enabled) return;
            const int left = (int)PilotSide.Left;
            switch (grab.Update(serverTime, BossGrabbable, finisher.IsReady(PilotSide.Left) && gripHeld[left]))
            {
                case GrabStep.Started:
                    events?.Add(Make(CombatCue.GrabStart, PilotSide.Left, serverTime, 0, grab.Settings.maxHoldSeconds));
                    break;
                case GrabStep.Broken:
                    events?.Add(Make(CombatCue.GrabBreak, PilotSide.Left, serverTime, 0, 0f));
                    break;
                case GrabStep.TimedOut:
                    events?.Add(Make(CombatCue.GrabBreak, PilotSide.Left, serverTime, 0, 1f));
                    break;
            }
        }

        /// <summary>재시작: HP(보스·조종사)·연계·부스트·중복 기록·회피를 처음으로. 그립·가드는 실제 손 상태라 그대로 둔다.</summary>
        public void Reset()
        {
            resolver.Reset();
            pilot.Reset();
            finisher.Reset();
            grab.Reset();
            BossPhase = 1;
            dodge.Reset();
            triggerPressed[0] = triggerPressed[1] = false;
        }

        CombatEvent Make(CombatCue cue, PilotSide side, double time, int code = 0, float amount = 0f) => new CombatEvent
        {
            Cue = cue, Side = side, Kind = PunchKind.None, Time = time, Code = code, Amount = amount,
            ComboStep = resolver.Combo.Step, BossHealth = resolver.Boss.Current,
        };
    }
}
