using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 동작 재생(M2-02, 모든 PC). 서버가 복제한 <see cref="BossAction"/>(상태 + 기술 + 시작 서버 시각)이 바뀌면
    /// 그 상태의 클립을 경과 시간만큼 앞당겨 CrossFade한다. 그래서 두 PC가 같은 순간의 자세를 본다.
    /// 상태 이름은 보스 Animator 컨트롤러와 같다(메뉴가 만든다): Idle, Stagger, Death, Attack_&lt;기술&gt;.
    /// 맞았을 때 짧은 휘청(<see cref="BossReactionDriver"/>)은 쉬는 중·빈틈에만 끼어들고, 끝나면 이 컴포넌트의 기본 자세로 돌아온다.
    /// </summary>
    public class BossAnimationDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("보스 두뇌(복제된 동작). 비어 있으면 같은 오브젝트에서 찾는다.")]
        BossServer server;

        [SerializeField, Tooltip("보스 Animator. 비어 있으면 자식에서 찾는다.")]
        Animator animator;

        [SerializeField, Min(0f), Tooltip("동작이 바뀔 때 섞기 시간(초). 길수록 묵직하게 넘어간다.")]
        float fadeSeconds = 0.25f;

        [SerializeField, Tooltip("대기 상태 이름.")]
        string idleState = "Idle";

        [SerializeField, Tooltip("경직 상태 이름(Stagger 클립).")]
        string staggerState = "Stagger";

        [SerializeField, Tooltip("쓰러짐 상태 이름(Death 클립, 없으면 대기 자세로 멈춤).")]
        string deathState = "Death";

        [SerializeField, Tooltip("걷기 상태 이름(M2-04).")]
        string walkState = "Walk";

        [SerializeField, Tooltip("포효 상태 이름(M2-04, 클립이 없으면 대기).")]
        string roarState = "Roar";

        [SerializeField, Tooltip("밀려남 상태 이름(M2-04, Boss_Knockback, 없으면 Stagger 전체).")]
        string knockbackState = "Knockback";

        [SerializeField, Tooltip("무릎 상태 이름(M2-04, Boss_Kneel, 없으면 대기 정지).")]
        string kneelState = "Kneel";

        const string LayerPrefix = "Base Layer.";
        BossAction shown;
        bool hasShown;

        /// <summary>지금 짧은 휘청이 끼어들어도 되는지(쉬는 중·빈틈).</summary>
        public bool AllowsReaction
        {
            get
            {
                if (server == null) return true;
                var state = server.Action.BossState;
                return state == BossState.Rest || state == BossState.Opening;
            }
        }

        void Start()
        {
            if (server == null) server = GetComponent<BossServer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (server == null || animator == null) enabled = false;
        }

        void Update()
        {
            var action = server.Action;
            if (hasShown && action.Equals(shown)) return;
            shown = action;
            hasShown = true;
            Play(action, fadeSeconds);
        }

        /// <summary>휘청이 끝나면 지금 동작의 기본 자세로 돌아간다.</summary>
        public void ReturnToLoop(float fade)
        {
            if (!enabled) return;
            Play(server.Action, fade);
        }

        void Play(BossAction action, float fade)
        {
            var name = StateFor(action);
            var hash = Animator.StringToHash(LayerPrefix + name);
            if (!animator.HasState(0, hash))
            {
                hash = Animator.StringToHash(LayerPrefix + idleState);
                if (!animator.HasState(0, hash)) return;
            }
            var elapsed = action.BossState == BossState.Rest || action.BossState == BossState.Opening ? 0f : Mathf.Max(0f, (float)(server.Now - action.Start));
            animator.CrossFadeInFixedTime(hash, fade, 0, elapsed);
        }

        string StateFor(BossAction action)
        {
            switch (action.BossState)
            {
                case BossState.Telegraph:
                case BossState.Strike:
                case BossState.Recovery:
                    return action.HasAttack ? BossAttackDefinition.StateName(action.AttackKind) : idleState;
                case BossState.Staggered:
                    return staggerState;
                case BossState.Defeated:
                    return deathState;
                case BossState.Walk:
                    return walkState;
                case BossState.Roar:
                    return roarState;
                case BossState.KnockedBack:
                    return knockbackState;
                case BossState.Kneel:
                    return kneelState;
                default:
                    return idleState;
            }
        }
    }
}
