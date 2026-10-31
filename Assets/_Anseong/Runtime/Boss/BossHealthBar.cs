using Anseong.Combat;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 위 월드 공간 HP 막대(M1-05 임시, HUD 아트는 M2 이후). 사각형 두 개: 바탕 + 채움. 채움은 왼쪽 끝을 기준으로 줄어든다.
    /// 메뉴가 보스 머리 위에 만든다. 막대는 시점 쪽으로 좌우만 돈다.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField, Tooltip("채움 사각형(Quad). 메뉴가 만든다.")]
        Transform fill;

        [SerializeField, Min(0f), Tooltip("HP가 줄어드는 것을 따라가는 시간(초). 0이면 바로.")]
        float smoothSeconds = 0.25f;

        [SerializeField, Tooltip("시점 쪽으로 돌린다.")]
        bool faceViewer = true;

        Transform viewer;
        float shown = 1f, target = 1f, fullWidth;
        Vector3 fillBase;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (director == null || fill == null)
            {
                enabled = false;
                return;
            }
            var main = Camera.main;
            viewer = main != null ? main.transform : null;
            fullWidth = fill.localScale.x;
            fillBase = fill.localPosition;
            target = shown = director.State.BossFraction;
            Apply();
            director.Cue += OnCue;
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            target = director.State.BossFraction;
        }

        void LateUpdate()
        {
            // 명중 신호가 없이 바뀐 HP(다시 채우기)도 따라가게 매 프레임 읽는다.
            target = director.State.BossFraction;
            if (!Mathf.Approximately(shown, target))
            {
                shown = smoothSeconds > 0f ? Mathf.MoveTowards(shown, target, Time.deltaTime / smoothSeconds) : target;
                Apply();
            }
            if (!faceViewer || viewer == null) return;
            var toViewer = viewer.position - transform.position;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
        }

        void Apply()
        {
            var scale = fill.localScale;
            scale.x = fullWidth * Mathf.Clamp01(shown);
            fill.localScale = scale;
            // 왼쪽 끝 고정: 줄어든 만큼 중심을 왼쪽으로.
            fill.localPosition = fillBase - Vector3.right * (fullWidth - scale.x) * 0.5f;
        }
    }
}
