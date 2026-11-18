using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Anseong.Skirmish
{
    /// <summary>
    /// 조종석 아래쪽에 뜨는 임시 계기판(글자). 헤드셋과 모니터 양쪽에서 보인다.
    /// 화면은 시작할 때 코드로 만든다(씬·프리팹에 UI를 저장하지 않음). 아트가 오면 이 컴포넌트만 바꾼다.
    /// </summary>
    public class SkirmishHud : MonoBehaviour
    {
        [SerializeField, Tooltip("XR Origin 루트(헤드셋일 때 계기판 기준).")] Transform rig;
        [SerializeField, Tooltip("머리(모니터 확인일 때 계기판 기준).")] Transform head;
        [SerializeField, Tooltip("한글이 나오는 OS 글꼴(앞에서부터 있는 것).")] string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };
        [SerializeField, Tooltip("헤드셋: XR Origin 기준 계기판 위치(m).")] Vector3 vrLocalPosition = new Vector3(0f, 0.85f, 0.9f);
        [SerializeField, Tooltip("모니터: 머리 기준 계기판 위치(m).")] Vector3 desktopLocalPosition = new Vector3(0f, -0.42f, 1f);
        [SerializeField, Range(0f, 80f), Tooltip("계기판을 위로 젖히는 각도(°).")] float tilt = 35f;

        Text text;
        Transform panel;
        string lastEvent = "";

        public void Bind(Transform rigRoot, Transform headTransform) { rig = rigRoot; head = headTransform; }

        void Build()
        {
            var canvasObject = new GameObject("Skirmish HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Canvas를 붙이면 Transform이 RectTransform으로 바뀌므로 그 뒤에 잡는다.
            panel = canvasObject.transform;
            var rect = (RectTransform)panel;
            rect.sizeDelta = new Vector2(900f, 360f);
            panel.localScale = Vector3.one * 0.001f;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(panel, false);
            text = textObject.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(osFontNames, 40);
            text.fontSize = 40;
            text.alignment = TextAnchor.UpperCenter;
            text.color = new Color(0.85f, 1f, 0.95f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        }

        /// <param name="waiting">교전 시작 전에 띄울 안내(없으면 빈 문자열).</param>
        public void Show(SkirmishState st, SkirmishSettings s, IReadOnlyList<SkirmishEvent> events, SkirmishSeat seat, bool vr, string waiting)
        {
            if (text == null) Build();
            var parent = vr ? rig : head;
            if (parent != null && panel.parent != parent)
            {
                panel.SetParent(parent, false);
                panel.localPosition = vr ? vrLocalPosition : desktopLocalPosition;
                panel.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            }
            for (int i = 0; events != null && i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind != SkirmishEventKind.CannonShot && e.Kind != SkirmishEventKind.MechGunShot) lastEvent = Describe(e);
            }
            if (st.phase == SkirmishPhase.Waiting) lastEvent = waiting;

            string status = seat == SkirmishSeat.Mech
                ? $"메카 내구도 {st.hull:0}    고로 {st.furnace}/{s.furnaceHits}"
                : $"고로 {st.furnace}/{s.furnaceHits}    미사일 {st.missiles}    포열 {st.cannonHeat * 100f:0}%{(st.cannonOverheated ? " 과열" : "")}";
            string lockLine = seat == SkirmishSeat.Gunner && st.lockTarget >= 0
                ? $"잠금 {(st.lockTarget == SkirmishSession.G3 ? "정면 적" : "동쪽 적")} {st.lockProgress * 100f:0}%" + (st.lockProgress >= 1f ? " — 놓으면 발사" : "")
                : "";
            string result = st.phase == SkirmishPhase.Cleared ? "적치장 확보"
                : st.phase == SkirmishPhase.Failed ? "실패 — 운영자가 R로 재시작" : "";
            text.text = $"{status}\n{lockLine}\n{lastEvent}\n{result}";
        }

        static string Describe(SkirmishEvent e)
        {
            string who = e.Id == SkirmishSession.G4 ? "동쪽 적" : "정면 적";
            switch (e.Kind)
            {
                case SkirmishEventKind.WindupStarted: return e.Id == SkirmishSession.G4 ? "동쪽 적이 고로를 겨눈다!" : "정면 적 큰 탄 예고!";
                case SkirmishEventKind.ShellFired: return "탄 발사";
                case SkirmishEventKind.ShellIntercepted: return "큰 탄 요격";
                case SkirmishEventKind.ShellBlocked: return "메카가 막았다";
                case SkirmishEventKind.ShellMissed: return "큰 탄 빗나감";
                case SkirmishEventKind.MechHit: return "메카 피격!";
                case SkirmishEventKind.MissileFired: return "미사일 발사";
                case SkirmishEventKind.MissileHit: return who + " 미사일 명중";
                case SkirmishEventKind.MissileLost: return "미사일 표적 없음";
                case SkirmishEventKind.EnemyStaggered: return who + " 멈칫 — 예고 끊김";
                case SkirmishEventKind.EnemyDown: return who + " 격파";
                case SkirmishEventKind.FurnaceHit: return "고로 피격! 동쪽 화력을 끊어!";
                default: return "";
            }
        }
    }
}
