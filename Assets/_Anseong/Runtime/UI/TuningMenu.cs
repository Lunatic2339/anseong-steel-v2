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
    /// 바꾼 값은 tuning.txt(에디터: 프로젝트 Logs/Anseong, 빌드: persistentDataPath)에 바로 저장되고 다음 Play에도 적용된다. 그 파일을 올리면 코드 기본값으로 옮긴다.
    /// 메뉴가 열려 있는 동안 스틱은 메뉴에만 쓴다(M2에서 이동 입력이 생기면 그쪽에서 <see cref="IsOpen"/>을 본다).
    /// 열면 설정 화면처럼 뒤 화면이 어두워지고 시야 가운데에 표가 뜬다(10/25: 시야 끝 글자는 안 보임). 닫으면 다 숨긴다.
    /// Add Pilot Input 메뉴가 XR 카메라 아래에 만든다. 에디터·개발 빌드에서만 동작한다.
    /// </summary>
    public class TuningMenu : MonoBehaviour
    {
#pragma warning disable 0414, 0649
        [SerializeField, Tooltip("열 때 켜고 닫을 때 끄는 화면 묶음(어둡게 하는 막 + 표). 메뉴가 만든다.")]
        GameObject screen;

        [SerializeField, Tooltip("제목 줄 TextMesh.")]
        TextMesh title;

        [SerializeField, Tooltip("항목 이름 열 TextMesh(왼쪽).")]
        TextMesh labels;

        [SerializeField, Tooltip("값 열 TextMesh(오른쪽). 이름 열과 줄 수를 맞춘다.")]
        TextMesh values;

        [SerializeField, Tooltip("아래 안내 줄 TextMesh(조작법·저장 상태).")]
        TextMesh footer;

        [SerializeField, Tooltip("스틱을 읽을 손 입력. 비어 있으면 시작할 때 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Tooltip("한글을 쓰려고 시작할 때 이 OS 글꼴로 바꾼다(앞에서부터 있는 것).")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [SerializeField, Tooltip("메뉴 열기·닫기 버튼(Input System 경로, 여러 개).")]
        string[] toggleBindings = { "<XRController>{RightHand}/secondaryButton", "<Keyboard>/f1" };

        [SerializeField, Tooltip("지금 항목을 시작값으로 되돌리는 버튼.")]
        string[] resetBindings = { "<XRController>{RightHand}/primaryButton", "<Keyboard>/backspace" };

        [SerializeField, Range(3, 21), Tooltip("한 번에 보여 줄 줄 수(묶음 제목 포함).")]
        int visibleRows = 12;

        [SerializeField, Range(0.2f, 0.9f), Tooltip("스틱을 이만큼 밀어야 한 칸 움직인다.")]
        float stickThreshold = 0.6f;

        [SerializeField, Min(0.05f), Tooltip("스틱을 밀고 있으면 이 시간(초) 뒤부터 계속 움직인다.")]
        float repeatDelay = 0.4f;

        [SerializeField, Min(0.02f), Tooltip("계속 움직이는 간격(초).")]
        float repeatInterval = 0.08f;

        [SerializeField, Min(0.1f), Tooltip("값을 바꾸는 스틱을 이 시간(초) 넘게 밀고 있으면 한 번에 5칸씩.")]
        float fastAfter = 1.5f;

        [SerializeField, Tooltip("저장 파일 이름(LogFolder.Root 아래: 에디터는 프로젝트 Logs/Anseong, 빌드는 persistentDataPath).")]
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
        readonly StringBuilder valueBuilder = new StringBuilder(512);
        // [0] = 위아래(고르기), [1] = 좌우(바꾸기)
        readonly int[] heldDirection = new int[2];
        readonly float[] heldSince = new float[2];
        readonly float[] nextRepeat = new float[2];
        string path, status = string.Empty;
        bool dirty = true;

        // 검증 순서(M1-04_검증_조절값.md)대로 묶음을 보여 준다.
        static readonly string[] GroupOrder = { "몸 이동", "손목", "펀치 인식", "그립", "묵직함", "조종석 흔들림", "허리", "팔 매핑", "공격 보정", "연계·부스트", "보스 반응", "보스 HP", "소리·진동", "가드" };

        void Start()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (screen == null || labels == null || values == null)
            {
                Debug.LogWarning("[TuningMenu] 화면 구성이 없어 조절 메뉴를 끕니다. 'Anseong → Setup → Rebuild Robot + Pilot Input'을 다시 실행하세요.", this);
                enabled = false;
                return;
            }
            UseOsFont();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour is ITunable tunable) tunable.AddTuning(list);
            list.SortByGroup(GroupOrder);

            path = Path.Combine(LogFolder.Root, fileName);
            MoveOldSave();
            if (loadSaved && File.Exists(path))
            {
                try
                {
                    var applied = list.Apply(File.ReadAllText(path));
                    // 어떤 값이 시작값과 다른지 기록에 남긴다(10/28: 예전에 꺼 둔 공격 보정이 남아 있어 헷갈렸다).
                    if (applied > 0) Debug.Log($"[TuningMenu] 저장한 조절값 {applied}개 적용: {path} | 시작값과 다른 값: " +
                        list.Serialize().Replace("\n", " ; "), this);
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
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, labels.fontSize > 0 ? labels.fontSize : 32);
            if (font == null) return;
            foreach (var mesh in new[] { title, labels, values, footer })
            {
                if (mesh == null) continue;
                mesh.font = font;
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = font.material;
            }
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            screen.SetActive(open);
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
                Directory.CreateDirectory(Path.GetDirectoryName(path));
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
            valueBuilder.Length = 0;
            var items = list.Items;
            var first = FirstVisible(items);
            string group = first > 0 ? items[first - 1].Group : null;
            var rows = 0;
            // 고른 항목 묶음의 검증 안내(무엇을 하고 무엇을 볼지)를 맨 위에. 값 열은 그 줄을 비워 둔다.
            var hint = list.Current != null ? list.Hint(list.Current.Group) : null;
            if (hint != null)
            {
                builder.Append("<color=#C8C8C8>").Append(hint).Append("</color>\n\n");
                var lines = 2;
                foreach (var c in hint) if (c == '\n') lines++;
                for (var i = 0; i < lines; i++) valueBuilder.Append('\n');
                rows += lines;
            }
            for (var i = first; i < items.Count && rows < visibleRows; i++)
            {
                var item = items[i];
                if (item.Group != group)
                {
                    group = item.Group;
                    builder.Append("<color=#7FB2FF><b>").Append(group).Append("</b></color>\n");
                    valueBuilder.Append('\n');
                    if (++rows >= visibleRows) break;
                }
                var selected = i == list.Selected;
                var changed = !Mathf.Approximately(item.Value, item.Default);
                builder.Append(selected ? "<color=#FFD24D><b>  > " : "     ").Append(item.Label).Append(selected ? "</b></color>" : string.Empty).Append('\n');
                var color = !changed || selected ? null
                    : !item.IsCheck ? "#5BE37D"
                    : Mathf.RoundToInt(item.Value) == TuningList.CheckOk ? "#5BE37D" : "#FF6B6B";
                valueBuilder.Append(selected ? "<color=#FFD24D><b>◀ " : "   ")
                    .Append(color != null ? "<color=" + color + ">" : string.Empty).Append(item.ValueText()).Append(color != null ? "</color>" : string.Empty)
                    .Append(selected ? " ▶</b></color>" : string.Empty);
                if (changed && !item.IsCheck) valueBuilder.Append("   <color=#999999>처음 ").Append(DefaultText(item)).Append("</color>");
                valueBuilder.Append('\n');
                rows++;
            }
            labels.text = builder.ToString();
            values.text = valueBuilder.ToString();
            list.CountChecks(out var checkTotal, out var checkOk, out var checkProblem);
            if (title != null)
                title.text = $"<b>조절 · 검증 메뉴</b>   <color=#999999>{list.Selected + 1} / {items.Count}</color>" +
                             (checkTotal > 0 ? $"   확인 <color=#5BE37D>OK {checkOk}</color> · <color=#FF6B6B>문제 {checkProblem}</color> / {checkTotal}" : string.Empty);
            if (footer != null)
                footer.text = "B 닫기    스틱 ↑↓ 고르기    ←→ 바꾸기    A 처음 값으로\n<color=#999999>바꾼 값은 자동 저장(다음 Play에도 남음)" +
                              (status.Length > 0 ? " · " + status : string.Empty) + "</color>";
        }

        /// <summary>고른 항목이 가운데쯤 오도록 첫 줄을 정한다(묶음 제목 줄도 센다).</summary>
        int FirstVisible(System.Collections.Generic.IReadOnlyList<TuningItem> items)
        {
            var first = list.Selected;
            var used = 1;
            while (first > 0 && used < visibleRows / 2)
            {
                used += items[first - 1].Group != items[first].Group ? 2 : 1;
                first--;
            }
            return first;
        }

        static string DefaultText(TuningItem item)
        {
            if (item.IsChoice) return item.Choices[Mathf.Clamp(Mathf.RoundToInt(item.Default), 0, item.Choices.Length - 1)];
            return item.Default.ToString("F" + item.Decimals, System.Globalization.CultureInfo.InvariantCulture);
        }
#endif

        // 예전 저장 위치(persistentDataPath)에만 파일이 있으면 새 위치로 한 번 옮겨 둔다(10/28, 기록을 프로젝트 Logs 폴더로 모음).
        void MoveOldSave()
        {
            var old = Path.Combine(Application.persistentDataPath, fileName);
            if (old == path || File.Exists(path) || !File.Exists(old)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(old, path);
            }
            catch (IOException) { }
            catch (System.UnauthorizedAccessException) { }
        }
    }
}
