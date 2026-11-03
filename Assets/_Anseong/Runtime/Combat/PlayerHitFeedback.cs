using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>
    /// 조종사 로봇 쪽 임시 반응(M2-03): 회피 시작 때 로봇 루트를 옆으로 미끄러뜨리고(약 0.4초, 협동 이동 전체는 M3),
    /// 맞았을 때 조종석을 짧게 흔든다(카메라는 거의 안 흔들리게 작게). 전투 이벤트만 듣는다(모든 PC가 같은 이벤트를 받는다).
    /// 플레이어 로봇 루트에 메뉴가 붙인다.
    /// </summary>
    public class PlayerHitFeedback : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;
        [SerializeField, Tooltip("조종석(흔들림). 비어 있으면 자식에서 찾는다.")]
        CockpitSeat seat;

        [Header("회피 미끄러짐")]
        [SerializeField, Tooltip("회피 때 로봇을 옆으로 옮긴다.")]
        bool slide = true;
        [SerializeField, Min(0f), Tooltip("옆으로 가는 거리(m, 로봇 크기 기준).")]
        float slideMeters = 10f;
        [SerializeField, Min(0.05f), Tooltip("미끄러지는 시간(초, 01: 약 0.4).")]
        float slideSeconds = 0.4f;
        [SerializeField, Min(0f), Tooltip("다음 회피까지 쉬는 시간(초).")]
        float slideCooldown = 0.8f;

        [Header("피격 흔들림")]
        [SerializeField, Tooltip("맞으면 조종석을 흔든다.")]
        bool shake = true;
        [SerializeField, Range(0f, 0.1f), Tooltip("흔들림 크기(m, 사람 크기). 멀미 때문에 작게.")]
        float shakeMeters = 0.015f;
        [SerializeField, Range(0.05f, 1f), Tooltip("흔들림 시간(초).")]
        float shakeSeconds = 0.3f;

        Vector3 slideFrom, slideTo;
        float slideStart = -1f, nextSlide, shakeStart = -1f, shakeScale;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (seat == null) seat = GetComponentInChildren<CockpitSeat>(true);
            if (director == null)
            {
                enabled = false;
                return;
            }
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
                case CombatCue.DodgeStart:
                    if (!slide || Time.time < nextSlide) return;
                    slideFrom = transform.position;
                    slideTo = slideFrom + transform.right * (e.Amount < 0f ? -slideMeters : slideMeters);
                    slideStart = Time.time;
                    nextSlide = Time.time + slideSeconds + slideCooldown;
                    break;
                case CombatCue.PilotHit:
                    if (!shake) return;
                    shakeStart = Time.time;
                    // 큰 피해일수록 조금 더(최대 2배).
                    shakeScale = Mathf.Clamp(e.Damage / 6f, 0.5f, 2f);
                    break;
            }
        }

        void Update()
        {
            if (slideStart >= 0f)
            {
                var t = Mathf.Clamp01((Time.time - slideStart) / slideSeconds);
                // 묵직하게: 천천히 출발해 천천히 멈춘다.
                transform.position = Vector3.Lerp(slideFrom, slideTo, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f) slideStart = -1f;
            }
            if (shakeStart >= 0f && seat != null)
            {
                var t = (Time.time - shakeStart) / shakeSeconds;
                if (t >= 1f)
                {
                    shakeStart = -1f;
                    seat.AdditionalOffset = Vector3.zero;
                    return;
                }
                var fade = 1f - t;
                var a = shakeMeters * shakeScale * fade;
                seat.AdditionalOffset = new Vector3(Mathf.Sin(t * 47f) * a, Mathf.Sin(t * 31f + 1f) * a * 0.6f, Mathf.Sin(t * 23f + 2f) * a * 0.4f);
            }
        }
    }
}
