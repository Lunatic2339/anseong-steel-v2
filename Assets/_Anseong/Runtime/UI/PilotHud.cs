using System.Text;
using Anseong.Boss;
using Anseong.Combat;
using Anseong.Flow;
using Anseong.Net;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.UI
{
    /// <summary>
    /// 조종사 HUD(M4-06 프로토타입, 각 PC). 글자로 먼저: 공격 오는 쪽(오른쪽만), 드론 방향(왼쪽만), 기체 HP, 해킹 잡음.
    /// 무엇을 누가 보는지는 <see cref="HudRules"/>. 윤주 HUD 시안이 오면 이 글자를 화살표·아이콘으로 바꾼다(같은 사건을 듣는다).
    /// 메뉴 'Add Match Flow'가 XR 카메라 왼쪽 아래에 만든다.
    /// </summary>
    public class PilotHud : MonoBehaviour
    {
        [SerializeField] CombatDirector director;
        [SerializeField, Tooltip("보스 서버(기술의 요구 대응을 읽는다). 비어 있으면 처음 예고 때 한 번 찾는다. 없으면 05 기본값.")]
        BossServer boss;
        [SerializeField] TextMesh text;
        [SerializeField, Tooltip("한쪽에만 보이기를 끈다(시험용: 둘 다 모든 것을 봄).")]
        bool showEverything;
        [SerializeField, Min(0.3f), Tooltip("공격·드론 표시를 보여 주는 시간(초).")]
        float calloutSeconds = 2f;
        [SerializeField] string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        readonly StringBuilder builder = new StringBuilder(128);
        SessionBootstrap session;
        TuningMenu tuningMenu;
        MeshRenderer textRenderer;
        Transform head;
        string attack = string.Empty, drone = string.Empty, coop = string.Empty;
        float attackUntil, droneUntil, coopUntil, nextRefresh;
        float clashGauge = -1f;
        PilotSide? hacked;
        bool subscribed;

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            session = FindAnyObjectByType<SessionBootstrap>();
            tuningMenu = FindAnyObjectByType<TuningMenu>();
            head = transform.parent;
            if (text == null)
            {
                enabled = false;
                return;
            }
            textRenderer = text.GetComponent<MeshRenderer>();
            var font = osFontNames != null && osFontNames.Length > 0 ? Font.CreateDynamicFontFromOSFont(osFontNames, text.fontSize > 0 ? text.fontSize : 32) : null;
            if (font != null)
            {
                text.font = font;
                if (textRenderer != null) textRenderer.sharedMaterial = font.material;
            }
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

        bool BothArms => showEverything || session == null || session.Mode == SessionMode.SoloBothArms;
        PilotSide Viewer => session != null && session.Mode == SessionMode.Client ? PilotSide.Right : PilotSide.Left;
        bool Sees(HudInfo info) => HudRules.Visible(info, Viewer, BothArms, hacked);

        void OnCue(CombatEvent e)
        {
            switch (e.Cue)
            {
                case CombatCue.BossTelegraph:
                    // 기술의 대응 방식에 맞는 글(붙잡기 = 둘이 막기, 충격파 = 회피). 오는 쪽·피할 쪽은 볼 수 있는 사람에게만.
                    var kind = (BossAttackKind)e.Code;
                    var callout = HudRules.AttackCallout(kind, CounterOf(kind), e.Side, Sees(HudInfo.AttackSide), Sees(HudInfo.DodgeSide));
                    if (callout.Length > 0) Show(ref attack, ref attackUntil, callout);
                    break;
                case CombatCue.DroneFire:
                    if (Sees(HudInfo.DroneDirection) && head != null)
                    {
                        var local = head.InverseTransformPoint(e.Point);
                        var yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                        var pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                        Show(ref drone, ref droneUntil, "드론 사격: " + HudRules.Direction(yaw, pitch));
                    }
                    break;
                case CombatCue.HackStart:
                    hacked = e.Side;
                    break;
                // 잡고 베기: 붙잡은 쪽(왼)과 베는 쪽(오른)에 서로 다른 말을 띄운다. 1인 양팔은 둘 다.
                case CombatCue.GrabStart:
                    coop = BothArms ? "붙잡았다! 오른팔로 베어라" : Viewer == PilotSide.Left ? "붙잡았다! 놓지 마라" : "지금 베어라!";
                    coopUntil = Time.unscaledTime + Mathf.Max(0.5f, e.Amount);
                    break;
                case CombatCue.GrabSlash:
                    Show(ref coop, ref coopUntil, "잡고 베기 성공!");
                    break;
                case CombatCue.GrabBreak:
                    Show(ref coop, ref coopUntil, e.Amount >= 1f ? "뿌리쳤다! 반격이 온다" : "놓쳤다");
                    break;
                case CombatCue.ClashStart:
                    clashGauge = 0.4f;
                    break;
                case CombatCue.ClashTick:
                    clashGauge = e.Amount;
                    break;
                case CombatCue.ClashWin:
                    clashGauge = -1f;
                    Show(ref coop, ref coopUntil, "밀어냈다!");
                    break;
                case CombatCue.ClashLose:
                    clashGauge = -1f;
                    Show(ref coop, ref coopUntil, "밀렸다!");
                    break;
                case CombatCue.HackCleared:
                    hacked = null;
                    break;
                case CombatCue.MatchScene:
                    hacked = null;
                    if ((MatchScene)e.Code != MatchScene.Clash) clashGauge = -1f;
                    break;
            }
        }

        bool lookedForBoss;

        /// <summary>그 기술의 요구 대응(기술 에셋). 보스가 없으면 05 기본값.</summary>
        BossCounter CounterOf(BossAttackKind kind)
        {
            if (boss == null && !lookedForBoss)
            {
                lookedForBoss = true;
                boss = FindAnyObjectByType<BossServer>();
            }
            return boss != null ? boss.SpecOf(kind).counter : BossAttackSpec.Default(kind).counter;
        }

        void Show(ref string slot, ref float until, string value)
        {
            slot = value;
            until = Time.unscaledTime + calloutSeconds;
        }

        void Update()
        {
            var hide = tuningMenu != null && tuningMenu.IsOpen;
            if (textRenderer != null) textRenderer.enabled = !hide;
            if (hide || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            builder.Clear();
            if (hacked.HasValue && hacked.Value == Viewer && !BothArms)
            {
                builder.Append("<color=#ff5050>▒▒ 해킹 ▒▒ 동료가 떼어 줘야 한다 ▒▒</color>");
                text.text = builder.ToString();
                return;
            }
            if (Time.unscaledTime < attackUntil) builder.Append("<size=120><color=#ffcc33>").Append(attack).Append("</color></size>\n");
            if (clashGauge >= 0f)
            {
                // 힘겨루기 게이지: 둘 다 팔을 들어야 찬다.
                var filled = Mathf.Clamp(Mathf.RoundToInt(clashGauge * 10f), 0, 10);
                builder.Append("<size=120><color=#ff8844>버텨라! ").Append('■', filled).Append('□', 10 - filled).Append("</color></size>\n");
            }
            if (Time.unscaledTime < coopUntil) builder.Append("<size=120><color=#ffe066>").Append(coop).Append("</color></size>\n");
            if (Time.unscaledTime < droneUntil) builder.Append("<color=#66ffcc>").Append(drone).Append("</color>\n");
            if (director != null)
            {
                var s = director.State;
                if (s.PilotMax > 0f) builder.Append("기체 HP ").Append(Mathf.RoundToInt(s.PilotHealth / s.PilotMax * 100f)).Append('%');
            }
            if (!BothArms) builder.Append("  <color=#888888>(").Append(Viewer == PilotSide.Left ? "왼팔" : "오른팔").Append(")</color>");
            text.text = builder.ToString();
        }
    }
}
