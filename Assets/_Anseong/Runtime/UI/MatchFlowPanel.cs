using System.Text;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Flow;
using UnityEngine;

namespace Anseong.UI
{
    /// <summary>
    /// 판 흐름 글자판(M4-01 프로토타입, 임시 HUD). 눈앞 위쪽에 지금 장면·할 일·남은 시간·사령관 대사·결과판을 글자로 띄운다.
    /// 릴리스 빌드에서도 보인다(게임 화면 자리). 윤주 HUD 시안이 오면 M4-06 HUD가 이 자리를 대신한다.
    /// 메뉴 'Add Network Session'이 XR 카메라 아래에 만든다.
    /// </summary>
    public class MatchFlowPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("판 흐름. 비어 있으면 시작할 때 한 번 찾는다.")]
        MatchFlow flow;

        [SerializeField, Tooltip("이 PC의 전투 판정(사령관 대사 사건을 듣는다). 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField, Tooltip("보스 두뇌(무기 경계). 비어 있으면 시작할 때 한 번 찾는다.")]
        BossBrain boss;

        [SerializeField, Tooltip("글자를 쓸 TextMesh. 메뉴가 만든다.")]
        TextMesh text;

        [SerializeField, Tooltip("조절 메뉴. 열려 있는 동안 글자를 숨긴다. 비어 있으면 시작할 때 한 번 찾는다.")]
        TuningMenu tuningMenu;

        [SerializeField, Tooltip("한글을 쓰려고 시작할 때 이 OS 글꼴로 바꾼다(앞에서부터 있는 것).")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [SerializeField, Min(0.5f), Tooltip("사령관 대사 글자를 보여 주는 시간(초).")]
        float lineSeconds = 5f;

        readonly StringBuilder builder = new StringBuilder(256);
        MeshRenderer textRenderer;
        string lineText = string.Empty;
        float lineUntil;
        float nextRefresh;
        bool subscribed;

        void Start()
        {
            if (flow == null) flow = FindAnyObjectByType<MatchFlow>();
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (boss == null) boss = FindAnyObjectByType<BossBrain>();
            if (tuningMenu == null) tuningMenu = FindAnyObjectByType<TuningMenu>();
            if (text == null)
            {
                enabled = false;
                return;
            }
            textRenderer = text.GetComponent<MeshRenderer>();
            UseOsFont();
            if (director != null)
            {
                director.Cue += OnCue;
                subscribed = true;
            }
        }

        void OnDestroy()
        {
            if (subscribed && director != null) director.Cue -= OnCue;
        }

        void OnCue(CombatEvent e)
        {
            if (e.Cue != CombatCue.CommanderLine) return;
            var line = CommanderLines.Text(e.Code);
            if (string.IsNullOrEmpty(line)) return;
            lineText = line;
            lineUntil = Time.unscaledTime + lineSeconds;
        }

        void Update()
        {
            var hide = tuningMenu != null && tuningMenu.IsOpen;
            if (textRenderer != null) textRenderer.enabled = !hide;
            if (hide || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            text.text = Build();
        }

        string Build()
        {
            builder.Clear();
            if (flow == null || !flow.Running)
            {
                builder.Append("<color=#aaaaaa>판 흐름 대기(세션 시작 전)</color>");
                return builder.ToString();
            }
            var scene = flow.Scene;
            builder.Append("<b>").Append(Title(scene)).Append("</b>");
            var remaining = flow.Remaining;
            if (remaining >= 0f && scene != MatchScene.Result) builder.Append("  <color=#aaaaaa>").Append(Mathf.CeilToInt(remaining)).Append("초</color>");
            builder.Append('\n');
            if (scene == MatchScene.Result) AppendResult(flow.Result);
            else if (scene == MatchScene.Checkup) builder.Append("<color=#cccccc>").Append(CheckupHint((CheckupStep)flow.CheckupStep)).Append("</color>");
            else builder.Append("<color=#cccccc>").Append(Hint(scene)).Append("</color>");
            if (MatchFlowRules.IsFight(scene) && boss != null && director != null) AppendWeapon();
            if (Time.unscaledTime < lineUntil && lineText.Length > 0)
                builder.Append("\n<color=#ffd24a>사령관: ").Append(lineText).Append("</color>");
            return builder.ToString();
        }

        /// <summary>보스 무기 금 세 칸(M4-02). HUD 시안이 오면 M4-06이 그림으로 바꾼다.</summary>
        void AppendWeapon()
        {
            var state = director.State;
            var hp = state.BossMax > 0f ? state.BossHealth / state.BossMax * 100f : 100f;
            var weapon = BossWeaponRules.Current(hp, boss.Thresholds);
            if (weapon == BossWeapon.None) return;
            var cracks = BossWeaponRules.CrackCount(hp, boss.Thresholds);
            builder.Append("\n보스 ").Append(boss.WeaponName(weapon)).Append(" 금 ");
            for (var i = 0; i < BossWeaponRules.Cracks; i++) builder.Append(i < cracks ? "■" : "□");
        }

        void AppendResult(MatchResult r)
        {
            builder.Append(r.Won ? "<color=#7fff7f>작전 성공</color>" : "<color=#ff7f7f>작전 실패</color>").Append('\n');
            var seconds = Mathf.RoundToInt(r.Seconds);
            builder.Append("걸린 시간 ").Append(seconds / 60).Append("분 ").Append(seconds % 60).Append("초\n");
            builder.Append("막기 성공률 ").Append(r.BlockPercent < 0 ? "-" : r.BlockPercent + "%").Append('\n');
            builder.Append("드론 격추 ").Append(r.Drones).Append("   최고 연계 ").Append(r.BestCombo).Append('\n');
            if (r.Reboots > 0) builder.Append("재기동 ").Append(r.Reboots).Append("회\n");
            builder.Append("<color=#aaaaaa>다시 하기: 두 스틱 1초 꾹 또는 B 메뉴 \"판 재시작\"</color>");
        }

        /// <summary>장면 이름(10_game_scenario.md 장면 번호).</summary>
        public static string Title(MatchScene scene)
        {
            switch (scene)
            {
                case MatchScene.Waiting: return "장면 0 · 대기";
                case MatchScene.Checkup: return "장면 1 · 기체 점검";
                case MatchScene.Entrance: return "장면 2 · 마당 등장";
                case MatchScene.DroneHunt: return "장면 3 · 숨은 드론 사냥";
                case MatchScene.Approach: return "장면 4 · 접근";
                case MatchScene.FightCannon: return "근접전 · 목표: 보스 팔뚝 포";
                case MatchScene.LostGun: return "장면 5 · 주포 상실";
                case MatchScene.FightArmor: return "장면 6 · 목표: 보스 방패";
                case MatchScene.LostSword: return "장면 7 · 검·방패 상실";
                case MatchScene.FightCore: return "장면 8 · 맨손 · 목표: 핵";
                case MatchScene.Finisher: return "장면 9 · 필살기";
                case MatchScene.Result: return "장면 10 · 결과";
                case MatchScene.Rebooting: return "기체 정지 · 재기동 중";
                case MatchScene.Opening: return "오프닝";
                case MatchScene.Ending: return "엔딩";
                default: return scene.ToString();
            }
        }

        /// <summary>기체 점검 단계별 할 일(M4-05).</summary>
        public static string CheckupHint(CheckupStep step)
        {
            switch (step)
            {
                case CheckupStep.Connect: return "1-1 신경 연결: 정면으로 두 팔 뻗고 그립 → 편한 가드에서 그립";
                case CheckupStep.JointTest: return "1-2 관절 시험: 철판에 좌우 번갈아 세 번 → 트리거";
                case CheckupStep.ShieldDrill: return "1-3 검·방패: 오른쪽이 오는 쪽을 불러 주고, 왼팔 방패로 막기(3번)";
                case CheckupStep.GunBrief: return "1-4 스캐너·포 장착: 왼팔이 찾고 오른팔이 쏜다";
                case CheckupStep.Sortie: return "1-5 출격: 둘이 같이 스틱을 앞으로!";
                default: return string.Empty;
            }
        }

        /// <summary>장면에서 할 일(프로토타입 안내). 자리만 있는 장면은 무엇이 들어올지 적는다.</summary>
        public static string Hint(MatchScene scene)
        {
            switch (scene)
            {
                case MatchScene.Waiting: return "조종사 접속을 기다리는 중";
                case MatchScene.Checkup: return "보정 → 철판 → 공 막기 → 무장 → 둘이 같이 스틱을 앞으로";
                case MatchScene.Entrance: return "마당 끝 120m에 보스, 머리에 붉은 빛";
                case MatchScene.DroneHunt: return "왼손 스캐너로 훑어 드론을 찾고, 오른손 트리거로 쏴라";
                case MatchScene.Approach: return "주먹 포탄을 막으며 둘이 같이 스틱을 앞으로";
                case MatchScene.FightCannon:
                case MatchScene.FightArmor:
                case MatchScene.FightCore: return "막고 → 빈틈에 번갈아 세 번 + 트리거";
                case MatchScene.LostGun:
                case MatchScene.LostSword: return "보스 보복! 막거나 피하라";
                case MatchScene.Finisher: return "둘이 함께 정면으로 팔을 뻗고 버튼을 모두 눌러라";
                case MatchScene.Rebooting: return "잠시 뒤 이 구간 처음부터(한 판에 한 번)";
                case MatchScene.Opening:
                case MatchScene.Ending: return "앞 화면 영상";
                default: return string.Empty;
            }
        }

        void UseOsFont()
        {
            if (osFontNames == null || osFontNames.Length == 0) return;
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, text.fontSize > 0 ? text.fontSize : 32);
            if (font == null) return;
            text.font = font;
            if (textRenderer != null) textRenderer.sharedMaterial = font.material;
        }
    }
}
