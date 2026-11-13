namespace Anseong.Flow
{
    /// <summary>배경음악 곡(13_ai_tools.md 이름표: BGM_Opening·Battle·Bare·Ending).</summary>
    public enum MusicTrack : byte
    {
        /// <summary>음악 없음(대기 화면 영상 소리만).</summary>
        None,
        Opening,
        Battle,
        Bare,
        Ending,
        /// <summary>지금 곡을 그대로 둔다(재기동 등).</summary>
        Keep,
    }

    /// <summary>조종석 앞 화면 영상(Video_Lobby·Opening·Ending).</summary>
    public enum ScreenVideo : byte
    {
        None,
        Lobby,
        Opening,
        Ending,
    }

    /// <summary>
    /// M4-08: 장면 → 배경음악·영상. 순수 함수. 모든 PC가 복제된 장면으로 같은 답을 낸다.
    /// 기체 점검은 오프닝 곡, 무기를 든 싸움은 전투 곡, 맨손(장면 8~9)은 맨손 곡, 승리 엔딩·결과판은 엔딩 곡, 패배 결과판은 무음.
    /// </summary>
    public static class MatchMediaRules
    {
        public static MusicTrack MusicFor(MatchScene scene, bool won)
        {
            switch (scene)
            {
                case MatchScene.Waiting:
                case MatchScene.Opening:
                    return MusicTrack.None;
                case MatchScene.Checkup:
                    return MusicTrack.Opening;
                case MatchScene.Entrance:
                case MatchScene.DroneHunt:
                case MatchScene.Approach:
                case MatchScene.FightCannon:
                case MatchScene.LostGun:
                case MatchScene.FightArmor:
                case MatchScene.Clash:
                case MatchScene.LostSword:
                    return MusicTrack.Battle;
                case MatchScene.FightCore:
                case MatchScene.Finisher:
                    return MusicTrack.Bare;
                case MatchScene.Ending:
                    return MusicTrack.None;
                case MatchScene.Result:
                    return won ? MusicTrack.Ending : MusicTrack.None;
                default:
                    return MusicTrack.Keep;
            }
        }

        public static ScreenVideo VideoFor(MatchScene scene)
        {
            switch (scene)
            {
                case MatchScene.Waiting: return ScreenVideo.Lobby;
                case MatchScene.Opening: return ScreenVideo.Opening;
                case MatchScene.Ending: return ScreenVideo.Ending;
                default: return ScreenVideo.None;
            }
        }

        /// <summary>영상이 끝나면 처음부터 다시(대기 화면만).</summary>
        public static bool Loops(ScreenVideo video) => video == ScreenVideo.Lobby;
    }
}
