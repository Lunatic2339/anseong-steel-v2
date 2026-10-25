using System.IO;
using System.Text;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anseong.UI
{
    /// <summary>
    /// 헤드셋을 쓴 채 조절값을 바꾸는 인게임 메뉴(개발용, 선우 10/25). 씬에서 <see cref="ITunable"/>을 구현한 컴포넌트를 시작할 때 한 번 모아 항목을 만든다.
    /// 오른손 B로 열고 닫고, 스틱 위아래로 고르고, 좌우로 바꾸고, 오른손 A로 지금 항목을 시작값(씬·프리팹 값)으로 되돌린다(키보드: F1, 방향키, Backspace).
    /// 바꾼 값은 persistentDataPath/tuning.txt에 바로 저장되고 다음 Play에도 적용된다. 그 파일을 올리면 코드 기본값으로 옮긴다.
    /// 메뉴가 열려 있는 동안 스틱은 메뉴에만 쓴다(M2에서 이동 입력이 생기면 그쪽에서 <see cref="IsOpen"/>을 본다).
    /// Add Pilot Input 메뉴가 XR 카메라 앞 왼쪽에 만든다. 에디터·개발 빌드에서만 동작한다.
    /// </summary>
    public class TuningMenu : MonoBehaviour
    {
#pragma warning disable 0414, 0649
        [SerializeField, Tooltip("글자를 쓸 TextMesh. 메뉴가 만든다.")]
        TextMesh text;

        [SerializeField, Tooltip("스틱을 읽을 손 입력. 비어 있으면 시작할 때 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Tooltip("한글을 쓰려고 시작할 때 이 OS 글꼴로 바꾼다(앞에서부터 있는 것).")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [SerializeField, Tooltip("메뉴 열기·닫기 버튼(Input System 경로, 여러 개).")]
        string[] toggleBindings = { "<XRController>{RightHand}/secondaryButton", "<Keyboard>/f1" };

        [SerializeField, Tooltip("지금 항목을 시작값으로 되돌리는 버튼.")]
        string[] resetBindings = { "<XRController>{RightHand}/primaryButton", "<Keyboard>/backspace" };

        [SerializeField, Range(3, 21), Tooltip("한 번에 보여 줄 줄 수.")]
        int visibleRows = 11;

        [SerializeField, Range(0.2f, 0.9f), Tooltip("스틱을 이만큼 밀어야 한 칸 움직인다.")]
        float stickThreshold = 0.6f;

        [SerializeField, Min(0.05f), Tooltip("스틱을 밀고 있으면 이 시간(초) 뒤부터 계속 움직인다.")]
        float repeatDelay = 0.4f;

        [SerializeField, Min(0.02f), Tooltip("계속 움직이는 간격(초).")]
        float repeatInterval = 0.08f;

        [SerializeField, Min(0.1f), Tooltip("값을 바꾸는 스틱을 이 시간(초) 넘게 밀고 있으면 한 번에 5칸씩.")]
        float fastAfter = 1.5f;

        [SerializeField, Tooltip("저장 파일 이름(persistentDataPath 아래).")]
        string fileName = "tuning.txt";

        [SerializeField, Tooltip("시작할 때 저장 파일 값을 적용한다. 끄면 늘 코드 기본값으로 시작.")]
        bool loadSaved = true;
#pragma warning restore 0414, 0649

        public bool IsOpen { get; private set; }

#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        // 릴리스 빌드에서는 메뉴를 띄우지 않는다.
        void Awake() => gameObject.SetActive(false);
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly TuningList list = new TuningList();
        readonly StringBuilder builder = new StringBuilder(1024);
        InputAction toggleAction, resetAction;
        MeshRenderer textRenderer;
        // [0] = 위아래(고르기), [1] = 좌우(바꾸기)
        readonly int[] heldDirection = new int[2];
        readonly float[] heldSince = new float[2];
        readonly float[] nextRepeat = new float[2];
        string path, status = string.Empty;
        bool dirty = true;

        // 검증 순서(M1-04_검증_조절값.md)대로 묶음을 보여 준다.
        static readonly string[] GroupOrder = { "몸 이동", "손목", "펀치 인식", "그립", "묵직함", "공격 보정", "연계·부스트", "보스 HP", "가드", "팔 매핑", "허리" };

        void Start()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (text == null)
            {
                enabled = false;
                return;
            }
            textRenderer = text.GetComponent<MeshRenderer>();
            UseOsFont();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour is ITunable tunable) tunable.AddTuning(list);
            list.SortByGroup(GroupOrder);

            path = Path.Combine(Application.persistentDataPath, fileName);
            if (loadSaved && File.Exists(path))
            {
                try
                {
                    var applied = list.Apply(File.ReadAllText(path));
                    if (applied > 0) Debug.Log($"[TuningMenu] 저장한 조절값 {applied}개 적용: {path}", this);
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"[TuningMenu] 조절값 파일을 읽지 못했습니다: {e.Message}", this);
                }
            }
            Debug.Log($"[TuningMenu] 조절 항목 {list.Items.Count}개. 오른손 B(키보드 F1)로 열기. 저장 위치: {path}", this);

            toggleAction = MakeAction("Tuning Toggle", toggleBindings);
            resetAction = MakeAction("Tuning Reset", resetBindings);
            SetOpen(false);
        }

        static InputAction MakeAction(string name, string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings)
                if (!string.IsNullOrEmpty(binding)) action.AddBinding(binding);
            action.Enable();
            return action;
        }

        void OnDestroy()
        {
            toggleAction?.Dispose();
            resetAction?.Dispose();
        }

        void UseOsFont()
        {
            if (osFontNames == null || osFontNames.Length == 0) return;
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, text.fontSize > 0 ? text.fontSize : 32);
            if (font == null) return;
            text.font = font;
            if (textRenderer != null) textRenderer.sharedMaterial = font.material;
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            if (textRenderer != null) textRenderer.enabled = open;
            dirty = true;
        }

        void Update()
        {
            if (toggleAction.WasPressedThisFrame()) SetOpen(!IsOpen);
            if (!IsOpen || list.Items.Count == 0) return;

            var now = Time.unscaledTime;
            var stick = ReadStick();
            var move = Step(0, -Axis(stick.y), now);
            if (move != 0)
            {
                list.Move(move);
                dirty = true;
            }
            var adjust = Step(1, Axis(stick.x), now);
            if (adjust != 0)
            {
                var fast = now - heldSince[1] >= fastAfter;
                if (list.Adjust(adjust * (fast ? 5 : 1))) Changed();
                dirty = true;
            }
            if (resetAction.WasPressedThisFrame() && list.ResetCurrent()) Changed();

            if (!dirty) return;
            dirty = false;
            Redraw();
        }

        /// <summary>두 스틱 중 더 많이 민 쪽(축마다). 키보드 방향키도 더한다.</summary>
        Vector2 ReadStick()
        {
            var value = Vector2.zero;
            if (input != null)
            {
                var left = input.Get(PilotSide.Left).Stick;
                var right = input.Get(PilotSide.Right).Stick;
                value.x = Mathf.Abs(left.x) > Mathf.Abs(right.x) ? left.x : right.x;
                value.y = Mathf.Abs(left.y) > Mathf.Abs(right.y) ? left.y : right.y;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed) value.y = 1f;
                if (keyboard.downArrowKey.isPressed) value.y = -1f;
                if (keyboard.rightArrowKey.isPressed) value.x = 1f;
                if (keyboard.leftArrowKey.isPressed) value.x = -1f;
            }
            return value;
        }

        int Axis(float v) => v >= stickThreshold ? 1 : v <= -stickThreshold ? -1 : 0;

        /// <summary>민 방향이 바뀐 순간 한 칸, 계속 밀면 repeatDelay 뒤부터 repeatInterval마다 한 칸.</summary>
        int Step(int axis, int direction, float now)
        {
            if (direction != heldDirection[axis])
            {
                heldDirection[axis] = direction;
                heldSince[axis] = now;
                nextRepeat[axis] = now + repeatDelay;
                return direction;
            }
            if (direction == 0 || now < nextRepeat[axis]) return 0;
            nextRepeat[axis] = now + repeatInterval;
            return direction;
        }

        void Changed()
        {
            dirty = true;
            try
            {
                File.WriteAllText(path, "# 안성제철 인게임 조절값. 이 파일을 총괄 채팅에 올리면 코드 기본값으로 옮긴다.\n" + list.Serialize(), new UTF8Encoding(false));
                status = "저장됨";
            }
            catch (IOException e)
            {
                status = "저장 실패: " + e.Message;
            }
        }

        void Redraw()
        {
            builder.Length = 0;
            builder.Append("<b>조절 메뉴</b>   B 닫기 · 스틱 ↑↓ 고르기 ←→ 바꾸기 · A 시작값\n");
            var items = list.Items;
            var half = visibleRows / 2;
            var first = Mathf.Clamp(list.Selected - half, 0, Mathf.Max(0, items.Count - visibleRows));
            var last = Mathf.Min(items.Count, first + visibleRows);
            string group = null;
            for (var i = first; i < last; i++)
            {
                var item = items[i];
                if (item.Group != group)
                {
                    group = item.Group;
                    builder.Append("<color=#7FB2FF>[").Append(group).Append("]</color>\n");
                }
                var selected = i == list.Selected;
                var changed = !Mathf.Approximately(item.Value, item.Default);
                builder.Append(selected ? "<color=#FFD24D>> " : "   ").Append(item.Label).Append("  ")
                    .Append(changed ? "<color=#5BE37D>" : string.Empty).Append(item.ValueText()).Append(changed ? "</color>" : string.Empty);
                if (selected && changed) builder.Append("  (시작 ").Append(DefaultText(item)).Append(')');
                if (selected) builder.Append("</color>");
                builder.Append('\n');
            }
            builder.Append(list.Selected + 1).Append(" / ").Append(items.Count);
            if (status.Length > 0) builder.Append("   ").Append(status);
            text.text = builder.ToString();
        }

        static string DefaultText(TuningItem item)
        {
            if (item.IsChoice) return item.Choices[Mathf.Clamp(Mathf.RoundToInt(item.Default), 0, item.Choices.Length - 1)];
            return item.Default.ToString("F" + item.Decimals, System.Globalization.CultureInfo.InvariantCulture);
        }
#endif
    }
}
