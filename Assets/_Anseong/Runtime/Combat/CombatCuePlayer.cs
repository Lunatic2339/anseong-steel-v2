using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;
using UnityEngine.XR;

namespace Anseong.Combat
{
    /// <summary>
    /// 전투 이벤트를 듣고 <see cref="CombatCueSet"/> 표대로 소리와 컨트롤러 진동을 낸다. 표가 비어 있거나 소리 칸이 비면 조용히 넘어간다.
    /// 외골격 명령(H-01 뒤)도 같은 방식으로 이벤트를 듣는 컴포넌트를 하나 더 붙이면 된다.
    /// </summary>
    public class CombatCuePlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField, Tooltip("이벤트별 소리·진동 표(Audio/CombatCues.asset). 메뉴가 만든다.")]
        CombatCueSet cues;

        [SerializeField, Range(1, 16), Tooltip("동시에 낼 수 있는 소리 개수.")]
        int voices = 8;

        [SerializeField, Min(0f), Tooltip("3D 소리(보스 쪽)가 줄어들기 시작하는 거리(m). 로봇 크기라 크게 잡는다.")]
        float minDistance = 30f;

        [SerializeField, Min(1f), Tooltip("3D 소리가 들리지 않게 되는 거리(m).")]
        float maxDistance = 600f;

        [Header("사령관 대사 (M4-08)")]
        [SerializeField, Range(0f, 1f), Tooltip("사령관 대사 크기(표의 줄 크기에 곱함). 배경음악보다 크게.")]
        float commanderVolume = 1f;

        [SerializeField, Tooltip("무전 효과(낮은음·높은음 깎고 살짝 찌그러뜨림). 사령관 목소리가 무전기에서 나오는 것처럼.")]
        bool radioEffect = true;

        [SerializeField, Range(0f, 0.9f), Tooltip("무전 찌그러짐 정도.")]
        float radioDistortion = 0.35f;

        [SerializeField, Tooltip("컨트롤러 진동을 쓴다.")]
        bool haptics = true;

        [SerializeField, Range(0f, 0.3f), Tooltip("같은 손 진동 사이 최소 간격(초, 01: 0.09). 이보다 빨리 오면 더 중요한 진동만 끼어든다.")]
        float minHapticGap = 0.09f;

        AudioSource[] sources;
        int nextVoice;
        readonly float[] hapticUntil = new float[2];
        readonly float[] hapticAmplitude = new float[2];
        readonly float[] hapticStart = { -1f, -1f };
        readonly int[] hapticPriority = new int[2];
        readonly int[] clipCursor = new int[128];

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (director == null)
            {
                enabled = false;
                return;
            }
            sources = new AudioSource[voices];
            for (var i = 0; i < voices; i++)
            {
                var go = new GameObject("CueVoice" + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                sources[i] = source;
            }
            BuildCommanderVoice();
            director.Cue += OnCue;
        }

        AudioSource commanderVoice;

        /// <summary>사령관 대사 전용 소리: 겹치지 않게 하나, 무전 효과 필터.</summary>
        void BuildCommanderVoice()
        {
            var go = new GameObject("CommanderVoice");
            go.transform.SetParent(transform, false);
            commanderVoice = go.AddComponent<AudioSource>();
            commanderVoice.playOnAwake = false;
            commanderVoice.spatialBlend = 0f;
            commanderVoice.priority = 0;
            var high = go.AddComponent<AudioHighPassFilter>();
            high.cutoffFrequency = 350f;
            var low = go.AddComponent<AudioLowPassFilter>();
            low.cutoffFrequency = 3800f;
            var distortion = go.AddComponent<AudioDistortionFilter>();
            distortion.distortionLevel = radioDistortion;
            high.enabled = low.enabled = distortion.enabled = radioEffect;
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            if (cues == null) return;
            var entries = cues.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.Matches(e)) continue;
                PlaySound(i, entry, e);
                if (!haptics || entry.hapticAmplitude <= 0f || entry.hapticSeconds <= 0f) continue;
                var priority = Priority(e.Cue);
                // 피격·필살기는 몸 전체로 느끼게 두 손 모두.
                if (e.Cue == CombatCue.PilotHit || e.Cue == CombatCue.FinisherCharge || e.Cue == CombatCue.FinisherFire)
                {
                    Rumble(PilotSide.Left, entry.hapticAmplitude, entry.hapticSeconds, priority);
                    Rumble(PilotSide.Right, entry.hapticAmplitude, entry.hapticSeconds, priority);
                }
                else Rumble(e.Side, entry.hapticAmplitude, entry.hapticSeconds, priority);
            }
        }

        void PlaySound(int index, CombatCueSet.Entry entry, CombatEvent e)
        {
            if (entry.clips == null || entry.clips.Length == 0) return;
            var slot = index % clipCursor.Length;
            var clip = entry.clips[clipCursor[slot]++ % entry.clips.Length];
            if (clip == null) return;
            if (entry.cue == CombatCue.CommanderLine && commanderVoice != null)
            {
                // 새 대사가 오면 앞 대사를 끊는다(무전은 한 번에 한 줄).
                commanderVoice.Stop();
                commanderVoice.clip = clip;
                commanderVoice.volume = entry.volume * commanderVolume;
                commanderVoice.pitch = 1f;
                commanderVoice.Play();
                return;
            }
            var source = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;
            source.spatialBlend = entry.atHitPoint ? 1f : 0f;
            source.transform.position = entry.atHitPoint ? e.Point : transform.position;
            source.pitch = Mathf.Max(0.1f, entry.pitch + entry.pitchPerComboStep * Mathf.Max(0, e.ComboStep - 1)
                + Random.Range(-entry.pitchJitter, entry.pitchJitter));
            source.PlayOneShot(clip, entry.volume);
        }

        /// <summary>진동 우선순위(01: 피격 &gt; 부스트 &gt; 연계 성공 &gt; 명중 &gt; 가드).</summary>
        static int Priority(CombatCue cue)
        {
            switch (cue)
            {
                case CombatCue.PilotHit: return 5;
                case CombatCue.FinisherFire: return 5;
                case CombatCue.Boost: return 4;
                case CombatCue.ComboStep: return 3;
                case CombatCue.Hit: return 2;
                case CombatCue.GuardBlock: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// 더 중요한 진동이 울리는 중이면 덜 중요한 것은 건너뛴다. 최소 간격 안에 온 것은 더 중요할 때만 끼어든다.
        /// 우선순위가 같으면 센 쪽이 이긴다.
        /// </summary>
        void Rumble(PilotSide side, float amplitude, float seconds, int priority)
        {
            var h = (int)side;
            var now = Time.unscaledTime;
            var playing = now < hapticUntil[h];
            if (playing && (priority < hapticPriority[h] || (priority == hapticPriority[h] && amplitude < hapticAmplitude[h]))) return;
            if (now - hapticStart[h] < minHapticGap && priority <= hapticPriority[h]) return;
            var device = InputDevices.GetDeviceAtXRNode(side == PilotSide.Left ? XRNode.LeftHand : XRNode.RightHand);
            if (!device.isValid || !device.SendHapticImpulse(0u, amplitude, seconds)) return;
            hapticUntil[h] = now + seconds;
            hapticAmplitude[h] = amplitude;
            hapticStart[h] = now;
            hapticPriority[h] = priority;
        }
    }
}
