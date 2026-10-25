using Anseong.Combat;
using Anseong.Core;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스가 맞았을 때 애니메이션(05 "보스가 맞았을 때", M1-05): 잽 = 효과만, 훅·어퍼컷 = 휘청 앞부분, 부스트 = 휘청 전체.
    /// 전투 이벤트만 듣는다. 클립은 보스 Animator 컨트롤러의 상태 이름으로 고르고, 메뉴가 그 이름으로 컨트롤러를 만든다.
    /// 쓰러짐·밀려남·보스 공격은 M2.
    /// </summary>
    public class BossReactionDriver : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField, Tooltip("보스 Animator. 메뉴가 연결한다.")]
        Animator animator;

        [SerializeField, Tooltip("대기 상태 이름(보스 컨트롤러).")]
        string idleState = "Idle";

        [SerializeField, Tooltip("휘청 상태 이름(보스 컨트롤러, Stagger 클립).")]
        string staggerState = "Stagger";

        [SerializeField, Min(0f), Tooltip("휘청으로 넘어가는 섞기 시간(초).")]
        float fadeInSeconds = 0.08f;

        [SerializeField, Min(0f), Tooltip("대기로 돌아오는 섞기 시간(초). 길수록 묵직하게 돌아온다.")]
        float fadeOutSeconds = 0.35f;

        [SerializeField, Min(0f), Tooltip("훅·어퍼컷 명중: 휘청 앞부분만 이만큼(초) 틀고 대기로.")]
        float hitStaggerSeconds = 0.6f;

        [SerializeField, Min(0f), Tooltip("부스트 명중: 휘청 전체 길이(초). 메뉴가 클립 길이를 넣는다.")]
        float boostStaggerSeconds = 2f;

        [SerializeField, Tooltip("잽도 짧게 휘청(기본 끔: 잽은 효과만).")]
        bool staggerOnJab;

        const string LayerPrefix = "Base Layer.";
        int idleHash, staggerHash;
        float returnAt = -1f;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (director == null || animator == null)
            {
                enabled = false;
                return;
            }
            // 컨트롤러 첫 레이어(Base Layer) 안의 상태. 전체 경로 해시는 HasState·CrossFade 둘 다 받는다.
            idleHash = Animator.StringToHash(LayerPrefix + idleState);
            staggerHash = Animator.StringToHash(LayerPrefix + staggerState);
            if (!animator.HasState(0, staggerHash))
                Debug.LogWarning($"[BossReactionDriver] 보스 Animator에 '{staggerState}' 상태가 없어 휘청하지 않습니다. 'Anseong → Setup → Rebuild Robot + Pilot Input'을 다시 실행하세요.", this);
            director.Cue += OnCue;
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.Hit:
                    if (e.Kind == PunchKind.Hook || e.Kind == PunchKind.Uppercut || (staggerOnJab && e.Kind == PunchKind.Jab))
                        Stagger(e.Kind == PunchKind.Jab ? hitStaggerSeconds * 0.5f : hitStaggerSeconds);
                    break;
                case CombatCue.Boost:
                    Stagger(boostStaggerSeconds);
                    break;
            }
        }

        void Stagger(float seconds)
        {
            if (!animator.HasState(0, staggerHash)) return;
            animator.CrossFadeInFixedTime(staggerHash, fadeInSeconds, 0, 0f);
            returnAt = Time.time + seconds;
        }

        void Update()
        {
            if (returnAt < 0f || Time.time < returnAt) return;
            returnAt = -1f;
            if (animator.HasState(0, idleHash)) animator.CrossFadeInFixedTime(idleHash, fadeOutSeconds, 0);
        }

        /// <summary>인게임 조절 메뉴: 보스가 맞고 휘청하는 느낌(묵직함).</summary>
        public void AddTuning(TuningList list)
        {
            const string g = "보스 반응";
            list.Describe(g, "할 것: 잽·훅·어퍼컷·부스트로 보스 치기\n볼 것: 거대한 보스가 무게 있게 휘청하고 돌아옴");
            list.AddCheck(g, "boss.stagger", "휘청이 묵직함");
            list.AddCheck(g, "boss.boostStagger", "부스트 휘청이 더 큼");
            list.AddFloat(g, "boss.fadeOut", "대기로 돌아오는 섞기(초)", () => fadeOutSeconds, v => fadeOutSeconds = v, 0f, 1.5f, 0.05f);
            list.AddFloat(g, "boss.hitStagger", "훅·어퍼컷 휘청 길이(초)", () => hitStaggerSeconds, v => hitStaggerSeconds = v, 0f, 2f, 0.05f);
        }
    }
}
