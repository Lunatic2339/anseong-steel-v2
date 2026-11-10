using Anseong.Pilot;

namespace Anseong.Flow
{
    /// <summary>기체 점검 단계(10_game_scenario.md 장면 1). 숫자가 복제되므로 끝에만 더한다.</summary>
    public enum CheckupStep : byte
    {
        /// <summary>1-1 신경 연결: 보정(뻗기 → 가드).</summary>
        Connect,
        /// <summary>1-2 관절 시험(맨손): 번갈아 세 번 + 트리거 → 철판이 날아감.</summary>
        JointTest,
        /// <summary>1-3 검·방패 장착 + 공 막기 연습(오는 쪽은 오른쪽 HUD에만).</summary>
        ShieldDrill,
        /// <summary>1-4 스캐너·포 장착(설명 한 줄).</summary>
        GunBrief,
        /// <summary>1-5 출격: 둘이 같이 스틱 앞으로.</summary>
        Sortie,
    }

    /// <summary>관절 시험 "좌우 번갈아 세 번" 세기(M4-05). 같은 팔이 연달아 치면 1부터. 순수 클래스.</summary>
    public sealed class AlternationCounter
    {
        PilotSide? last;
        public int Count { get; private set; }
        public bool Done(int needed) => Count >= needed;

        public void Reset()
        {
            last = null;
            Count = 0;
        }

        public int Strike(PilotSide side)
        {
            Count = last.HasValue && last.Value != side ? Count + 1 : 1;
            last = side;
            return Count;
        }
    }
}
