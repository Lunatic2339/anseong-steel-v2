using Anseong.Combat;
using UnityEngine;

namespace Anseong.UI
{
    /// <summary>
    /// 조종석 콘솔 위 조종사 로봇 HP 막대(M2-03 임시, HUD 아트는 M4). 바탕 + 채움 사각형, 채움은 왼쪽 끝 기준으로 줄어든다.
    /// 낮으면 색이 빨개진다. 서버가 복제한 상태(<see cref="CombatDirector.State"/>)만 읽는다.
    /// </summary>
    public class PilotHealthBar : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;
        [SerializeField, Tooltip("채움 사각형. 메뉴가 만든다.")]
        Transform fill;
        [SerializeField, Tooltip("채움 렌더러(색). 메뉴가 만든다.")]
        Renderer fillRenderer;
        [SerializeField, Min(0f), Tooltip("줄어드는 것을 따라가는 시간(초).")]
        float smoothSeconds = 0.3f;
        [SerializeField] Color healthy = new Color(0.3f, 0.9f, 0.45f);
        [SerializeField] Color danger = new Color(1f, 0.2f, 0.15f);
        [SerializeField, Range(0f, 1f), Tooltip("이 비율 아래면 위험 색.")]
        float dangerBelow = 0.3f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;
        float shown = 1f, fullWidth;
        Vector3 fillBase;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (director == null || fill == null)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            fullWidth = fill.localScale.x;
            fillBase = fill.localPosition;
            shown = director.State.PilotFraction;
            Apply();
        }

        void LateUpdate()
        {
            var target = director.State.PilotFraction;
            if (Mathf.Approximately(shown, target)) return;
            shown = smoothSeconds > 0f ? Mathf.MoveTowards(shown, target, Time.deltaTime / smoothSeconds) : target;
            Apply();
        }

        void Apply()
        {
            var scale = fill.localScale;
            scale.x = fullWidth * Mathf.Clamp01(shown);
            fill.localScale = scale;
            fill.localPosition = fillBase - Vector3.right * (fullWidth - scale.x) * 0.5f;
            if (fillRenderer == null) return;
            var color = shown < dangerBelow ? danger : healthy;
            fillRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            fillRenderer.SetPropertyBlock(block);
        }
    }
}
