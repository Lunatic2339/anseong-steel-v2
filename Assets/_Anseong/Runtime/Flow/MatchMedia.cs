using Anseong.Pilot;
using UnityEngine;
using UnityEngine.Video;

namespace Anseong.Flow
{
    /// <summary>
    /// M4-08: 장면에 맞춰 배경음악을 겹쳐 바꾸고 조종석 앞 화면에 영상을 튼다. 모든 PC가 각자 복제된 장면을 보고 같은 것을 낸다.
    /// 영상은 장면 시작 시각(서버 시계)에 맞춰 시작 위치를 잡아, 늦게 받은 PC도 같은 곳을 본다.
    /// 칸이 비면 조용히 넘어간다(무음, 화면 꺼짐). 메뉴 'Add Match Media'가 붙인다.
    /// </summary>
    public class MatchMedia : MonoBehaviour
    {
        [SerializeField, Tooltip("배경음악·영상 표(Audio/MatchMedia.asset).")]
        MatchMediaSet media;

        [SerializeField, Tooltip("판 흐름. 비어 있으면 시작할 때 한 번 찾는다.")]
        MatchFlow flow;

        [SerializeField, Tooltip("화면 재질(빛 안 받는 재질). 메뉴가 넣는다.")]
        Material screenMaterial;

        [Header("화면 (조종사 루트 기준 m, 기체 점검 철판과 같은 기준)")]
        [SerializeField, Tooltip("화면 가운데 위치.")] Vector3 screenOffset = new Vector3(0f, 33f, 45f);
        [SerializeField, Tooltip("화면 크기(가로·세로, 16:9).")] Vector2 screenSize = new Vector2(32f, 18f);

        AudioSource[] music;
        int activeMusic;
        float fadeT = 1f;
        MusicTrack playingTrack = MusicTrack.None;

        VideoPlayer player;
        Renderer screen;
        RenderTexture target;
        Transform pilotRoot;
        ScreenVideo playingVideo = ScreenVideo.None;
        MatchScene lastScene = MatchScene.Waiting;
        double lastStart = double.NaN;
        bool lastWon;

        void Start()
        {
            if (flow == null) flow = FindAnyObjectByType<MatchFlow>();
            var seat = FindAnyObjectByType<CockpitSeat>();
            if (seat != null) pilotRoot = seat.transform.root;
            music = new AudioSource[2];
            for (var i = 0; i < 2; i++)
            {
                var go = new GameObject("Music" + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                music[i] = source;
            }
            BuildScreen();
        }

        void BuildScreen()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Cockpit Screen";
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(screenSize.x, screenSize.y, 1f);
            screen = go.GetComponent<Renderer>();
            if (screenMaterial != null) screen.sharedMaterial = screenMaterial;
            target = new RenderTexture(1920, 1080, 0) { name = "Cockpit Screen RT" };
            screen.material.mainTexture = target;
            player = go.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            go.SetActive(false);
        }

        void OnDestroy()
        {
            if (target != null) target.Release();
        }

        void Update()
        {
            if (flow == null || !flow.Running) return;
            var scene = flow.Scene;
            var won = flow.Result.Won;
            if (scene != lastScene || !flow.SceneStart.Equals(lastStart) || won != lastWon)
            {
                lastScene = scene;
                lastStart = flow.SceneStart;
                lastWon = won;
                SetMusic(MatchMediaRules.MusicFor(scene, won));
                SetVideo(MatchMediaRules.VideoFor(scene));
            }
            FadeMusic();
            PlaceScreen();
        }

        void SetMusic(MusicTrack track)
        {
            if (track == MusicTrack.Keep || track == playingTrack) return;
            playingTrack = track;
            var clip = media != null ? media.Music(track) : null;
            activeMusic = 1 - activeMusic;
            var next = music[activeMusic];
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            if (clip != null) next.Play();
            fadeT = 0f;
        }

        void FadeMusic()
        {
            if (fadeT >= 1f || music == null) return;
            var seconds = media != null ? media.crossfadeSeconds : 0f;
            fadeT = seconds <= 0f ? 1f : Mathf.Min(1f, fadeT + Time.unscaledDeltaTime / seconds);
            var volume = media != null ? media.musicVolume : 0f;
            music[activeMusic].volume = volume * fadeT;
            var old = music[1 - activeMusic];
            old.volume = volume * (1f - fadeT);
            if (fadeT >= 1f) old.Stop();
        }

        void SetVideo(ScreenVideo video)
        {
            if (player == null) return;
            var clip = media != null ? media.Video(video) : null;
            if (clip == null)
            {
                playingVideo = ScreenVideo.None;
                player.Stop();
                player.gameObject.SetActive(false);
                return;
            }
            if (video == playingVideo && player.isPlaying) return;
            playingVideo = video;
            player.gameObject.SetActive(true);
            player.clip = clip;
            player.isLooping = MatchMediaRules.Loops(video);
            player.SetDirectAudioVolume(0, media.videoVolume);
            // 늦게 받은 PC도 같은 곳부터(반복 영상은 처음부터).
            var elapsed = player.isLooping ? 0d : flow.Now - flow.SceneStart;
            player.time = elapsed > 0d && elapsed < clip.length ? elapsed : 0d;
            player.Play();
        }

        void PlaceScreen()
        {
            if (screen == null || !screen.gameObject.activeSelf || pilotRoot == null) return;
            screen.transform.SetPositionAndRotation(pilotRoot.position + pilotRoot.rotation * screenOffset, pilotRoot.rotation);
        }

        /// <summary>판 흐름이 서버에서 영상 장면 길이를 정할 때 쓴다.</summary>
        public MatchMediaSet Media => media;
    }
}
