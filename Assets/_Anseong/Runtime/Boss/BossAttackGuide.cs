using Anseong.Pilot;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 공격 안내 글자(선우 11/04 "보스 공격마다 내가 뭘 봐야 하는지 띄워놔, 색으로만 구분하니 모르겠음"). 순수 함수 → EditMode 테스트.
    /// 색은 세기(일반·강·막기 불가)만 말하므로, 무슨 공격인지·어느 팔로 무엇을 할지는 이 글자로 알려 준다.
    /// 글은 판정 규칙을 따른다(방패 하나로 양쪽 막기, M4-03): 한쪽 공격은 왼팔 방패를 들어오는 쪽으로, 두 손 내려찍기·붙잡기는 둘이 X자, 돌진은 정해진 쪽으로 회피.
    /// 2인 모드에서는 "어느 쪽"을 오른쪽 조종사만 본다(대본: 오른쪽이 보고 불러 준다). 못 보는 사람에게는 쪽을 뺀 글을 준다(seesSide = false).
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
                case BossAttackKind.SwordSlash: return "검 베기 (강공격)";
                case BossAttackKind.Shockwave: return "충격파 (막기 불가)";
                case BossAttackKind.Grab: return "붙잡기 (강공격)";
                case BossAttackKind.Feint: return "속임수 펀치 (늦게 들어옴)";
                case BossAttackKind.ArmLock: return "팔 꺾기";
                default: return kind.ToString();
            }
        }

        /// <summary>기술 이름. 쪽을 못 보는 사람(seesSide = false)에게는 좌/우가 드러나는 이름을 감춘다.</summary>
        public static string Name(BossAttackKind kind, int hits, bool seesSide)
        {
            if (!seesSide && (kind == BossAttackKind.PunchLeft || kind == BossAttackKind.PunchRight)) return "펀치";
            return Name(kind, hits);
        }

        /// <summary>예고 때 할 일(1인 양팔 = 다 보이는 글). side = 첫 타가 들어오는 쪽(돌진은 피할 쪽). 대응은 05 기본값.</summary>
        public static string Action(BossAttackKind kind, PilotSide side) =>
            Action(kind, BossAttackSpec.Default(kind).counter, side, PilotSide.Left, true, true);

        /// <summary>
        /// 예고 때 할 일. counter = 그 기술의 요구 대응(기술 에셋), side = 첫 타가 들어오는 쪽(돌진은 피해야 하는 쪽),
        /// viewer = 이 글을 보는 조종사, bothArms = 1인 양팔(개발용), seesSide = 이 사람이 "어느 쪽"을 볼 수 있는지.
        /// </summary>
        public static string Action(BossAttackKind kind, BossCounter counter, PilotSide side, PilotSide viewer, bool bothArms, bool seesSide)
        {
            var caller = !bothArms && viewer == PilotSide.Right;
            switch (counter)
            {
                case BossCounter.LeftGuard:
                case BossCounter.RightGuard:
                    string guard;
                    if (!seesSide) guard = "동료가 부르는 쪽으로 방패 들어 막기";
                    else if (bothArms) guard = $"방패(왼팔)를 {Side(side)}으로 들어 막기";
                    else if (caller) guard = $"{Side(side)}! 불러 줘라 (왼팔 방패가 막는다)";
                    else guard = $"방패를 {Side(side)}으로 들어 막기";
                    return kind == BossAttackKind.Feint ? "속지 말고 기다렸다가 " + guard : guard;
                case BossCounter.AlternateGuard:
                    if (!seesSide) return "동료가 부르는 쪽으로 번갈아 방패 들어 막기";
                    if (bothArms) return $"{Side(side)}부터 번갈아 방패(왼팔)로 막기";
                    return caller ? $"{Side(side)}부터 번갈아! 타마다 불러 줘라" : $"{Side(side)}부터 번갈아 방패로 막기";
                case BossCounter.BothGuard:
                    return bothArms ? "두 팔을 들어 X자로 막기" : "둘이 같이 팔을 들어 X자로 막기";
                case BossCounter.Dodge:
                    // 충격파 등: 아무 쪽이나 같이 피하면 된다. 돌진만 피할 쪽이 정해져 있다.
                    if (kind != BossAttackKind.Charge)
                        return (kind == BossAttackKind.Shockwave ? "퍼지기 직전에 " : string.Empty) + "두 스틱을 같은 쪽으로 밀어 피하기";
                    if (!seesSide) return "동료가 부르는 쪽으로 두 스틱을 같이 밀어 피하기";
                    if (caller) return $"{Side(side)}! 불러 주고 같이 스틱을 {Side(side)}으로";
                    return $"부딪히기 직전에 두 스틱을 {Side(side)}으로 밀어 피하기";
                default:
                    return string.Empty;
            }
        }

        /// <summary>연타 중 다음 타 안내. hit = 방금 들어온 타(0부터), nextSide = 다음 타가 들어오는 쪽. 다음이 없으면 빈 글자.</summary>
        public static string Next(int hit, int hits, PilotSide nextSide) => Next(hit, hits, nextSide, true);

        /// <summary>연타 중 다음 타 안내. 쪽을 못 보는 사람에게는 몇 번째 타인지만.</summary>
        public static string Next(int hit, int hits, PilotSide nextSide, bool seesSide)
        {
            var next = hit + 1;
            if (next >= Hits(hits)) return string.Empty;
            return seesSide ? $"{next + 1}/{Hits(hits)} → {Side(nextSide)}" : $"{next + 1}/{Hits(hits)}";
        }

        public static string Arm(PilotSide side) => side == PilotSide.Left ? "왼팔" : "오른팔";

        /// <summary>들어오는 쪽·피할 쪽(조종사 기준).</summary>
        public static string Side(PilotSide side) => side == PilotSide.Left ? "왼쪽" : "오른쪽";

        static int Hits(int hits) => hits < 1 ? 1 : hits;
    }
}
