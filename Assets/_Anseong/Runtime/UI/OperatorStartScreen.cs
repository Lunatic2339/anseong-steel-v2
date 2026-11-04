using Anseong.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Anseong.UI
{
    /// <summary>
    /// 운영자 시작 화면(M3-01, 07_milestones M3-01). PC 모니터에만 보이는 화면(Screen Space Overlay, 헤드셋에는 안 보임)에서
    /// 1인 양팔 / 호스트 / 클라이언트 + 호스트 IP를 고르고 시작한다. 마지막 모드·IP는 <see cref="SessionBootstrap"/>이 기억한다.
    /// 시작한 뒤에는 모니터 왼쪽 위에 상태 한 줄과 "다시 고르기" 버튼만 남는다.
    /// 키보드: 1·2·3 = 모드(IP 칸에 쓰는 중이 아닐 때), Enter = 시작.
    /// 화면은 시작할 때 코드로 만든다(씬·프리팹에 UI를 저장하지 않음). 메뉴 'Add Network Session'이 세션 오브젝트에 붙인다.
    /// </summary>
    public class OperatorStartScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("네트워크 세션. 비어 있으면 시작할 때 한 번 찾는다.")]
        SessionBootstrap session;

        [SerializeField, Tooltip("한글이 나오는 OS 글꼴(앞에서부터 있는 것).")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [SerializeField, Tooltip("시작 화면 기준 해상도(가로 x 세로). 모니터 크기에 맞게 늘어난다.")]
        Vector2 referenceResolution = new Vector2(1920f, 1080f);

        static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.94f);
        static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        static readonly Color SelectedColor = new Color(0.85f, 0.45f, 0.12f, 1f);
        static readonly Color StartColor = new Color(0.18f, 0.55f, 0.28f, 1f);
        static readonly Color ProblemColor = new Color(1f, 0.45f, 0.4f, 1f);

        Font font;
        GameObject panel, statusBar;
        readonly Button[] modeButtons = new Button[3];
        Text info, problem, status;
        InputField ipField;
        SessionMode selected;
        bool built;

        void Start()
        {
            if (session == null) session = FindAnyObjectByType<SessionBootstrap>();
            if (session == null)
            {
                Debug.LogWarning("[OperatorStartScreen] SessionBootstrap이 없어 시작 화면을 끕니다. 'Anseong → Setup → Add Network Session'을 실행하세요.", this);
                enabled = false;
                return;
            }
            font = Font.CreateDynamicFontFromOSFont(osFontNames, 32);
            Build();
            selected = session.LastMode;
            ipField.text = session.Address;
            session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (session != null) session.StateChanged -= Refresh;
        }

        void Update()
        {
            if (!built) return;
            if (statusBar.activeSelf) RefreshStatus();
            if (!panel.activeSelf) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var typing = ipField.isFocused;
            if (!typing)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Select(SessionMode.SoloBothArms);
                else if (keyboard.digit2Key.wasPressedThisFrame) Select(SessionMode.Host);
                else if (keyboard.digit3Key.wasPressedThisFrame) Select(SessionMode.Client);
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) StartSession();
        }

        void Select(SessionMode mode)
        {
            selected = mode;
            Refresh();
        }

        void StartSession()
        {
            session.Begin(selected, ipField.text);
            Refresh();
        }

        void Refresh()
        {
            if (!built) return;
            var waiting = session.WaitingForOperator;
            panel.SetActive(waiting);
            statusBar.SetActive(!waiting);
            for (var i = 0; i < modeButtons.Length; i++)
                modeButtons[i].GetComponent<Image>().color = i == (int)selected ? SelectedColor : ButtonColor;
            ipField.gameObject.SetActive(selected == SessionMode.Client);
            switch (selected)
            {
                case SessionMode.Host:
                    var ips = SessionBootstrap.LocalAddresses();
                    info.text = "PC-A · 왼팔 조종사. PC-B를 한 대 받습니다.\nPC-B에 이 PC의 IP를 알려 주세요: " +
                                (ips.Count > 0 ? string.Join("  /  ", ips) : "(못 읽음: cmd에서 ipconfig)") + $"   포트 {session.Port}";
                    break;
                case SessionMode.Client:
                    info.text = "PC-B · 오른팔 조종사. PC-A 시작 화면에 나온 IP를 넣으세요.";
                    break;
                default:
                    info.text = "이 PC 하나로 두 팔을 모두 조종합니다(개발·시연 대체용). 다른 PC는 받지 않습니다.";
                    break;
            }
            problem.text = session.Problem;
            RefreshStatus();
        }

        void RefreshStatus()
        {
            var text = SessionRules.ModeLabel(session.Mode);
            if (session.Connecting) text += $" · {session.Address} 찾는 중…";
            else if (session.Running) text += $" · 조종사 {session.PilotCount}/{SessionRules.MaxPilots}";
            if (!string.IsNullOrEmpty(session.Problem)) text += " · " + session.Problem;
            if (status.text != text) status.text = text;
        }

        // ── 화면 만들기 ──

        void Build()
        {
            EnsureEventSystem();
            var canvasGo = new GameObject("Operator Screen", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            panel = Box(canvasGo.transform, "Start Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 640f), PanelColor);
            Label(panel.transform, "안성제철 · 운영자 시작", new Vector2(0f, 250f), new Vector2(1000f, 70f), 48, Color.white);

            string[] names = { "1  1인 양팔", "2  호스트 (PC-A · 왼팔)", "3  클라이언트 (PC-B · 오른팔)" };
            for (var i = 0; i < 3; i++)
            {
                var mode = (SessionMode)i;
                modeButtons[i] = MakeButton(panel.transform, names[i], new Vector2(-350f + 350f * i, 140f), new Vector2(330f, 80f), ButtonColor, () => Select(mode));
            }

            info = Label(panel.transform, string.Empty, new Vector2(0f, 20f), new Vector2(1000f, 110f), 28, new Color(0.9f, 0.9f, 0.9f));
            ipField = MakeInputField(panel.transform, new Vector2(0f, -70f), new Vector2(500f, 70f));
            MakeButton(panel.transform, "시작  (Enter)", new Vector2(0f, -170f), new Vector2(400f, 80f), StartColor, StartSession);
            problem = Label(panel.transform, string.Empty, new Vector2(0f, -260f), new Vector2(1040f, 80f), 26, ProblemColor);

            statusBar = Box(canvasGo.transform, "Status Bar", new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(900f, 56f), PanelColor, new Vector2(0f, 1f));
            status = Label(statusBar.transform, string.Empty, new Vector2(-90f, 0f), new Vector2(700f, 50f), 24, Color.white, TextAnchor.MiddleLeft);
            MakeButton(statusBar.transform, "다시 고르기", new Vector2(360f, 0f), new Vector2(160f, 44f), ButtonColor, () => session.End(), 22);
            built = true;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        static GameObject Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        Button MakeButton(Transform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 26)
        {
            var go = Box(parent, "Button", new Vector2(0.5f, 0.5f), position, size, color);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var label = Label(go.transform, text, Vector2.zero, size, fontSize, Color.white);
            ((RectTransform)label.transform).sizeDelta = size;
            return button;
        }

        InputField MakeInputField(Transform parent, Vector2 position, Vector2 size)
        {
            var go = Box(parent, "Host IP", new Vector2(0.5f, 0.5f), position, size, new Color(0.95f, 0.95f, 0.95f, 1f));
            var placeholder = Label(go.transform, "호스트 IP (예: 192.168.0.10)", Vector2.zero, size - new Vector2(30f, 10f), 28, new Color(0.45f, 0.45f, 0.45f));
            placeholder.fontStyle = FontStyle.Italic;
            var text = Label(go.transform, string.Empty, Vector2.zero, size - new Vector2(30f, 10f), 30, Color.black);
            text.supportRichText = false;
            var field = go.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 15;
            field.contentType = InputField.ContentType.Standard;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }
    }
}
