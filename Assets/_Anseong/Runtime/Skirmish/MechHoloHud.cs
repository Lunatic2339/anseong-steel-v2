using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Anseong.Skirmish
{
    /// <summary>
    /// P1 조종석의 홀로그램 HUD(선·원형 계기·글자, 전부 코드로 그린다). 얼굴에 붙어 따라다니는 것은 하나도 없다:
    /// 헤드셋에서는 고개를 따라오는 화면이 눈을 피로하게 하고 멀미를 부른다.
    /// <list type="bullet">
    /// <item>유리 패널(조종석에 고정): 왼쪽 = 내 로봇(내구도·차폐판), 오른쪽 = 상대와 P2(적 체력·상태·미사일).</item>
    /// <item>표적 테두리(세상에 고정): 적 둘레의 모서리 표시. 공격이 오는 쪽 변이 굵어지고, 가드를 굳히면 포격을 요청한다.</item>
    /// <item>순간 알림(조종석 위쪽에 고정): 막음·피격·격파가 잠깐 떴다 사라진다.</item>
    /// </list>
    /// 화면은 처음 보여 줄 때 만든다(씬·프리팹에 UI를 저장하지 않음).
    /// </summary>
    public class MechHoloHud : MonoBehaviour
    {
        [SerializeField, Tooltip("P1 조종석(원점 = 발밑). 유리 패널과 알림이 여기에 붙는다.")] Transform cockpit;
        [SerializeField, Tooltip("머리(카메라). 표적 테두리가 이쪽을 본다.")] Transform head;
        [SerializeField, Tooltip("차폐판 상태를 읽을 로봇 팔(없으면 표시하지 않음).")] SkirmishMechArms arms;
        [SerializeField] string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [Header("자리(조종석 기준 m). 눈은 대략 (0, 1.6, 0)")]
        [SerializeField] Vector3 leftPanel = new Vector3(-0.62f, 1.36f, 1f);
        [SerializeField, Tooltip("패널이 안쪽을 보도록 돌린 각도(°).")] float panelYaw = 28f;
        [SerializeField] Vector3 alertPosition = new Vector3(0f, 1.95f, 1f);
        [SerializeField, Min(0.1f), Tooltip("알림이 떠 있는 시간(초).")] float alertSeconds = 0.8f;
        [SerializeField, Range(0.2f, 1.3f), Tooltip("표적 테두리의 크기(적의 키 배수).")] float bracketSize = 0.5f;
        [SerializeField, Range(-0.3f, 0.5f), Tooltip("표적 테두리를 적의 몸 가운데에서 위로 올리는 정도(적의 키 배수).")] float bracketRaise = 0.22f;

        [Header("색")]
        [SerializeField] Color main = new Color(0.35f, 0.95f, 1f, 0.9f);
        [SerializeField] Color warn = new Color(1f, 0.72f, 0.2f, 0.95f);
        [SerializeField] Color danger = new Color(1f, 0.28f, 0.2f, 0.95f);
        [SerializeField] Color strike = new Color(1f, 1f, 1f, 0.95f);

        Font font;
        Sprite ring;
        Transform left, right, alert, target;
        Image hullRing, enemyRing, sideBar;
        Text hullNumber, shieldLine, enemyTitle, enemyNumber, enemyState, partnerLine, alertText, targetText;
        readonly List<Graphic> targetLines = new List<Graphic>();
        float alertUntil;

        public void Bind(Transform cockpitRoot, Transform headTransform, SkirmishMechArms mechArms)
        {
            cockpit = cockpitRoot; head = headTransform; arms = mechArms;
        }

        // ── 만들기 ──

        Transform NewCanvas(string name, Transform parent, Vector2 size, float metersPerUnit)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * metersPerUnit;
            return rect;
        }

        Image Line(Transform parent, Vector2 position, Vector2 size)
        {
            var image = new GameObject("Line").AddComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            image.color = main;
            image.raycastTarget = false;
            return image;
        }

        Image Ring(Transform parent, Vector2 position, float size, float alpha)
        {
            var image = Line(parent, position, new Vector2(size, size));
            image.sprite = ring;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.color = new Color(main.r, main.g, main.b, main.a * alpha);
            return image;
        }

        Text Label(Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor, string value = "")
        {
            var text = new GameObject("Text").AddComponent<Text>();
            var rect = text.rectTransform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = main;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        /// <summary>가장자리가 부드러운 흰 고리(원형 계기용).</summary>
        static Sprite MakeRing(int size, float inner, float outer)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f, soft = 1.5f / half;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    float a = Mathf.Clamp01((r - inner) / soft) * Mathf.Clamp01((outer - r) / soft);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        void Build()
        {
            font = Font.CreateDynamicFontFromOSFont(osFontNames, 48);
            ring = MakeRing(256, 0.78f, 0.96f);

            // 왼쪽: 내 로봇.
            left = NewCanvas("Holo left (내 로봇)", cockpit, new Vector2(400f, 320f), 0.001f);
            left.localPosition = leftPanel;
            left.localRotation = Quaternion.Euler(8f, -panelYaw, 0f);
            Frame(left, 400f, 320f);
            Label(left, new Vector2(0f, 128f), new Vector2(360f, 40f), 26, TextAnchor.MiddleLeft, "기체");
            Ring(left, new Vector2(-80f, 10f), 170f, 0.2f);
            hullRing = Ring(left, new Vector2(-80f, 10f), 170f, 1f);
            hullNumber = Label(left, new Vector2(-80f, 10f), new Vector2(170f, 80f), 54, TextAnchor.MiddleCenter);
            Label(left, new Vector2(90f, 40f), new Vector2(160f, 40f), 24, TextAnchor.MiddleLeft, "내구도");
            shieldLine = Label(left, new Vector2(0f, -122f), new Vector2(360f, 44f), 28, TextAnchor.MiddleLeft);

            // 오른쪽: 상대와 P2.
            right = NewCanvas("Holo right (상대)", cockpit, new Vector2(400f, 320f), 0.001f);
            right.localPosition = new Vector3(-leftPanel.x, leftPanel.y, leftPanel.z);
            right.localRotation = Quaternion.Euler(8f, panelYaw, 0f);
            Frame(right, 400f, 320f);
            enemyTitle = Label(right, new Vector2(0f, 128f), new Vector2(360f, 40f), 26, TextAnchor.MiddleLeft);
            Ring(right, new Vector2(80f, 10f), 170f, 0.2f);
            enemyRing = Ring(right, new Vector2(80f, 10f), 170f, 1f);
            enemyNumber = Label(right, new Vector2(80f, 10f), new Vector2(170f, 80f), 54, TextAnchor.MiddleCenter);
            enemyState = Label(right, new Vector2(-100f, 30f), new Vector2(170f, 90f), 30, TextAnchor.MiddleLeft);
            partnerLine = Label(right, new Vector2(0f, -122f), new Vector2(360f, 44f), 28, TextAnchor.MiddleLeft);

            // 알림.
            alert = NewCanvas("Holo alert", cockpit, new Vector2(900f, 120f), 0.001f);
            alert.localPosition = alertPosition;
            alert.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            alertText = Label(alert, Vector2.zero, new Vector2(900f, 120f), 72, TextAnchor.MiddleCenter);

            // 표적 테두리: 1000 단위 = 적의 키보다 조금 큰 정사각형.
            target = NewCanvas("Holo target bracket", null, new Vector2(1000f, 1000f), 0.03f);
            const float c = 430f, arm = 150f, thick = 14f;
            foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    targetLines.Add(Line(target, new Vector2(sx * (c - arm * 0.5f), sy * c), new Vector2(arm, thick)));
                    targetLines.Add(Line(target, new Vector2(sx * c, sy * (c - arm * 0.5f)), new Vector2(thick, arm)));
                }
            sideBar = Line(target, Vector2.zero, new Vector2(34f, 520f));
            targetText = Label(target, new Vector2(0f, -520f), new Vector2(1000f, 110f), 84, TextAnchor.MiddleCenter);
        }

        /// <summary>패널 위아래의 가는 선과 모서리 표시.</summary>
        void Frame(Transform panel, float width, float height)
        {
            float w = width * 0.5f, h = height * 0.5f;
            Line(panel, new Vector2(0f, h), new Vector2(width, 2f));
            Line(panel, new Vector2(0f, -h), new Vector2(width, 2f));
            foreach (int s in new[] { -1, 1 })
            {
                Line(panel, new Vector2(s * w, h - 14f), new Vector2(3f, 30f));
                Line(panel, new Vector2(s * w, -h + 14f), new Vector2(3f, 30f));
            }
            Line(panel, new Vector2(-w + 80f, h - 58f), new Vector2(120f, 4f));
        }

        // ── 보여 주기 ──

        /// <param name="now">알림을 끄는 데 쓰는 현재 시각(초).</param>
        public void Show(SkirmishState st, SkirmishSettings s, IReadOnlyList<SkirmishEvent> events, bool asPilot, float now)
        {
            if (cockpit == null) return;
            if (left == null) Build();
            if (target.gameObject.activeSelf && !asPilot) target.gameObject.SetActive(false);
            if (!asPilot) return;   // 패널과 알림은 조종석의 자식이라 조종석과 함께 꺼진다.

            // 내 로봇.
            float hull = Mathf.Clamp01(st.hull / Mathf.Max(1f, s.mechHull));
            var hullColor = hull > 0.5f ? main : hull > 0.25f ? warn : danger;
            hullRing.fillAmount = hull;
            hullRing.color = hullNumber.color = hullColor;
            hullNumber.text = Mathf.CeilToInt(st.hull).ToString();
            if (arms == null) shieldLine.text = "";
            else
            {
                bool ready = arms.ShieldReady;
                shieldLine.text = ready ? (st.guarding ? "차폐판  막는 중" : "차폐판  펴짐") : "차폐판  접힘";
                shieldLine.color = ready ? main : warn;
            }

            // 상대.
            bool present = st.gruntIndex >= 0 && s.grunts != null && st.gruntIndex < s.grunts.Length;
            bool alive = present && st.gruntStep != GruntStep.Dead;
            var stateColor = main;
            string state = "";
            if (alive)
                switch (st.gruntStep)
                {
                    case GruntStep.Rise: case GruntStep.Hold: case GruntStep.Strike:
                        state = GruntDuel.IncomingSide(st.gruntAttack) == Pilot.PilotSide.Left ? "왼쪽\n막아라" : "오른쪽\n막아라";
                        stateColor = danger; break;
                    case GruntStep.Stagger: case GruntStep.Frozen: state = "빈틈\n쳐라"; stateColor = strike; break;
                    case GruntStep.Guard: state = "가드\n포격 요청"; stateColor = warn; break;
                    case GruntStep.Approach: state = "접근 중"; break;
                    default: state = "대치"; break;
                }
            if (present)
            {
                float maxHp = Mathf.Max(1f, s.grunts[st.gruntIndex].spec.hp);
                enemyTitle.text = $"상대 {st.gruntIndex + 1} / {s.grunts.Length}";
                enemyRing.fillAmount = alive ? Mathf.Clamp01(st.gruntHp / maxHp) : 0f;
                enemyNumber.text = alive ? Mathf.CeilToInt(st.gruntHp).ToString() : "격파";
            }
            else
            {
                enemyTitle.text = s.grunts != null && st.gruntsDown >= s.grunts.Length && s.grunts.Length > 0 ? "상대 없음" : "탐색 중";
                enemyRing.fillAmount = 0f;
                enemyNumber.text = "";
            }
            enemyRing.color = enemyNumber.color = enemyState.color = stateColor;
            enemyState.text = state;
            partnerLine.text = $"공중포대  미사일 {st.missiles}";

            // 표적 테두리.
            bool showTarget = alive && head != null;
            if (target.gameObject.activeSelf != showTarget) target.gameObject.SetActive(showTarget);
            if (showTarget)
            {
                // 키 전체가 아니라 머리와 가슴 둘레만 두른다(가까이 붙으면 키 전체는 화면 밖으로 나간다).
                var center = SkirmishSession.GruntCenter(st, s) + Vector3.up * (s.gruntHeight * bracketRaise);
                var toward = head.position - center;
                // 적의 몸에 가리지 않게 조종사 쪽으로 조금 당겨 놓는다.
                target.position = center + toward.normalized * (s.gruntRadius + 2f);
                target.rotation = Quaternion.LookRotation(-toward, Vector3.up);
                target.localScale = Vector3.one * (s.gruntHeight * bracketSize / 1000f);
                foreach (var line in targetLines) line.color = stateColor;
                bool telegraph = st.gruntStep == GruntStep.Rise || st.gruntStep == GruntStep.Hold || st.gruntStep == GruntStep.Strike;
                sideBar.enabled = telegraph;
                if (telegraph)
                {
                    bool fromLeft = GruntDuel.IncomingSide(st.gruntAttack) == Pilot.PilotSide.Left;
                    sideBar.rectTransform.anchoredPosition = new Vector2(fromLeft ? -470f : 470f, 0f);
                    sideBar.color = danger;
                }
                targetText.text = st.gruntStep == GruntStep.Guard ? "가드 — 포격 요청" : st.gruntStep == GruntStep.Stagger ? "빈틈" : "";
                targetText.color = stateColor;
            }

            // 알림.
            for (int i = 0; events != null && i < events.Count; i++)
            {
                string message = null;
                var color = main;
                switch (events[i].Kind)
                {
                    case SkirmishEventKind.GruntPerfectBlock: message = "완벽 막기"; color = strike; break;
                    case SkirmishEventKind.GruntBlocked: message = "막음"; break;
                    case SkirmishEventKind.MechHit: message = "피격"; color = danger; break;
                    case SkirmishEventKind.GuardClosed: message = "가드 굳힘"; color = warn; break;
                    case SkirmishEventKind.GuardBroken: message = "가드 파괴"; color = strike; break;
                    case SkirmishEventKind.PunchBounced: message = "튕겨남"; color = warn; break;
                    case SkirmishEventKind.GruntDown: message = "격파"; color = strike; break;
                }
                if (message == null) continue;
                alertText.text = message;
                alertText.color = color;
                alertUntil = now + alertSeconds;
            }
            float leftover = alertUntil - now;
            var fade = alertText.color;
            fade.a = Mathf.Clamp01(leftover / 0.25f) * 0.95f;
            alertText.color = fade;

            if (!Application.isPlaying) Canvas.ForceUpdateCanvases();
        }
    }
}
