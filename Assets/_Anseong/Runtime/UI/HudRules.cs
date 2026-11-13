using Anseong.Boss;
using Anseong.Pilot;

namespace Anseong.UI
{
    /// <summary>HUD에 띄우는 정보 종류(M4-06).</summary>
    public enum HudInfo
    {
        /// <summary>보스 공격이 오는 쪽(좌/우 펀치·주먹 포탄). 오른쪽 조종사만.</summary>
        AttackSide,
        /// <summary>돌진 피할 방향(서버가 돌진마다 정한다). 오른쪽 조종사만.</summary>
        DodgeSide,
        /// <summary>숨은 드론이 쏜 방향. 왼쪽 조종사만(왼팔 = 스캐너).</summary>
        DroneDirection,
        /// <summary>둘 다 보는 것(보스 무기 금, 기체 HP, 두 팔 발광 내려찍기 등).</summary>
        Shared,
    }

    /// <summary>
    /// 한쪽에만 보이는 HUD(M4-06, 대본 "협동은 반대쪽이 보고 불러 준다"). 순수 함수.
    /// 공격 방향은 오른쪽에만, 드론 방향은 왼쪽에만. 해킹당한 사람의 HUD는 잡음으로 덮이고, 동료가 대신 모든 예고를 본다.
    /// 1인 양팔(개발용)은 다 보인다.
    /// </summary>
    public static class HudRules
    {
        public static PilotSide Owner(HudInfo info) => info == HudInfo.DroneDirection ? PilotSide.Left : PilotSide.Right;

        /// <param name="hacked">해킹당한 팔(없으면 null).</param>
        public static bool Visible(HudInfo info, PilotSide viewer, bool bothArms, PilotSide? hacked)
        {
            if (bothArms) return true;
            if (hacked.HasValue && hacked.Value == viewer) return false;
            if (info == HudInfo.Shared) return true;
            if (hacked.HasValue) return true; // 동료가 해킹당함 → 대신 다 본다
            return Owner(info) == viewer;
        }

        /// <summary>
        /// 보스 공격 예고 때 조종사 HUD에 띄울 글(대응 방식에 맞춰). 띄울 것이 없으면 빈 글자.
        /// counter = 그 기술의 요구 대응, side = 첫 타가 들어오는 쪽(돌진은 피해야 하는 쪽),
        /// seesAttackSide·seesDodgeSide = 이 사람이 그 정보를 보는지(<see cref="Visible"/>).
        /// 둘이 X자로 막기·충격파는 둘 다 본다. 공격 오는 쪽·돌진 피할 쪽은 오른쪽 조종사만(왼쪽은 "동료가 부르는 쪽으로").
        /// </summary>
        public static string AttackCallout(BossAttackKind kind, BossCounter counter, PilotSide side, bool seesAttackSide, bool seesDodgeSide)
        {
            switch (counter)
            {
                case BossCounter.BothGuard:
                    return kind == BossAttackKind.Grab ? "붙잡으러 온다: 둘이 X자로 막아라" : "두 팔 발광: 둘이 X자로 막아라";
                case BossCounter.Dodge:
                    if (kind != BossAttackKind.Charge) return "충격파! 같이 옆으로 피해라";
                    if (!seesDodgeSide) return "돌진! 동료가 부르는 쪽으로";
                    return side == PilotSide.Left ? "돌진! ◀◀ 왼쪽으로 피해라" : "돌진! 오른쪽으로 피해라 ▶▶";
                case BossCounter.AlternateGuard:
                    if (!seesAttackSide) return string.Empty;
                    return side == PilotSide.Left ? "◀◀ 왼쪽부터 연타" : "오른쪽부터 연타 ▶▶";
                default:
                    if (!seesAttackSide) return string.Empty;
                    return side == PilotSide.Left ? "◀◀ 왼쪽 공격" : "오른쪽 공격 ▶▶";
            }
        }

        /// <summary>머리 기준 방향을 글자로(조종석 안에서 고개를 돌릴 쪽).</summary>
        public static string Direction(float yawDegrees, float pitchDegrees)
        {
            string side;
            if (yawDegrees < -100f || yawDegrees > 100f) side = "뒤";
            else if (yawDegrees < -25f) side = "왼쪽";
            else if (yawDegrees > 25f) side = "오른쪽";
            else side = "앞";
            if (pitchDegrees > 20f) return side + " 위";
            return side;
        }
    }
}
