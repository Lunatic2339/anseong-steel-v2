using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class FinisherSettings
    {
        [Tooltip("팔 펴짐 비율(보정한 도달 거리 대비) 이상이어야 준비(01: 85%).")]
        [Range(0.5f, 1.2f)] public float minExtension = 0.85f;

        [Tooltip("정면에서 이 각도(°) 안으로 뻗어야 준비.")]
        [Range(5f, 90f)] public float maxAngleFromForward = 35f;

        [Tooltip("두 팔이 준비를 시작한 시간 차가 이 안(초)이어야 충전(01: 0.5).")]
        [Range(0.1f, 2f)] public float startWindow = 0.5f;

        [Tooltip("충전 시간(초, 01: 1.5).")]
        [Range(0.2f, 5f)] public float chargeSeconds = 1.5f;

        [Tooltip("광선 시간(초, 01: 3).")]
        [Range(0.5f, 10f)] public float beamSeconds = 3f;
    }

    public enum FinisherStep
    {
        None,
        ChargeStarted,
        ChargeReset,
        Fired,
        Finished,
    }

    /// <summary>
    /// 합동 필살기(01 "필살기", M2-05): 두 팔이 준비(정면으로 85% 이상 뻗고 버튼을 모두 누름, 추적 유효)를 0.5초 안에 시작하면 1.5초 충전,
    /// 충전 중 한 팔이라도 놓으면 처음부터, 다 차면 광선 3초 → 끝. 서버 시각으로만 진행. 순수 클래스.
    /// 팔 준비 여부는 그 팔 주인 PC가 사람 손으로 판정해 보고한다(<see cref="ArmReady"/>).
    /// </summary>
    public sealed class FinisherRules
    {
        readonly FinisherSettings settings;
        readonly bool[] ready = new bool[2];
        readonly double[] readySince = { double.NaN, double.NaN };
        double chargeStart = double.NaN, fireEnd = double.NaN;

        public FinisherRules(FinisherSettings settings = null)
        {
            this.settings = settings ?? new FinisherSettings();
        }

        public FinisherSettings Settings => settings;
        /// <summary>무릎 꿇은 보스에게만 쓸 수 있다(보스 두뇌가 켠다).</summary>
        public bool Enabled { get; set; }
        public bool Charging => !double.IsNaN(chargeStart);
        public bool Firing => !double.IsNaN(fireEnd);
        public bool Done { get; private set; }
        public double ChargeStart => chargeStart;
        public double FireEnd => fireEnd;

        /// <summary>충전 비율 0~1.</summary>
        public float Progress(double now) => Charging ? Mathf.Clamp01((float)((now - chargeStart) / settings.chargeSeconds)) : Firing || Done ? 1f : 0f;

        /// <summary>한 팔이 지금 준비 자세인지(사람 손 기준). 펴짐 비율·정면 각도·버튼·추적은 보고하는 쪽이 <see cref="IsArmReady"/>로 본다.</summary>
        public void ArmReady(PilotSide side, bool value, double now)
        {
            var h = (int)side;
            if (ready[h] == value) return;
            ready[h] = value;
            readySince[h] = value ? now : double.NaN;
        }

        public bool IsReady(PilotSide side) => ready[(int)side];

        public FinisherStep Update(double now)
        {
            if (!Enabled || Done) return FinisherStep.None;
            if (Firing)
            {
                if (now < fireEnd) return FinisherStep.None;
                fireEnd = double.NaN;
                Done = true;
                return FinisherStep.Finished;
            }
            var both = ready[0] && ready[1];
            if (Charging)
            {
                if (!both)
                {
                    chargeStart = double.NaN;
                    return FinisherStep.ChargeReset;
                }
                if (now - chargeStart < settings.chargeSeconds) return FinisherStep.None;
                chargeStart = double.NaN;
                fireEnd = now + settings.beamSeconds;
                return FinisherStep.Fired;
            }
            if (!both || Math.Abs(readySince[0] - readySince[1]) > settings.startWindow) return FinisherStep.None;
            chargeStart = Math.Max(readySince[0], readySince[1]);
            return FinisherStep.ChargeStarted;
        }

        public void Reset()
        {
            ready[0] = ready[1] = false;
            readySince[0] = readySince[1] = double.NaN;
            chargeStart = fireEnd = double.NaN;
            Done = false;
            Enabled = false;
        }

        /// <summary>
        /// 사람 손 기준 준비 판정(보고하는 PC). ratio = 보정한 도달 거리 대비 손 위치(어깨 기준, 로봇 정면 +Z).
        /// </summary>
        public static bool IsArmReady(Vector3 ratio, bool tracked, bool allButtons, FinisherSettings s)
        {
            if (!tracked || !allButtons) return false;
            if (ratio.magnitude < s.minExtension) return false;
            return Vector3.Angle(ratio, Vector3.forward) <= s.maxAngleFromForward;
        }
    }
}
