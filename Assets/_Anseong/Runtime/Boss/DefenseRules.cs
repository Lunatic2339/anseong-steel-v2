using Anseong.Pilot;

namespace Anseong.Boss
{
    public enum DefenseResult
    {
        /// <summary>요구한 가드로 막음: 피해 0.</summary>
        Blocked,
        /// <summary>양팔 가드가 필요한데 한 팔만: 피해 감소 없는 실패(01).</summary>
        PartialBlock,
        /// <summary>협동 회피 성공: 피해 0.</summary>
        Dodged,
        /// <summary>맞음.</summary>
        Hit,
    }

    /// <summary>
    /// 보스 공격 명중 순간의 대응 판정(01 "가드와 회피", M2-03). 순수 함수.
    /// 일반 펀치는 실제로 들어오는 쪽 팔 가드만 인정(옛 코드 "항상 오른팔" 버그 5 수정). 연타는 타마다 들어오는 쪽.
    /// </summary>
    public static class DefenseRules
    {
        public static DefenseResult Judge(BossCounter counter, PilotSide incoming, bool leftGuard, bool rightGuard, bool dodged)
        {
            switch (counter)
            {
                case BossCounter.LeftGuard:
                case BossCounter.RightGuard:
                case BossCounter.AlternateGuard:
                    return (incoming == PilotSide.Left ? leftGuard : rightGuard) ? DefenseResult.Blocked : DefenseResult.Hit;
                case BossCounter.BothGuard:
                    if (leftGuard && rightGuard) return DefenseResult.Blocked;
                    return leftGuard || rightGuard ? DefenseResult.PartialBlock : DefenseResult.Hit;
                case BossCounter.Dodge:
                    return dodged ? DefenseResult.Dodged : DefenseResult.Hit;
                default:
                    return DefenseResult.Hit;
            }
        }

        public static bool IsSuccess(DefenseResult result) => result == DefenseResult.Blocked || result == DefenseResult.Dodged;
    }
}
