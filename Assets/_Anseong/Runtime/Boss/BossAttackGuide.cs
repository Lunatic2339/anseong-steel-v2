using Anseong.Pilot;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 공격 안내 글자(선우 11/04 "보스 공격마다 내가 뭘 봐야 하는지 띄워놔, 색으로만 구분하니 모르겠음"). 순수 함수 → EditMode 테스트.
    /// 색은 세기(일반·강·막기 불가)만 말하므로, 무슨 공격인지·어느 팔로 무엇을 할지는 이 글자로 알려 준다.
    /// </summary>
    public static class BossAttackGuide
    {
        public static string Name(BossAttackKind kind, int hits)
        {
            switch (kind)
            {
                case BossAttackKind.PunchLeft: return "왼손 펀치";
                case BossAttackKind.PunchRight: return "오른손 펀치";
                case BossAttackKind.AlternatingCombo: return $"좌우 연타 {Hits(hits)}번";
                case BossAttackKind.SlamBoth: return "두 손 내려찍기 (강공격)";
                case BossAttackKind.Charge: return "돌진 (막기 불가)";
                default: return kind.ToString();
            }
        }

        /// <summary>예고 때 할 일. firstSide = 첫 타가 들어오는 쪽.</summary>
        public static string Action(BossAttackKind kind, PilotSide firstSide)
        {
            switch (kind)
            {
                case BossAttackKind.PunchLeft: return "왼팔 들어 막기";
                case BossAttackKind.PunchRight: return "오른팔 들어 막기";
                case BossAttackKind.AlternatingCombo: return $"{Arm(firstSide)}부터 번갈아 막기";
                case BossAttackKind.SlamBoth: return "두 팔 다 들어 막기";
                case BossAttackKind.Charge: return "두 스틱을 같은 쪽으로 밀어 피하기";
                default: return string.Empty;
            }
        }

        /// <summary>연타 중 다음 타 안내. hit = 방금 들어온 타(0부터), nextSide = 다음 타가 들어오는 쪽. 다음이 없으면 빈 글자.</summary>
        public static string Next(int hit, int hits, PilotSide nextSide)
        {
            var next = hit + 1;
            if (next >= Hits(hits)) return string.Empty;
            return $"{next + 1}/{Hits(hits)} → {Arm(nextSide)}";
        }

        public static string Arm(PilotSide side) => side == PilotSide.Left ? "왼팔" : "오른팔";

        static int Hits(int hits) => hits < 1 ? 1 : hits;
    }
}
