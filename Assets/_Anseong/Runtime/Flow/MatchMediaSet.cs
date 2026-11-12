using UnityEngine;
using UnityEngine.Video;

namespace Anseong.Flow
{
    /// <summary>
    /// M4-08: 배경음악·영상 칸(13_ai_tools.md 3번 이름표). 비어 있는 칸은 무음·화면 꺼짐·장면 건너뛰기.
    /// 메뉴 'Anseong/Setup/Add Match Media'가 Audio/MatchMedia.asset을 만들고 이름이 맞는 파일을 찾아 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "Anseong/Match Media Set", fileName = "MatchMedia")]
    public class MatchMediaSet : ScriptableObject
    {
        [Header("배경음악 (BGM_*)")]
        [Tooltip("기체 점검 곡(BGM_Opening).")] public AudioClip opening;
        [Tooltip("무기 든 싸움 곡(BGM_Battle).")] public AudioClip battle;
        [Tooltip("맨손 곡(BGM_Bare).")] public AudioClip bare;
        [Tooltip("승리 결과판 곡(BGM_Ending).")] public AudioClip ending;
        [Range(0f, 1f), Tooltip("배경음악 크기. 사령관 대사·효과음보다 낮게.")] public float musicVolume = 0.35f;
        [Range(0f, 5f), Tooltip("곡 바뀔 때 겹쳐 넘어가는 시간(초).")] public float crossfadeSeconds = 2f;

        [Header("조종석 앞 화면 영상 (Video_*)")]
        [Tooltip("대기 중 반복(Video_Lobby).")] public VideoClip lobby;
        [Tooltip("판 시작 오프닝(Video_Opening). 비면 오프닝 장면 없음.")] public VideoClip openingVideo;
        [Tooltip("승리 엔딩(Video_Ending). 비면 바로 결과판.")] public VideoClip endingVideo;
        [Range(0f, 1f), Tooltip("영상 소리 크기(영상에 소리가 있을 때).")] public float videoVolume = 0.6f;

        public AudioClip Music(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Opening: return opening;
                case MusicTrack.Battle: return battle;
                case MusicTrack.Bare: return bare;
                case MusicTrack.Ending: return ending;
                default: return null;
            }
        }

        public VideoClip Video(ScreenVideo video)
        {
            switch (video)
            {
                case ScreenVideo.Lobby: return lobby;
                case ScreenVideo.Opening: return openingVideo;
                case ScreenVideo.Ending: return endingVideo;
                default: return null;
            }
        }

        /// <summary>영상 길이(초). 비어 있으면 0 = 그 장면 건너뜀.</summary>
        public static float Seconds(VideoClip clip) => clip != null ? (float)clip.length : 0f;
    }
}
