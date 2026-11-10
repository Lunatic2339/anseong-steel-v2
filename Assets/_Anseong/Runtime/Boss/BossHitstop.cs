using Anseong.Combat;
using Anseong.Core;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 히트스톱(M5-01, 모든 PC, 겉모습만): 큰 타격이 들어가면 <b>보스 Animator만</b> 잠깐 거의 멈춘다. 화면·카메라·우리 팔은 절대 늦추지 않는다(멀미).
    /// 서버 판정·시간에는 손대지 않는다. 메뉴 'Add Match Flow'가 보스에 붙인다. B 메뉴 "연출"에서 끄고 켠다.
    /// </summary>
    public class BossHitstop : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("보스 Animator. 비면 시작할 때 자식에서 찾는다.")] Animator animator;
        [SerializeField, Tooltip("끄면 히트스톱 없음.")] bool on = true;
        [SerializeField, Range(0f, 0.3f), Tooltip("훅·어퍼컷 명중 때 멈춤(초).")] float hookSeconds = 0.08f;
        [SerializeField, Range(0f, 0.5f), Tooltip("부스트 명중 때 멈춤(초).")] float boostSeconds = 0.15f;
        [SerializeField, Range(0f, 0.5f), Tooltip("보스 무기가 부서질 때(페이즈 전환) 멈춤(초).")] float breakSeconds = 0.25f;
        [SerializeField, Range(0f, 0.5f), Tooltip("멈춤 동안 보스 애니 속도(0 = 완전 정지).")] float frozenSpeed = 0.05f;

        CombatDirector director;
        float until;
        bool frozen;

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            director = FindAnyObjectByType<CombatDirector>();
            if (director != null) director.Cue += OnCue;
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            if (!on || animator == null) return;
            switch (e.Cue)
            {
                case CombatCue.Hit:
                    if (e.Kind == PunchKind.Hook || e.Kind == PunchKind.Uppercut) Freeze(hookSeconds);
                    break;
                case CombatCue.Boost: Freeze(boostSeconds); break;
                case CombatCue.PhaseChanged: Freeze(breakSeconds); break;
            }
        }

        void Freeze(float seconds)
        {
            if (seconds <= 0f) return;
            until = Mathf.Max(until, Time.unscaledTime + seconds);
            frozen = true;
            animator.speed = frozenSpeed;
        }

        void Update()
        {
            if (!frozen || Time.unscaledTime < until) return;
            frozen = false;
            if (animator != null) animator.speed = 1f;
        }

        public void AddTuning(TuningList list)
        {
            list.AddToggle("연출", "fx.hitstop", "히트스톱(보스만 잠깐 멈춤)", () => on, v => on = v);
        }
    }
}
