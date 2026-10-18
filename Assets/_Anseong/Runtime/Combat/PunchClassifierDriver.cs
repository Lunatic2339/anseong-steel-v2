using System;
using System.Diagnostics;
using Anseong.Pilot;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Anseong.Combat
{
    /// <summary>
    /// 조종사 손 궤적을 팔마다 잽·훅·어퍼컷·해당 없음으로 나눈다(M1-04). 판정은 사람 손 기준(보정한 몸 정면, m)이고,
    /// 동작이 끝날 때마다 <see cref="StrikeCompleted"/>를 낸다. 명중·피해는 M1-05가 이 이벤트를 받아 정한다.
    /// 개발 빌드에서는 동작마다 CSV 한 줄을 남기고, 로봇 주먹 옆 표시 색을 잠깐 바꾼다.
    /// </summary>
    public class PunchClassifierDriver : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField, Tooltip("손 입력. 비어 있으면 시작할 때 씬에서 한 번 찾는다.")]
        PilotHandInput input;

        [SerializeField, Tooltip("개인 보정. 비어 있으면 시작할 때 씬에서 한 번 찾는다. 보정이 끝날 때마다 중립(가드 위치)을 다시 잡는다.")]
        PilotCalibrationFlow calibration;

        [Header("분류")]
        [SerializeField, Tooltip("잽·훅 기준값(사람 손 m). 01_combat_rules.md 초기값.")]
        PunchClassifierSettings settings = new PunchClassifierSettings();

        [Header("개발용 (에디터·개발 빌드에서만)")]
        [SerializeField, Tooltip("동작마다 persistentDataPath/logs/strikes_<날짜시각>.csv에 한 줄 남긴다.")]
        bool writeCsv = true;

        [SerializeField, Tooltip("동작마다 Console에 한 줄 남긴다.")]
        bool logToConsole = true;

        [SerializeField, Tooltip("왼팔·오른팔 주먹 옆 표시(메뉴가 만든다). 분류 결과마다 색이 잠깐 바뀐다. 릴리스 빌드에서는 꺼진다.")]
        Renderer[] indicators = new Renderer[2];

        [SerializeField, Min(0f), Tooltip("표시 색이 유지되는 시간(초).")]
        float indicatorSeconds = 0.5f;

        [SerializeField] Color jabColor = new Color(0.2f, 0.55f, 1f);
        [SerializeField] Color hookColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] Color uppercutColor = new Color(0.75f, 0.3f, 1f);
        [SerializeField] Color noneColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField, Tooltip("동작이 없을 때 표시 색.")]
        Color idleColor = new Color(1f, 1f, 1f, 0.35f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly PunchClassifier[] classifiers = new PunchClassifier[2];
        readonly float[] indicatorUntil = new float[2];
        readonly PunchPhase[] lastPhase = new PunchPhase[2];
        readonly float[] headAtStart = new float[2];
        MaterialPropertyBlock block;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StrikeLog log;
#endif

        /// <summary>동작 하나가 끝났다(잽·훅이 아니어도). 공격인지는 <see cref="PunchResult.IsAttack"/>.</summary>
        public event Action<PilotSide, PunchResult> StrikeCompleted;

        public PunchClassifier Classifier(PilotSide side) => classifiers[(int)side];

        /// <summary>CSV 명중 열 값을 정하는 함수(M1-05 전투 판정이 넣는다). 비어 있으면 빈칸.</summary>
        public Func<PilotSide, PunchResult, string> HitColumn { get; set; }

        void Awake()
        {
            classifiers[0] = new PunchClassifier(true, settings);
            classifiers[1] = new PunchClassifier(false, settings);
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            foreach (var indicator in indicators)
                if (indicator != null) indicator.gameObject.SetActive(false);
#endif
        }

        void Start()
        {
            if (input == null) input = FindAnyObjectByType<PilotHandInput>();
            if (calibration == null) calibration = FindAnyObjectByType<PilotCalibrationFlow>();
            if (input == null || calibration == null)
            {
                Debug.LogWarning("[PunchClassifierDriver] 씬에서 PilotHandInput·PilotCalibrationFlow를 찾지 못해 펀치를 분류하지 않습니다. " +
                                 "XR Origin에 'Anseong → Setup → Add Pilot Input'을 실행하세요.", this);
                enabled = false;
                return;
            }
            calibration.Completed += ResetNeutrals;
            if (calibration.IsComplete) ResetNeutrals();
            block = new MaterialPropertyBlock();
            for (var i = 0; i < indicators.Length; i++) SetIndicator(i, idleColor);
        }

        void OnDestroy()
        {
            if (calibration != null) calibration.Completed -= ResetNeutrals;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            log?.Dispose();
            log = null;
#endif
        }

        void ResetNeutrals()
        {
            for (var h = 0; h < 2; h++)
            {
                var arm = calibration.Get((PilotSide)h);
                if (arm != null && arm.IsComplete) classifiers[h].SetNeutral(arm.ToBody(arm.Guard));
            }
        }

        void Update()
        {
            var now = Time.timeAsDouble;
            for (var h = 0; h < 2; h++)
            {
                var side = (PilotSide)h;
                var arm = calibration.Get(side);
                if (!input.IsArmHand(side) || arm == null || !arm.IsComplete) continue;
                var sample = input.Get(side);
                var head = calibration.TryGetHeadPosition(out var headPosition) ? arm.ToBody(calibration.CompensateHand(headPosition)).z : 0f;
                var completed = classifiers[h].Sample(arm.ToBody(calibration.CompensateHand(sample.Position)), sample.Tracked, sample.AttackIntent, now, out var result);
                var phase = classifiers[h].Phase;
                // 휘두르기 시작한 프레임의 머리 위치를 잡아 두고 끝날 때 얼마나 앞으로 갔는지 남긴다(기록용).
                if (phase == PunchPhase.Moving && lastPhase[h] != PunchPhase.Moving) headAtStart[h] = head;
                lastPhase[h] = phase;
                if (completed)
                {
                    result.HeadForward = head - headAtStart[h];
                    Report(side, result);
                }
                if (indicatorUntil[h] > 0f && Time.time >= indicatorUntil[h])
                {
                    indicatorUntil[h] = 0f;
                    SetIndicator(h, idleColor);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            log?.Pump();
#endif
        }

        void Report(PilotSide side, PunchResult result)
        {
            var color = result.Kind == PunchKind.Jab ? jabColor
                : result.Kind == PunchKind.Hook ? hookColor
                : result.Kind == PunchKind.Uppercut ? uppercutColor : noneColor;
            SetIndicator((int)side, color);
            indicatorUntil[(int)side] = Time.time + indicatorSeconds;
            // 명중 판정(M1-05)이 먼저 결과를 받고 나서 기록해야 CSV 명중 열이 맞는다.
            StrikeCompleted?.Invoke(side, result);
            WriteDebug(side, result);
        }

        void SetIndicator(int index, Color color)
        {
            if (index >= indicators.Length || indicators[index] == null || block == null) return;
            indicators[index].GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            indicators[index].SetPropertyBlock(block);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void WriteDebug(PilotSide side, PunchResult r)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (writeCsv)
            {
                if (log == null)
                {
                    log = new StrikeLog(StrikeLog.DefaultPath(Application.persistentDataPath, DateTime.Now));
                    Debug.Log($"[PunchClassifierDriver] 펀치 기록: {log.Path}", this);
                }
                log.Add(side, r, HitColumn != null ? HitColumn(side, r) : string.Empty);
            }
            if (logToConsole)
            {
                var m = r.Measure;
                Debug.Log($"[Punch] {side} #{r.StrikeId} {r.Kind} ({r.Reason}) 공격 {(r.IsAttack ? "O" : "X")} " +
                          $"그립 {(r.GripHeld ? 1 : 0)} 추적 {(r.Tracked ? 1 : 0)} | 좌우/전진 {m.SideRatio:F2} 직진 {m.Straightness:F2} " +
                          $"바깥 {m.Outset * 100f:F0}cm 스윕 {m.InwardSweep * 100f:F0}cm 호/현 {m.ArcRatio:F2} 올림 {m.Rise * 100f:F0}cm 전진 {m.Forward * 100f:F0}cm 머리 {r.HeadForward * 100f:F0}cm " +
                          $"속도 {m.PeakSpeed:F1}m/s {m.Duration:F2}s", this);
            }
#endif
        }
    }
}
