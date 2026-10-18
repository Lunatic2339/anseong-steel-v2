using System.Text;
using Anseong.Combat;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.UI
{
    /// <summary>
    /// 헤드셋 안에서 보는 테스트 안내판(개발용). 처음 하는 팀원도 헤드셋을 벗지 않고 따라 할 수 있게
    /// 보정 중에는 지금 단계 안내와 손별 저장 상태를, 보정이 끝나면 펀치 분류 결과(종류별 개수, 최근 동작)를 띄운다.
    /// Add Pilot Input 메뉴가 XR 카메라 앞 아래쪽에 만든다. 에디터·개발 빌드에서만 보이고 릴리스 빌드에서는 꺼진다.
    /// </summary>
    public class TestGuidePanel : MonoBehaviour
    {
        // 릴리스 빌드에서는 아래 값을 쓰지 않는다(직렬화 레이아웃을 맞추려고 #if 밖에 둔다).
#pragma warning disable 0414, 0649
        [SerializeField, Tooltip("보정 흐름. 비어 있으면 시작할 때 씬에서 한 번 찾는다.")]
        PilotCalibrationFlow calibration;

        [SerializeField, Tooltip("펀치 분류. 비어 있으면 시작할 때 씬에서 한 번 찾는다. 없으면 보정 안내만 한다.")]
        PunchClassifierDriver classifier;

        [SerializeField, Tooltip("글자를 쓸 TextMesh. 메뉴가 만든다.")]
        TextMesh text;

        [SerializeField, Tooltip("한글을 쓰려고 시작할 때 이 OS 글꼴로 바꾼다(앞에서부터 있는 것). 비우면 TextMesh 글꼴 그대로.")]
        string[] osFontNames = { "Malgun Gothic", "맑은 고딕", "Arial" };

        [SerializeField, Range(1, 10), Tooltip("최근 펀치를 몇 개 보여 줄지.")]
        int recentLines = 4;

        [SerializeField, Tooltip("펀치 측정값(좌우/전진, 스윕, 올림 등)도 보여 준다.")]
        bool showMeasures = true;

        [SerializeField, Tooltip("다시 보정하는 방법(안내 문구). 입력 설정의 Dev/Recalibrate와 맞춘다.")]
        string recalibrateHint = "다시 보정: 왼손 Y 버튼 (키보드 R)";
#pragma warning restore 0414, 0649

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // [팔, 종류] 개수. 종류 순서 = PunchKind.
        readonly int[,] counts = new int[2, 4];
        readonly int[] attacks = new int[2];
        readonly StringBuilder builder = new StringBuilder(1024);
        string[] recent;
        int recentCount;
        string lastNotice = string.Empty;
        int stateKey = -1;
        bool dirty = true;

        void Start()
        {
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (classifier == null) classifier = FindAnyObjectByType<PunchClassifierDriver>();
            if (text == null || calibration == null)
            {
                Debug.LogWarning("[TestGuidePanel] 글자 칸이나 보정 흐름이 없어 안내판을 끕니다. 'Anseong → Setup → Add Pilot Input'을 다시 실행하세요.", this);
                gameObject.SetActive(false);
                return;
            }
            UseOsFont();
            recent = new string[recentLines];
            calibration.Notice += OnNotice;
            calibration.Completed += ResetCounts;
            if (classifier != null) classifier.StrikeCompleted += OnStrike;
        }

        void OnDestroy()
        {
            if (calibration != null)
            {
                calibration.Notice -= OnNotice;
                calibration.Completed -= ResetCounts;
            }
            if (classifier != null) classifier.StrikeCompleted -= OnStrike;
        }

        void UseOsFont()
        {
            if (osFontNames == null || osFontNames.Length == 0) return;
            var font = Font.CreateDynamicFontFromOSFont(osFontNames, text.fontSize > 0 ? text.fontSize : 32);
            if (font == null) return;
            text.font = font;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = font.material;
        }

        void Update()
        {
            // 단계나 손별 저장 상태가 바뀔 때만 다시 쓴다(매 프레임 문자열을 만들지 않는다).
            var key = (int)calibration.Stage * 4 + (calibration.IsCaptured(PilotSide.Left) ? 1 : 0) + (calibration.IsCaptured(PilotSide.Right) ? 2 : 0);
            if (key != stateKey)
            {
                stateKey = key;
                dirty = true;
            }
            if (!dirty) return;
            dirty = false;
            Redraw();
        }

        void OnNotice(string message)
        {
            // 요약처럼 여러 줄인 것은 첫 줄만.
            var newline = message.IndexOf('\n');
            lastNotice = newline >= 0 ? message.Substring(0, newline) : message;
            dirty = true;
        }

        void ResetCounts()
        {
            System.Array.Clear(counts, 0, counts.Length);
            System.Array.Clear(attacks, 0, attacks.Length);
            recentCount = 0;
            dirty = true;
        }

        void OnStrike(PilotSide side, PunchResult r)
        {
            var h = (int)side;
            counts[h, (int)r.Kind]++;
            if (r.IsAttack) attacks[h]++;

            var line = new StringBuilder(160);
            line.Append(side == PilotSide.Left ? "왼" : "오").Append(" #").Append(r.StrikeId).Append(' ');
            line.Append("<color=").Append(KindColor(r.Kind)).Append('>').Append(KindName(r.Kind)).Append("</color> ");
            line.Append(r.IsAttack ? "공격 O" : !r.Tracked ? "추적 끊김" : !r.GripHeld ? "그립 없음" : "공격 X");
            if (r.Kind == PunchKind.None) line.Append(" (").Append(ReasonName(r.Reason)).Append(')');
            if (showMeasures)
            {
                var m = r.Measure;
                line.Append("\n   좌우/전진 ").Append(m.SideRatio.ToString("F2"))
                    .Append("  직진 ").Append(m.Straightness.ToString("F2"))
                    .Append("  바깥 ").Append(Cm(m.Outset))
                    .Append("  스윕 ").Append(Cm(m.InwardSweep))
                    .Append("  호/현 ").Append(m.ArcRatio.ToString("F2"))
                    .Append("  올림 ").Append(Cm(m.Rise))
                    .Append("  전진 ").Append(Cm(m.Forward));
            }
            // 새 줄을 맨 위에.
            for (var i = Mathf.Min(recentCount, recent.Length - 1); i > 0; i--) recent[i] = recent[i - 1];
            recent[0] = line.ToString();
            recentCount = Mathf.Min(recentCount + 1, recent.Length);
            dirty = true;
        }

        void Redraw()
        {
            builder.Length = 0;
            if (!calibration.IsComplete) DrawCalibration();
            else DrawPunches();
            text.text = builder.ToString();
        }

        void DrawCalibration()
        {
            builder.Append("<b>보정</b>  (양손 그립을 쥐었다 놓으면 저장)\n\n");
            builder.Append(PilotCalibrationFlow.Instruction(calibration.Stage)).Append("\n\n");
            if (calibration.Stage != CalibrationStage.NotStarted)
            {
                Hand("왼손", PilotSide.Left);
                builder.Append("    ");
                Hand("오른손", PilotSide.Right);
                builder.Append('\n');
            }
            if (lastNotice.Length > 0) builder.Append("\n<color=#BBBBBB>").Append(lastNotice).Append("</color>\n");
            builder.Append('\n').Append(recalibrateHint);
        }

        void Hand(string label, PilotSide side)
        {
            if (!calibration.UsesHand(side)) return;
            builder.Append(label).Append(' ');
            builder.Append(calibration.IsCaptured(side) ? "<color=#5BE37D>저장됨</color>" : "<color=#FFD24D>대기</color>");
        }

        void DrawPunches()
        {
            builder.Append("<b>보정 완료</b>  그립을 쥔 채 잽·훅·어퍼컷을 쳐 보세요\n");
            builder.Append("치고 나면 손을 가드 자리로 잠깐 돌려야 다음 동작을 셉니다\n\n");
            if (classifier == null)
            {
                builder.Append("(펀치 분류 없음: Build Player Robot Prefab을 다시 실행하세요)\n");
            }
            else
            {
                builder.Append("             왼  /  오\n");
                Row("<color=#4D8CFF>잽</color>         ", PunchKind.Jab);
                Row("<color=#FF8C1A>훅</color>         ", PunchKind.Hook);
                Row("<color=#BF4DFF>어퍼컷</color>    ", PunchKind.Uppercut);
                Row("<color=#999999>해당 없음</color> ", PunchKind.None);
                builder.Append("공격 인정    ").Append(attacks[0]).Append("  /  ").Append(attacks[1]).Append('\n');
                builder.Append("──────────────\n");
                for (var i = 0; i < recentCount; i++) builder.Append(recent[i]).Append('\n');
            }
            builder.Append('\n').Append(recalibrateHint);
        }

        void Row(string label, PunchKind kind)
        {
            builder.Append(label).Append(counts[0, (int)kind]).Append("  /  ").Append(counts[1, (int)kind]).Append('\n');
        }

        static string Cm(float meters) => (meters * 100f).ToString("F0") + "cm";

        static string KindName(PunchKind kind)
        {
            switch (kind)
            {
                case PunchKind.Jab: return "잽";
                case PunchKind.Hook: return "훅";
                case PunchKind.Uppercut: return "어퍼컷";
                default: return "해당 없음";
            }
        }

        static string KindColor(PunchKind kind)
        {
            switch (kind)
            {
                case PunchKind.Jab: return "#4D8CFF";
                case PunchKind.Hook: return "#FF8C1A";
                case PunchKind.Uppercut: return "#BF4DFF";
                default: return "#999999";
            }
        }

        static string ReasonName(PunchReason reason)
        {
            switch (reason)
            {
                case PunchReason.TooShort: return "너무 짧음";
                case PunchReason.InsufficientForward: return "앞으로 덜 나감";
                case PunchReason.TooSlow: return "너무 느림";
                case PunchReason.VerticalMotion: return "위아래 움직임";
                case PunchReason.Ambiguous: return "애매함";
                case PunchReason.TooLong: return "너무 김";
                case PunchReason.CapacityExceeded: return "샘플 초과";
                default: return reason.ToString();
            }
        }
#else
        void Awake()
        {
            gameObject.SetActive(false);
        }
#endif
    }
}
