using System.Collections.Generic;
using Anseong.Pilot;

namespace Anseong.Combat
{
    /// <summary>
    /// 서버에서만 도는 전투 규칙 묶음(M2-01). 보고(명중·헛침·팔 입력)를 받아 서버 시각으로 <see cref="CombatResolver"/>를 돌린다.
    /// 보낸 쪽 시각(<see cref="HitReport.ClientTime"/>)은 판정에 쓰지 않는다. 순수 클래스(네트워크 모름) → EditMode 테스트.
    /// </summary>
    public sealed class CombatAuthority
    {
        readonly CombatResolver resolver;
        readonly bool[] gripHeld = new bool[2];
        readonly bool[] triggerPressed = new bool[2];

        public CombatAuthority(CombatResolver resolver)
        {
            this.resolver = resolver ?? new CombatResolver();
        }

        public CombatResolver Resolver => resolver;
        public CombatSnapshot Snapshot => CombatSnapshot.Of(resolver);

        /// <summary>명중 보고. 같은 동작은 한 번만 받아들인다.</summary>
        public bool SubmitHit(HitReport report, double serverTime, List<CombatEvent> events) => resolver.Resolve(report, serverTime, events);

        public void SubmitMiss(PilotSide side, PunchKind kind, int strikeId, double serverTime, List<CombatEvent> events)
            => resolver.ReportMiss(side, kind, strikeId, serverTime, events);

        /// <summary>팔 입력 보고. 트리거 누름은 다음 <see cref="Tick"/>에서 한 번 쓴다.</summary>
        public void SetArmInput(PilotSide side, bool grip, bool pressed)
        {
            var h = (int)side;
            gripHeld[h] = grip;
            if (pressed) triggerPressed[h] = true;
        }

        public bool GripHeld(PilotSide side) => gripHeld[(int)side];

        /// <summary>서버 프레임마다: 부스트 입력과 시간 초과(연계·부스트 창).</summary>
        public void Tick(double serverTime, List<CombatEvent> events)
        {
            for (var h = 0; h < 2; h++)
            {
                resolver.UpdateInput((PilotSide)h, gripHeld[h], triggerPressed[h], serverTime, events);
                triggerPressed[h] = false;
            }
            resolver.Tick(serverTime, events);
        }

        /// <summary>재시작: HP·연계·부스트·중복 기록을 처음으로. 그립은 실제 손 상태라 그대로 둔다.</summary>
        public void Reset()
        {
            resolver.Reset();
            triggerPressed[0] = triggerPressed[1] = false;
        }
    }
}
