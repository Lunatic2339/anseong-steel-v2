using Anseong.Combat;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>
    /// 보스 공격 예고 임시 발광(M2-02): 예고가 시작되면 명중 순간까지 보스 몸 색을 예고 등급 색으로 깜빡인다.
    /// 05: 일반 = 그 팔, 강 = 두 팔, 가드 불가 = 몸 전체 붉게. 지금은 팔을 따로 나누지 않고 몸 전체(임시 재질 하나). 진짜 효과는 M5.
    /// 전투 이벤트만 듣는다. 재질은 바꾸지 않고 MaterialPropertyBlock으로 색만 덮는다(URP Lit _BaseColor·_EmissionColor).
    /// 공격 쪽 표시(M2-05 보강, 선우 11/03: 보스 모션이 없어 무엇이 오는지 읽기 어려움): 보스 가슴 높이 좌우에 큰 구를 두고,
    /// 막아야 할 조종사 팔 쪽 구만 켠다(내려찍기·돌진은 둘 다, 연타는 타마다 번갈아). 진짜 애니가 오면 끈다(markers 토글).
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

        [Header("공격 쪽 표시(임시)")]
        [SerializeField, Tooltip("막아야 할 쪽을 큰 구로 보여 준다. 진짜 보스 애니가 들어오면 끈다.")]
        bool markers = true;
        [SerializeField, Tooltip("조종사 왼팔로 막을 공격 표시(보스 쪽에서 보면 오른쪽). 메뉴가 만든다.")]
        Renderer leftMarker;
        [SerializeField, Tooltip("조종사 오른팔로 막을 공격 표시.")]
        Renderer rightMarker;
        [SerializeField, Min(0.1f), Tooltip("연타: 한 타 뒤 다음 타 표시를 켜 두는 시간(초, 05: 타 간격 0.7).")]
        float comboGap = 0.7f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Color color;
        float until = -1f;
        bool showLeft, showRight;

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
            ShowMarkers(false, false);
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
                    MarkSides((BossAttackKind)e.Code, e.Side);
                    break;
                case CombatCue.BossStrike:
                    Clear();
                    // 연타: 다음 타는 반대쪽(4타).
                    if ((BossAttackKind)e.Code == BossAttackKind.AlternatingCombo && Mathf.RoundToInt(e.Amount) < 3)
                    {
                        MarkSides(BossAttackKind.AlternatingCombo, e.Side == Anseong.Pilot.PilotSide.Left ? Anseong.Pilot.PilotSide.Right : Anseong.Pilot.PilotSide.Left);
                        until = Time.time + comboGap;
                    }
                    else until = -1f;
                    break;
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
            ShowMarkers(showLeft, showRight);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r == leftMarker || r == rightMarker)
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, color * (0.6f + k));
                    r.SetPropertyBlock(block);
                    continue;
                }
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
            ShowMarkers(false, false);
        }

        void MarkSides(BossAttackKind kind, Anseong.Pilot.PilotSide side)
        {
            var both = kind == BossAttackKind.SlamBoth || kind == BossAttackKind.Charge;
            showLeft = both || side == Anseong.Pilot.PilotSide.Left;
            showRight = both || side == Anseong.Pilot.PilotSide.Right;
        }

        void ShowMarkers(bool left, bool right)
        {
            if (leftMarker != null) leftMarker.enabled = markers && left;
            if (rightMarker != null) rightMarker.enabled = markers && right;
        }
    }
}
