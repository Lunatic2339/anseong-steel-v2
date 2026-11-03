using Anseong.Combat;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 공격 예고 임시 발광(M2-02): 예고가 시작되면 명중 순간까지 보스 몸 색을 예고 등급 색으로 깜빡인다.
    /// 05: 일반 = 그 팔, 강 = 두 팔, 가드 불가 = 몸 전체 붉게. 지금은 팔을 따로 나누지 않고 몸 전체(임시 재질 하나). 진짜 효과는 M5.
    /// 전투 이벤트만 듣는다. 재질은 바꾸지 않고 MaterialPropertyBlock으로 색만 덮는다(URP Lit _BaseColor·_EmissionColor).
    /// </summary>
    public class BossTelegraphTint : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField, Tooltip("색을 덮을 렌더러. 비어 있으면 자식 전부.")]
        Renderer[] renderers = new Renderer[0];

        [SerializeField, Tooltip("일반 예고 색.")] Color normal = new Color(1f, 0.85f, 0.2f);
        [SerializeField, Tooltip("강공격 예고 색.")] Color heavy = new Color(1f, 0.45f, 0.1f);
        [SerializeField, Tooltip("가드 불가 예고 색.")] Color unblockable = new Color(1f, 0.05f, 0.05f);

        [SerializeField, Range(0.5f, 8f), Tooltip("깜빡임 빠르기(초당 번).")]
        float pulsesPerSecond = 3f;

        [SerializeField, Range(0f, 1f), Tooltip("가장 셀 때 섞는 정도.")]
        float strength = 0.7f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Color color;
        float until = -1f;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            if (director == null)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
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
                case CombatCue.BossTelegraph:
                    var level = Mathf.RoundToInt(e.Amount);
                    color = level >= 2 ? unblockable : level == 1 ? heavy : normal;
                    // 명중 순간까지(정확한 길이는 몰라도 강·가드 불가가 길다). 명중 순간 이벤트가 오면 끈다.
                    until = Time.time + (level >= 2 ? 2.0f : level == 1 ? 1.8f : 1.2f);
                    break;
                case CombatCue.BossStrike:
                case CombatCue.BossOpening:
                    until = -1f;
                    Clear();
                    break;
            }
        }

        void Update()
        {
            if (until < 0f) return;
            if (Time.time >= until)
            {
                until = -1f;
                Clear();
                return;
            }
            var k = strength * (0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f));
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(Color.white, color, k));
                block.SetColor(EmissionId, color * k);
                r.SetPropertyBlock(block);
            }
        }

        void Clear()
        {
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(null);
        }
    }
}
