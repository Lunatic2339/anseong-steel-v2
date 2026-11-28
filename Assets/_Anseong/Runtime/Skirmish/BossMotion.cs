using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 보스의 온몸 동작(받은 사람형 동작 파일)을 싸움 상태에 맞춰 튼다. 규칙에는 관여하지 않는다.
    /// 동작을 그냥 재생하지 않고, 지금 단계와 남은 시간에 맞는 지점으로 직접 옮긴다(예고가 끝나는 순간과 칼이 닿는 순간이 규칙과 어긋나지 않게).
    /// 위치는 규칙이 정하므로 동작의 앞뒤 이동은 쓰지 않는다(제자리에서 걷는다).
    /// 대검은 오른손에 붙인다. 동작 파일이 없는 공격(속임 베기, 헬기를 노림, 자폭 떼)은 몸만 서 있기를 틀고 팔과 대검은 <see cref="BossPuppet"/>이 잡는다.
    /// 동작 파일이 없으면(장면을 만든 PC에 Art/BossMecha/Motions가 없었으면) 아무것도 하지 않고 예전처럼 BossPuppet만 쓴다.
    /// </summary>
    public class BossMotion : MonoBehaviour
    {
        [Serializable]
        public class Move
        {
            public AnimationClip clip;
            [Range(0f, 1f), Tooltip("예고 자세가 다 잡힌 지점(동작 길이에 대한 비율). 예고가 끝날 때까지 여기서 멈춘다.")] public float ready = 0.3f;
            [Range(0f, 1f), Tooltip("칼이 닿는 지점. 내려치는 구간이 끝나는 순간에 여기에 온다.")] public float hit = 0.5f;
            public bool Has => clip != null;
        }

        [SerializeField] Animator animator;
        [SerializeField, Tooltip("대검을 쥔 손.")] Transform hand;
        [SerializeField, Tooltip("손 기준으로 칼날이 뻗는 방향과 날이 서는 방향.")] Vector3 bladeAxis = Vector3.forward, edgeAxis = Vector3.up;
        [SerializeField] Transform sword;
        [SerializeField, Min(0f)] float swordLength = 20f;
        [SerializeField, Min(0.01f), Tooltip("동작이 바뀔 때 섞이는 시간(초).")] float blend = 0.15f;

        [Header("동작")]
        public AnimationClip idle;
        public AnimationClip walk, walkBack, stagger, death;
        public Move slam = new Move(), swing = new Move(), bigSlam = new Move(), thrust = new Move();
        [Range(0f, 1f), Tooltip("튕겨난 동안 멈춰 있는 지점(비틀거림 동작 길이에 대한 비율).")] public float staggerHold = 0.4f;
        [Range(0f, 1f), Tooltip("마무리의 일격을 맞고 넘어가기 시작해 멈춰 있는 지점(쓰러짐 동작 길이에 대한 비율).")] public float blowHold = 0.3f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly List<AnimationClip> clips = new List<AnimationClip>();
        readonly List<AnimationClipPlayable> players = new List<AnimationClipPlayable>();
        float[] weights, times;
        int current = -1;
        float loopTime, deathTime, staggerTime, recoverTime;
        GruntStep lastStep;
        FinaleStep lastFinale;

        /// <summary>동작 파일이 꽂혀 있어 온몸 동작을 틀 수 있다.</summary>
        public bool Ready => animator != null && idle != null;
        /// <summary>지금 팔과 대검까지 동작이 잡고 있다. false면 BossPuppet이 팔과 대검을 잡는다.</summary>
        public bool DrivesArms { get; private set; }

        public void Bind(Animator bossAnimator, Transform swordHand, Vector3 blade, Vector3 edge, Transform swordRoot, float length)
        {
            animator = bossAnimator; hand = swordHand; bladeAxis = blade; edgeAxis = edge; sword = swordRoot; swordLength = length;
        }

        Move For(GruntAttack attack)
        {
            switch (attack)
            {
                case GruntAttack.Slam: return slam;
                case GruntAttack.Swing: return swing;
                case GruntAttack.BigSlam: return bigSlam.Has ? bigSlam : slam;
                case GruntAttack.Thrust: return thrust;
                default: return null;
            }
        }

        void Build()
        {
            if (graph.IsValid()) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clips.Clear(); players.Clear();
            foreach (var clip in new[] { idle, walk, walkBack, stagger, death, slam.clip, swing.clip, bigSlam.clip, thrust.clip })
                if (clip != null && !clips.Contains(clip)) clips.Add(clip);
            graph = PlayableGraph.Create("Boss motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, clips.Count);
            for (int i = 0; i < clips.Count; i++)
            {
                var player = AnimationClipPlayable.Create(graph, clips[i]);
                // 시간은 매번 직접 정한다.
                player.SetSpeed(0);
                player.SetApplyFootIK(false);
                graph.Connect(player, 0, mixer, i);
                players.Add(player);
            }
            var output = AnimationPlayableOutput.Create(graph, "Boss", animator);
            output.SetSourcePlayable(mixer);
            weights = new float[clips.Count];
            times = new float[clips.Count];
            if (Application.isPlaying) graph.Play();
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        void Set(AnimationClip clip, float seconds, bool loop)
        {
            int i = clip != null ? clips.IndexOf(clip) : -1;
            if (i < 0) i = clips.IndexOf(idle);
            current = i;
            float length = Mathf.Max(0.01f, clips[i].length);
            times[i] = loop ? Mathf.Repeat(seconds, length) : Mathf.Clamp(seconds, 0f, length - 0.001f);
        }

        /// <summary>이번 프레임의 싸움 상태를 동작으로 옮긴다. 화면을 그리는 쪽이 매번 부른다.</summary>
        public void Show(SkirmishState st, SkirmishSettings s, float dt)
        {
            if (!Ready) return;
            Build();
            var step = st.gruntStep;
            bool dead = step == GruntStep.Dead;
            bool finale = !dead && st.finale != FinaleStep.None;
            loopTime += dt;
            if (step != lastStep) { if (step == GruntStep.Stagger) staggerTime = 0f; if (step == GruntStep.Recover) recoverTime = 0f; }
            if (!dead && st.finale != FinaleStep.Blow) deathTime = 0f;
            DrivesArms = true;

            var move = For(st.gruntAttack);
            if (dead)
            {
                deathTime += dt;
                Set(death, deathTime, false);
            }
            else if (finale)
            {
                // 마무리: 대검을 들고(Raise, Catch), 받혀 내려온 채 멈추고(Open), 가슴이 터져 비틀거리고(Together), 일격에 넘어가기 시작한다(Blow).
                var m = bigSlam.Has ? bigSlam : slam;
                switch (st.finale)
                {
                    case FinaleStep.Raise:
                    case FinaleStep.Catch:
                        Toward(m, m.ready, dt, Mathf.Max(0.1f, s.finaleRaiseSeconds));
                        break;
                    case FinaleStep.Open:
                        Toward(m, m.hit, dt, 0.25f);
                        break;
                    case FinaleStep.Together:
                        staggerTime = st.finale != lastFinale ? 0f : staggerTime + dt;
                        Set(stagger, Mathf.Min(staggerTime, (stagger != null ? stagger.length : 1f) * staggerHold), false);
                        break;
                    default:
                        // 느린 시간이라 천천히 넘어가다가 멈춰 있고, 쓰러지는 순간부터 끝까지 간다.
                        deathTime = Mathf.Min(deathTime + dt * 0.4f, (death != null ? death.length : 1f) * blowHold);
                        Set(death, deathTime, false);
                        break;
                }
            }
            else switch (step)
            {
                case GruntStep.Approach:
                    Set(walk, loopTime, true);
                    break;
                case GruntStep.Regroup:
                    Set(walkBack != null ? walkBack : walk, loopTime, true);
                    break;
                case GruntStep.Rise:
                    if (move == null || !move.Has) { Arms(); break; }
                    Toward(move, move.ready, dt, Mathf.Max(0.05f, s.grunt.riseSeconds));
                    break;
                case GruntStep.Hold:
                    if (move == null || !move.Has) { Arms(); break; }
                    Set(move.clip, move.clip.length * move.ready, false);
                    break;
                case GruntStep.Strike:
                {
                    if (move == null || !move.Has) { Arms(); break; }
                    // 내려치는 구간은 남은 시간으로 직접 잇는다(끝나는 순간이 막기 판정이다).
                    float k = 1f - Mathf.Clamp01(st.gruntTimer / Mathf.Max(0.01f, s.grunt.strikeSeconds));
                    Set(move.clip, move.clip.length * Mathf.Lerp(move.ready, move.hit, k * k), false);
                    break;
                }
                case GruntStep.Recover:
                    if (move == null || !move.Has) { Arms(); break; }
                    recoverTime += dt;
                    Set(move.clip, move.clip.length * move.hit + recoverTime, false);
                    break;
                case GruntStep.Stagger:
                    staggerTime += dt;
                    Set(stagger, Mathf.Min(staggerTime, (stagger != null ? stagger.length : 1f) * staggerHold), false);
                    break;
                default:
                    Set(idle, loopTime, true);
                    break;
            }
            lastStep = step;
            lastFinale = st.finale;

            // 지금 동작으로 부드럽게 넘어간다.
            float rate = Application.isPlaying ? dt / blend : 1f;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == current ? 1f : 0f, rate);
                total += weights[i];
            }
            for (int i = 0; i < weights.Length; i++)
            {
                mixer.SetInputWeight(i, total > 0f ? weights[i] / total : (i == current ? 1f : 0f));
                players[i].SetTime(times[i]);
            }
            if (!Application.isPlaying)
            {
                graph.Evaluate(0f);
                PlaceSword();
            }
        }

        /// <summary>이 공격은 온몸 동작이 없다: 몸은 서 있기, 팔과 대검은 BossPuppet.</summary>
        void Arms()
        {
            Set(idle, loopTime, true);
            DrivesArms = false;
        }

        /// <summary>그 동작의 지금 지점에서 target(비율)까지 seconds에 걸쳐 간다. 다른 동작에서 넘어왔으면 처음부터 간다.</summary>
        void Toward(Move move, float target, float dt, float seconds)
        {
            int i = clips.IndexOf(move.clip);
            float length = move.clip.length;
            float now = current == i ? times[i] : 0f;
            Set(move.clip, Mathf.MoveTowards(now, length * target, length * Mathf.Max(target, 0.05f) / seconds * dt), false);
        }

        void LateUpdate() => PlaceSword();

        void PlaceSword()
        {
            if (!Ready || !DrivesArms || sword == null || hand == null) return;
            var along = hand.TransformDirection(bladeAxis).normalized;
            var edge = hand.TransformDirection(edgeAxis);
            sword.SetPositionAndRotation(hand.position + along * (swordLength * 0.5f), Quaternion.LookRotation(along, edge));
        }
    }
}
