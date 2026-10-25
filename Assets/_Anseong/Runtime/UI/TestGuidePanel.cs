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

        [SerializeField, Tooltip("전투 판정(M1-05). 비어 있으면 시작할 때 씬에서 한 번 찾는다. 없으면 펀치 분류만 보여 준다.")]
        CombatDirector combat;

        [SerializeField, Tooltip("가드 판정(M1-06). 비어 있으면 시작할 때 씬에서 한 번 찾는다. 없으면 가드 줄을 쓰지 않는다.")]
        GuardDriver guard;

        [SerializeField, Tooltip("글자를 쓸 TextMesh. 메뉴가 만든다.")]
        TextMesh text;

        [SerializeField, Tooltip("조절·검증 메뉴. 열려 있는 동안 안내판 글자를 숨긴다(10/25: 메뉴와 겹침). 비어 있으면 시작할 때 한 번 찾는다.")]
        TuningMenu tuningMenu;

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
        float nextOffsetCheck;
        Vector3 shownOffset;
        string lastCombat = string.Empty;
        bool boostOpen;
        int guardKey = -1;
        int readyKey = -1;

        void Start()
        {
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (classifier == null) classifier = FindAnyObjectByType<PunchClassifierDriver>();
            if (combat == null) combat = FindAnyObjectByType<CombatDirector>();
            if (guard == null) guard = FindAnyObjectByType<GuardDriver>();
            if (tuningMenu == null) tuningMenu = FindAnyObjectByType<TuningMenu>();
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
            if (combat != null) combat.Cue += OnCombat;
        }

        void OnDestroy()
        {
            if (calibration != null)
            {
                calibration.Notice -= OnNotice;
                calibration.Completed -= ResetCounts;
            }
            if (classifier != null) classifier.StrikeCompleted -= OnStrike;
            if (combat != null) combat.Cue -= OnCombat;
        }

        void OnCombat(CombatEvent e)
        {
            var side = e.Side == PilotSide.Left ? "왼" : "오";
            switch (e.Cue)
            {
                case CombatCue.Hit:
                    lastCombat = $"<color=#5BE37D>명중</color> {side} {KindName(e.Kind)} -{e.Damage:F0}%" + (e.Quality == HitQuality.Assisted ? " (보정)" : " (접촉)");
                    break;
                case CombatCue.Miss:
                    lastCombat = $"<color=#FF6B6B>헛침</color> {side} {KindName(e.Kind)}";
                    break;
                case CombatCue.ComboBroken:
                    lastCombat += $"  <color=#FFD24D>연계 끊김: {BreakName(e.Break)}</color>";
                    break;
                case CombatCue.Boost:
                    lastCombat = $"<color=#BF4DFF>부스트!</color> -{e.Damage:F0}%";
                    break;
                case CombatCue.BossDefeated:
                    lastCombat = "<color=#5BE37D>보스 HP 0</color>";
                    break;
                case CombatCue.BoostReady:
                case CombatCue.BoostMissed:
                case CombatCue.ComboStep:
                    break;
                default:
                    return;
            }
            boostOpen = combat.Resolver.Boost.IsOpen;
            dirty = true;
        }

        static string BreakName(ComboBreak reason)
        {
            switch (reason)
            {
                case ComboBreak.Miss: return "헛침";
                case ComboBreak.WrongTurn: return "차례 아님";
                case ComboBreak.Timeout: return "시간 초과";
                case ComboBreak.Damaged: return "맞음";
                default: return reason.ToString();
            }
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

        MeshRenderer textRenderer;

        /// <summary>조절·검증 메뉴가 열려 있으면 글자를 숨긴다. 숨겼으면 true.</summary>
        bool HideUnderMenu()
        {
            if (textRenderer == null) textRenderer = text.GetComponent<MeshRenderer>();
            var hide = tuningMenu != null && tuningMenu.IsOpen;
            if (textRenderer != null && textRenderer.enabled == hide) textRenderer.enabled = !hide;
            return hide;
        }

        void Update()
        {
            if (HideUnderMenu()) return;
            // 단계나 손별 저장 상태가 바뀔 때만 다시 쓴다(매 프레임 문자열을 만들지 않는다).
            var key = (int)calibration.Stage * 4 + (calibration.IsCaptured(PilotSide.Left) ? 1 : 0) + (calibration.IsCaptured(PilotSide.Right) ? 2 : 0);
            if (key != stateKey)
            {
                stateKey = key;
                dirty = true;
            }
            // 몸 이동 보정 값은 0.25초마다, 1cm 넘게 바뀌었을 때만 다시 쓴다.
            if (calibration.IsComplete && Time.unscaledTime >= nextOffsetCheck)
            {
                nextOffsetCheck = Time.unscaledTime + 0.25f;
                if ((calibration.BodyOffset - shownOffset).sqrMagnitude > 0.0001f) dirty = true;
                // 가드 판정 이유가 바뀌었을 때만(같은 0.25초 간격).
                if (guard != null)
                {
                    var g = (int)guard.State(PilotSide.Left).Reason * 16 + (int)guard.State(PilotSide.Right).Reason;
                    if (g != guardKey)
                    {
                        guardKey = g;
                        dirty = true;
                    }
                }
                // 손별 "다음 펀치 준비" 상태가 바뀌었을 때(10/25: 펀치가 처음 몇 번 뒤 안 세지는 원인을 헤드셋 안에서 보게).
                if (classifier != null)
                {
                    var phaseKey = (int)classifier.Classifier(PilotSide.Left).Phase * 8 + (int)classifier.Classifier(PilotSide.Right).Phase;
                    if (phaseKey != readyKey || Waiting(PilotSide.Left) || Waiting(PilotSide.Right))
                    {
                        readyKey = phaseKey;
                        dirty = true;
                    }
                }
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
            builder.Append("치고 나면 손을 가드 자리로 잠깐 돌려야 다음 동작을 셉니다\n");
            shownOffset = calibration.BodyOffset;
            builder.Append("몸 이동 보정 ");
            if (calibration.BodyFollowActive)
                builder.Append("<color=#5BE37D>켜짐</color>  보정 뒤 옮긴 거리 좌우 ").Append(Cm(shownOffset.x))
                    .Append(" 앞뒤 ").Append(Cm(shownOffset.z)).Append(" 높이 ").Append(Cm(shownOffset.y));
            else builder.Append("<color=#FF6B6B>꺼짐</color>");
            builder.Append("\n\n");
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
                if (combat != null && combat.Resolver != null) DrawCombat();
                if (guard != null) DrawGuard();
                builder.Append("다음 펀치    ");
                ReadyText(PilotSide.Left);
                builder.Append("  /  ");
                ReadyText(PilotSide.Right);
                builder.Append('\n');
                builder.Append("──────────────\n");
                for (var i = 0; i < recentCount; i++) builder.Append(recent[i]).Append('\n');
            }
            builder.Append('\n').Append(recalibrateHint);
        }

        void DrawGuard()
        {
            builder.Append("가드  ");
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                builder.Append(h == 0 ? "왼 " : "  /  오 ");
                if (!guard.UsesHand(side))
                {
                    builder.Append('-');
                    continue;
                }
                var state = guard.State(side);
                if (state.IsGuarding) builder.Append("<color=#5BE37D>가드</color>");
                else builder.Append("<color=#999999>").Append(GuardReasonName(state.Reason)).Append("</color>");
            }
            builder.Append('\n');
        }

        static string GuardReasonName(GuardReason reason)
        {
            switch (reason)
            {
                case GuardReason.NotCalibrated: return "보정 전";
                case GuardReason.Untracked: return "추적 끊김";
                case GuardReason.OutOfZone: return "가드 자리 밖";
                case GuardReason.BehindFace: return "얼굴보다 뒤";
                case GuardReason.ForearmTilted: return "팔뚝이 누움";
                default: return reason.ToString();
            }
        }

        void DrawCombat()
        {
            var r = combat.Resolver;
            builder.Append("보스 HP ").Append(r.Boss.Current.ToString("F0")).Append("%   연계 ").Append(r.Combo.Step).Append("/3");
            if (r.Combo.Step > 0)
                builder.Append(" (다음: ").Append(r.Combo.NextSide == PilotSide.Left ? "왼손 " : "오른손 ").Append(r.Combo.Step == 2 ? "훅" : "잽").Append(')');
            if (boostOpen) builder.Append("   <color=#BF4DFF>부스트: 트리거!</color>");
            builder.Append('\n');
            if (combat.Boss == null) builder.Append("<color=#FF6B6B>보스 없음: 모든 공격이 헛침</color>\n");
            if (lastCombat.Length > 0) builder.Append(lastCombat).Append('\n');
        }

        bool Waiting(PilotSide side)
        {
            var phase = classifier.Classifier(side).Phase;
            return calibration.UsesHand(side) && (phase == PunchPhase.WaitingForNeutral || phase == PunchPhase.Recovering);
        }

        void ReadyText(PilotSide side)
        {
            if (!calibration.UsesHand(side))
            {
                builder.Append('-');
                return;
            }
            var punch = classifier.Classifier(side);
            switch (punch.Phase)
            {
                case PunchPhase.Ready: builder.Append("<color=#5BE37D>준비</color>"); break;
                case PunchPhase.Moving: builder.Append("휘두르는 중"); break;
                case PunchPhase.TrackingLost: builder.Append("<color=#FF6B6B>추적 끊김</color>"); break;
                default:
                    if (punch.RearmsByRetract)
                    {
                        builder.Append("<color=#FFD24D>조금 당기거나 멈추기</color>");
                        break;
                    }
                    var o = punch.NeutralOffset;
                    builder.Append("<color=#FFD24D>가드 자리로</color> ");
                    builder.Append(o.z > 0.1f ? "당기기" : Mathf.Abs(o.x) > Mathf.Abs(o.y) ? (o.x > 0f ? "왼쪽으로" : "오른쪽으로") : (o.y > 0f ? "내리기" : "올리기"));
                    break;
            }
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
                case PunchReason.RobotLagged: return "로봇 팔이 덜 뻗음";
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
