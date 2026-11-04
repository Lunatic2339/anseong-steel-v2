using Anseong.Boss;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.UI
{
    /// <summary>
    /// 보스 공격 안내 글자(M2-06, 선우 11/04). 보스가 공격을 예고하면 눈앞 위쪽에 크게 "무슨 공격 + 어느 팔로 무엇을" 띄우고,
    /// 연타 중엔 다음 타("2/4 → 왼팔"), 명중 순간엔 결과(막음·맞음·피함), 빈틈엔 "지금 치기"를 띄운 뒤 잠시 후 지운다.
    /// 색은 세기(노랑 일반, 주황 강공격, 빨강 막기 불가)로 보스 몸 색과 같다. 전투 이벤트만 듣는다(모든 PC).
    /// 초보 안내 HUD(M4)가 오면 이 자리를 바꾼다. 조절 메뉴 "안내 → 공격 안내 글자"로 끈다. Add Pilot Input 메뉴가 XR 카메라 아래에 만든다.
    /// </summary>
    public class BossAttackCallout : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector combat;
        [SerializeField, Tooltip("글자. 메뉴가 만든다.")]
        TextMesh text;
        [SerializeField, Tooltip("조절 메뉴. 열려 있으면 글자를 숨긴다. 비어 있으면 시작할 때 한 번 찾는다.")]
        TuningMenu tuningMenu;
        [SerializeField, Tooltip("한글 OS 글꼴(앞에서부터 있는 것).")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [SerializeField, Tooltip("켜면 보스 공격마다 안내 글자를 띄운다.")]
        bool show = true;
        [SerializeField, Min(0.3f), Tooltip("결과(막음·맞음) 글자를 남겨 두는 시간(초).")]
        float resultSeconds = 1.5f;

        [SerializeField] Color normal = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color heavy = new Color(1f, 0.55f, 0.15f);
        [SerializeField] Color unblockable = new Color(1f, 0.25f, 0.25f);

        const string Good = "#5BE37D", Bad = "#FF6B6B", Info = "#4DD2FF";

        BossAttackKind kind;
        int hits = 1;
        string head = string.Empty, line = string.Empty, result = string.Empty;
        Color color = Color.white;
        float hideAt = -1f;
        MeshRenderer textRenderer;

        void Start()
        {
            if (combat == null) combat = FindAnyObjectByType<CombatDirector>();
            if (tuningMenu == null) tuningMenu = FindAnyObjectByType<TuningMenu>();
            if (text == null || combat == null)
            {
                gameObject.SetActive(false);
                return;
            }
            textRenderer = text.GetComponent<MeshRenderer>();
            UseOsFont();
            text.text = string.Empty;
            combat.Cue += OnCue;
        }

        void OnDestroy()
        {
            if (combat != null) combat.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.BossTelegraph:
                    kind = (BossAttackKind)e.Code;
                    hits = Mathf.Max(1, e.ComboStep);
                    var level = Mathf.RoundToInt(e.Amount);
                    color = level >= 2 ? unblockable : level == 1 ? heavy : normal;
                    head = BossAttackGuide.Name(kind, hits);
                    line = BossAttackGuide.Action(kind, e.Side);
                    result = string.Empty;
                    hideAt = -1f;
                    break;
                case CombatCue.BossStrike:
                    if (kind == BossAttackKind.AlternatingCombo)
                    {
                        var hit = Mathf.RoundToInt(e.Amount);
                        var nextSide = e.Side == PilotSide.Left ? PilotSide.Right : PilotSide.Left;
                        var next = BossAttackGuide.Next(hit, hits, nextSide);
                        line = next.Length > 0 ? next : "마지막 타";
                    }
                    break;
                case CombatCue.GuardBlock:
                    Result($"<color={Good}>막음!</color>");
                    break;
                case CombatCue.GuardFail:
                    Result($"<color={Bad}>막기 모자람</color>");
                    break;
                case CombatCue.PilotHit:
                    Result(result.Length > 0 ? $"{result}  <color={Bad}>맞음 -{e.Damage:F0}%</color>" : $"<color={Bad}>맞음 -{e.Damage:F0}%</color>");
                    break;
                case CombatCue.Dodge:
                    Result($"<color={Good}>피함!</color>");
                    break;
                case CombatCue.BossOpening:
                    head = $"<color={Good}>빈틈 {e.Amount:F1}초</color>";
                    line = "지금 치기!";
                    result = string.Empty;
                    color = Color.white;
                    hideAt = Time.time + Mathf.Max(resultSeconds, e.Amount);
                    break;
                case CombatCue.BossKneel:
                    head = $"<color={Info}>필살기 차례</color>";
                    line = "두 팔을 정면으로 쭉 뻗고 버티기";
                    result = string.Empty;
                    color = Color.white;
                    hideAt = -1f;
                    break;
                case CombatCue.FinisherCharge:
                    line = "충전 중: 그대로 버티기";
                    break;
                case CombatCue.FinisherReset:
                    line = $"<color={Bad}>풀림: 두 팔 다시 함께 뻗기</color>";
                    break;
                case CombatCue.FinisherFire:
                    head = $"<color={Info}>발사!</color>";
                    line = string.Empty;
                    hideAt = Time.time + 3f;
                    break;
                case CombatCue.PhaseChanged:
                case CombatCue.EncounterWon:
                case CombatCue.PilotDefeated:
                    head = line = result = string.Empty;
                    break;
                default:
                    return;
            }
            Redraw();
        }

        // 결과는 잠깐 보여 주고 지운다. 연타는 다음 타 안내가 아래 줄에 남는다.
        void Result(string value)
        {
            result = value;
            var last = kind != BossAttackKind.AlternatingCombo || line == "마지막 타";
            hideAt = last ? Time.time + resultSeconds : -1f;
        }

        void Update()
        {
            var hide = !show || (tuningMenu != null && tuningMenu.IsOpen);
            if (textRenderer != null && textRenderer.enabled == hide) textRenderer.enabled = !hide;
            if (hideAt < 0f || Time.time < hideAt) return;
            hideAt = -1f;
            head = line = result = string.Empty;
            Redraw();
        }

        void Redraw()
        {
            if (head.Length == 0 && line.Length == 0 && result.Length == 0)
            {
                text.text = string.Empty;
                return;
            }
            text.color = color;
            var s = head;
            if (line.Length > 0) s += $"\n<size={Mathf.RoundToInt((text.fontSize > 0 ? text.fontSize : 32) * 0.7f)}>{line}</size>";
            if (result.Length > 0) s += "\n" + result;
            text.text = s;
        }

        void UseOsFont()
        {
            if (osFontNames == null || osFontNames.Length == 0) return;
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, text.fontSize > 0 ? text.fontSize : 32);
            if (font == null) return;
            text.font = font;
            if (textRenderer != null) textRenderer.sharedMaterial = font.material;
        }

        public void AddTuning(TuningList list)
        {
            list.AddToggle("안내", "callout.show", "공격 안내 글자", () => show, v =>
            {
                show = v;
                if (!v && text != null) text.text = string.Empty;
            });
        }
    }
}
