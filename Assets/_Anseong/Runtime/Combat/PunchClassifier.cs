using System;
using UnityEngine;

namespace Anseong.Combat
{
    public enum PunchKind { None, Jab, Hook, Uppercut }

    /// <summary>분류 결과의 이유. 잽·훅이면 Jab·Hook, 아니면 왜 아닌지.</summary>
    public enum PunchReason
    {
        Jab,
        Hook,
        Uppercut,
        /// <summary>궤적이 너무 짧다(0.06초 미만이거나 샘플 3개 미만).</summary>
        TooShort,
        /// <summary>전진 거리가 기준보다 짧다.</summary>
        InsufficientForward,
        TooSlow,
        /// <summary>위아래 움직임이 크다(가드 올리기·내리기).</summary>
        VerticalMotion,
        /// <summary>잽도 훅도 아니다(비스듬히 휘두름).</summary>
        Ambiguous,
        /// <summary>한 동작이 너무 길다.</summary>
        TooLong,
        /// <summary>한 동작의 샘플이 너무 많다.</summary>
        CapacityExceeded,
    }

    public enum PunchPhase { WaitingForNeutral, Ready, Moving, Recovering, TrackingLost }

    [Serializable]
    public class PunchClassifierSettings
    {
        [Tooltip("잽: 좌우폭 ÷ 전진 거리 최대값.")]
        public float maxJabSideRatio = 0.55f;

        [Tooltip("잽: 직진도(처음→가장 앞 거리 ÷ 지나온 길이) 최소값. 1차 헤드셋 로그에서 잽이 0.86~0.99로 나와 기준선에 걸리지 않게 0.80(01 초기값 0.86).")]
        public float minJabStraightness = 0.80f;

        [Tooltip("훅: 가드 위치보다 바깥으로 준비한 거리 최소값(m, 사람 손). 0 = 가드에서 바로 안쪽으로 휘둘러도 훅(기본). " +
                 "가드를 넓게 잡으면 손이 이미 바깥에 있어 더 빼지 않으므로 0으로 둔다(01 규칙 초기값 10cm는 1차 헤드셋에서 훅이 거의 안 잡혀 바꿈).")]
        public float minHookOutset = 0f;

        [Tooltip("훅: 바깥 끝에서 안쪽으로 휘두른 거리 최소값(m).")]
        public float minHookSweep = 0.22f;

        [Tooltip("훅: 호 길이 ÷ 현 길이 최소값(휘어진 정도).")]
        public float minHookArcRatio = 1.02f;

        [Tooltip("어퍼컷: 가장 낮은 지점에서 올려친 높이 최소값(m).")]
        public float minUppercutRise = 0.18f;

        [Tooltip("어퍼컷: 올려친 높이 ÷ 전진 거리 최소값. 앞으로보다 위로 더 가야 어퍼컷.")]
        public float minUppercutRiseRatio = 1.0f;

        [Tooltip("어퍼컷: 좌우폭 ÷ 올려친 높이 최대값. 넘으면 옆으로 휘두른 것.")]
        public float maxUppercutSideRatio = 0.6f;

        [Tooltip("동작 시작: 위로 올라가는 속도 최소값(m/s). 어퍼컷 시작.")]
        public float departUpSpeed = 0.5f;

        [Tooltip("잽·훅 공통: 중립에서 앞으로 나간 거리 최소값(m). 어퍼컷에는 쓰지 않는다.")]
        public float minForward = 0.18f;

        [Tooltip("중간 판정(휘두르는 도중 종류 미리 보기, M1-05 공격 보정용): 이만큼(m) 앞으로 나가면 판단을 시작한다. 끝 판정의 최소 전진보다 작다.")]
        public float peekMinForward = 0.12f;

        [Tooltip("위아래 폭 ÷ 전진 거리 최대값. 넘으면 가드 올리기·내리기로 본다.")]
        public float maxVerticalRatio = 0.7f;

        [Tooltip("켜면 위아래 폭을 전진 거리와 좌우 폭 중 큰 쪽과 비교한다(옆으로 크게 도는 훅이 '위아래 움직임'으로 버려지지 않게). " +
                 "끄면 전진 거리와만 비교한다(이전 동작).")]
        public bool verticalAgainstWidestSpan = true;

        [Tooltip("동작 시작 속도(m/s). 이보다 빨라야 휘두름이 시작된다.")]
        public float startSpeed = 0.75f;

        [Tooltip("동작 시작: 앞으로 나가는 속도 최소값(m/s).")]
        public float departForwardSpeed = 0.25f;

        [Tooltip("동작 시작: 바깥에서 준비 없이 안쪽으로 가는 속도 최소값(m/s). 훅을 옆에서 바로 시작할 때.")]
        public float departInwardSpeed = 0.35f;

        [Tooltip("재장전: 친 뒤 손이 보정한 가드 위치에서 좌우·위아래로 이만큼(m) 안에 돌아와야 다음 동작을 센다. " +
                 "10/25 헤드셋: 12cm(앞뒤 포함)는 가드로 돌아오는 자리가 조금만 달라져도 다음 펀치를 영영 못 세서 넓혔다.")]
        public float neutralRadius = 0.25f;

        [Tooltip("재장전: 앞뒤로는 가드 위치보다 이만큼(m) 앞까지만 돌아오면 된다(가드보다 몸 쪽으로 더 당긴 것은 괜찮다).")]
        public float neutralForwardBand = 0.10f;

        [Tooltip("재장전: 중립 반경 안에 머무를 시간(초).")]
        public float neutralDwell = 0.15f;

        [Tooltip("멈춤 속도(m/s). 이보다 느리게 stopDwell 동안 있으면 동작이 끝난다.")]
        public float stopSpeed = 0.20f;

        [Tooltip("멈춤 판정 시간(초).")]
        public float stopDwell = 0.10f;

        [Tooltip("손을 거둬들이기 시작하면 동작을 끝낸다: 가장 앞에서 이만큼(m) 물러났을 때.")]
        public float retractDistance = 0.065f;

        [Tooltip("한 동작 최대 길이(초).")]
        public float maxStrokeSeconds = 1.25f;

        [Tooltip("샘플 사이가 이보다(초) 길면(렉) 그 동작을 버리고 중립부터 다시 센다.")]
        public float maxSampleGap = 0.15f;

        [Tooltip("이보다 빠른 손 속도(m/s)는 추적 튐으로 보고 그 동작을 버린다.")]
        public float maxTrackingSpeed = 15f;
    }

    /// <summary>궤적 측정값. 길이는 사람 손 m.</summary>
    public struct PunchMeasure
    {
        /// <summary>좌우폭 ÷ 전진(잽 판정).</summary>
        public float SideRatio;
        /// <summary>직진도(잽 판정).</summary>
        public float Straightness;
        /// <summary>바깥 준비 거리(훅 판정).</summary>
        public float Outset;
        /// <summary>안쪽 스윕 거리(훅 판정).</summary>
        public float InwardSweep;
        /// <summary>호 ÷ 현(훅 판정).</summary>
        public float ArcRatio;
        /// <summary>가장 낮은 지점 뒤로 올라간 높이(어퍼컷 판정).</summary>
        public float Rise;
        public float Forward;
        public float PeakSpeed;
        public float Duration;
    }

    /// <summary>끝난 동작 하나.</summary>
    public struct PunchResult
    {
        /// <summary>이 팔에서 몇 번째 동작인지(1부터). 한 동작 = 한 번.</summary>
        public int StrikeId;
        public PunchKind Kind;
        public PunchReason Reason;
        public PunchMeasure Measure;
        /// <summary>동작 내내 공격 의도(그립 + 추적)가 있었다.</summary>
        public bool GripHeld;
        /// <summary>동작 내내 추적이 유효했다. 끊기면 그 자리에서 동작을 끝내고 false.</summary>
        public bool Tracked;
        public double StartTime, EndTime;
        /// <summary>동작 중 머리가 몸 정면으로 움직인 거리(m). 분류에는 쓰지 않는 기록용, 드라이버가 채운다.</summary>
        public float HeadForward;

        /// <summary>공격으로 센다(잽·훅 + 그립 + 추적). 명중은 M1-05가 따로 본다.</summary>
        public bool IsAttack => Kind != PunchKind.None && GripHeld && Tracked;
    }

    /// <summary>
    /// 사람 손 궤적 하나를 잽·훅·어퍼컷·해당 없음으로 나눈다(잽·훅은 기존 레포 PunchTrajectoryRecognizer를 옮김, 어퍼컷은 새로 추가).
    /// 좌표는 보정한 몸 기준(어깨 원점, +X 오른쪽, +Y 위, +Z 몸 정면, m). 중립은 보정한 가드 위치.
    /// 프레임마다 모든 샘플을 넣는다(빠른 펀치 샘플을 버리지 않는다).
    /// 친 뒤 중립 반경 안에 잠깐 머물러야 다음 동작을 센다(재장전).
    /// </summary>
    public sealed class PunchClassifier
    {
        const int Capacity = 512;
        // 기존 값: 중립에서 이만큼은 나가야 시작, 이만큼 앞으로 나가야 옆 출발 인정, 이 속도로 물러나면 거둬들임.
        const float DepartMinOffset = 0.035f, SideDepartMinForward = 0.015f, RetractSpeed = 0.30f, MinDuration = 0.06f;

        readonly PunchClassifierSettings settings;
        readonly float inwardSign;
        readonly Vector3[] points = new Vector3[Capacity];
        Vector3 neutral, previous;
        double previousTime, began, quietSince = -1, homeSince = -1;
        float peakSpeed, maxForward;
        int count, peakForwardIndex, returnStart = -1, strikeCount;
        bool hasPrevious, intentThroughout;

        /// <param name="leftArm">왼팔이면 몸 안쪽이 +X, 오른팔이면 −X.</param>
        public PunchClassifier(bool leftArm, PunchClassifierSettings settings)
        {
            inwardSign = leftArm ? 1f : -1f;
            this.settings = settings ?? new PunchClassifierSettings();
            Phase = PunchPhase.TrackingLost;
        }

        public PunchPhase Phase { get; private set; }

        /// <summary>휘두르는 중이면 지금 동작의 번호(끝나면 결과의 StrikeId와 같다). 아니면 0.</summary>
        public int CurrentStrikeId => Phase == PunchPhase.Moving ? strikeCount + 1 : 0;

        /// <summary>
        /// 휘두르는 도중 지금까지의 궤적으로 종류를 미리 본다(끝 판정과 같은 규칙, 최소 전진만 peekMinForward).
        /// 동작 내내 공격 의도가 있었고 종류가 정해졌으면 true. 끝 판정과 다를 수 있다(뒤에 휘어 들어오면 잽 → 훅).
        /// </summary>
        public bool Peek(out PunchKind kind)
        {
            kind = PunchKind.None;
            if (Phase != PunchPhase.Moving || count < 3 || !intentThroughout) return false;
            kind = Evaluate(count - 1, previousTime, settings.peekMinForward).Kind;
            return kind != PunchKind.None;
        }

        /// <summary>마지막 샘플의 손 − 중립(몸 기준, m). 안내판이 "가드 자리로 돌아오세요"를 띄울 때 쓴다.</summary>
        public Vector3 NeutralOffset { get; private set; }

        /// <summary>지금 손이 재장전 자리 안인지(다음 동작을 셀 수 있는 자리).</summary>
        public bool AtNeutral(Vector3 offset) => offset.z <= settings.neutralForwardBand
            && new Vector2(offset.x, offset.y).magnitude <= settings.neutralRadius;
        public PunchResult Last { get; private set; }
        public int CompletedCount => strikeCount;

        /// <summary>보정이 끝날 때마다 중립(몸 기준 가드 위치)을 넣는다. 진행 중인 동작은 버린다.</summary>
        public void SetNeutral(Vector3 bodyGuard)
        {
            neutral = bodyGuard;
            Discard(PunchPhase.WaitingForNeutral);
        }

        /// <summary>
        /// 샘플 하나. 동작이 끝났으면 true와 결과.
        /// tracked = 이 손 추적이 유효함, intent = 공격 의도(그립 + 추적, AttackIntentGate).
        /// </summary>
        public bool Sample(Vector3 position, bool tracked, bool intent, double time, out PunchResult result)
        {
            result = default(PunchResult);
            if (!tracked || !Finite(position) || double.IsNaN(time) || double.IsInfinity(time))
            {
                // 휘두르던 중이면 그 동작을 공격 아님으로 끝낸다(분류는 남긴다).
                if (Phase == PunchPhase.Moving)
                {
                    result = Finish(previousTime, null, -1, false);
                    Discard(PunchPhase.TrackingLost);
                    return true;
                }
                Discard(PunchPhase.TrackingLost);
                return false;
            }
            if (Phase == PunchPhase.TrackingLost) Phase = PunchPhase.WaitingForNeutral;
            if (!hasPrevious)
            {
                previous = position;
                previousTime = time;
                hasPrevious = true;
                return false;
            }
            var dt = (float)(time - previousTime);
            if (dt <= 0f) return false;
            var velocity = (position - previous) / dt;
            if (dt > settings.maxSampleGap || velocity.magnitude > settings.maxTrackingSpeed)
            {
                // 렉이나 추적 튐: 속도 기록이 깨졌으니 이 동작은 버리고 중립부터 다시.
                Discard(PunchPhase.WaitingForNeutral);
                previous = position;
                previousTime = time;
                hasPrevious = true;
                return false;
            }

            var completed = false;
            var offset = position - neutral;
            NeutralOffset = offset;
            switch (Phase)
            {
                case PunchPhase.WaitingForNeutral:
                case PunchPhase.Recovering:
                    if (AtNeutral(offset) && velocity.magnitude <= settings.stopSpeed)
                    {
                        if (homeSince < 0) homeSince = time;
                        if (time - homeSince >= settings.neutralDwell) Phase = PunchPhase.Ready;
                    }
                    else homeSince = -1;
                    break;

                case PunchPhase.Ready:
                    // 옆으로 준비하는 건 괜찮지만 가드를 올리고 내리거나 뒤로 빼는 건 시작이 아니다.
                    var departure = velocity.z > settings.departForwardSpeed
                        || velocity.y > settings.departUpSpeed
                        || (-velocity.x * inwardSign > settings.departInwardSpeed && offset.z > SideDepartMinForward);
                    if (velocity.magnitude >= settings.startSpeed && offset.magnitude > DepartMinOffset && departure)
                    {
                        count = 0;
                        points[count++] = previous;
                        points[count++] = position;
                        began = previousTime;
                        peakSpeed = velocity.magnitude;
                        maxForward = offset.z;
                        peakForwardIndex = 1;
                        quietSince = -1;
                        returnStart = -1;
                        intentThroughout = intent;
                        Phase = PunchPhase.Moving;
                    }
                    break;

                case PunchPhase.Moving:
                    intentThroughout &= intent;
                    peakSpeed = Mathf.Max(peakSpeed, velocity.magnitude);
                    if (count == points.Length)
                    {
                        result = Finish(time, PunchReason.CapacityExceeded);
                        completed = true;
                        break;
                    }
                    points[count++] = position;
                    if (offset.z >= maxForward)
                    {
                        maxForward = offset.z;
                        peakForwardIndex = count - 1;
                    }
                    var retracting = velocity.z < -RetractSpeed && -velocity.z > Mathf.Abs(velocity.x) * 0.8f
                        && Vector3.Dot(velocity, -offset) > 0.02f;
                    if (retracting && returnStart < 0) returnStart = count - 2;
                    if (!retracting) returnStart = -1;

                    if (velocity.magnitude < settings.stopSpeed)
                    {
                        if (quietSince < 0) quietSince = time;
                        if (time - quietSince >= settings.stopDwell)
                        {
                            result = Finish(time);
                            completed = true;
                            break;
                        }
                    }
                    else quietSince = -1;
                    if (returnStart >= 0 && maxForward - offset.z > settings.retractDistance)
                    {
                        result = Finish(time, null, returnStart);
                        completed = true;
                        break;
                    }
                    if (time - began > settings.maxStrokeSeconds)
                    {
                        result = Finish(time, PunchReason.TooLong);
                        completed = true;
                    }
                    break;
            }
            previous = position;
            previousTime = time;
            return completed;
        }

        PunchResult Finish(double time, PunchReason? rejected = null, int last = -1, bool tracked = true)
        {
            var result = Evaluate(last < 0 ? count - 1 : last, time);
            if (rejected.HasValue)
            {
                result.Kind = PunchKind.None;
                result.Reason = rejected.Value;
            }
            result.StrikeId = ++strikeCount;
            result.GripHeld = intentThroughout && tracked;
            result.Tracked = tracked;
            result.StartTime = began;
            result.EndTime = time;
            Last = result;
            Phase = PunchPhase.Recovering;
            homeSince = quietSince = -1;
            return result;
        }

        void Discard(PunchPhase phase)
        {
            hasPrevious = false;
            count = 0;
            homeSince = quietSince = -1;
            Phase = phase;
        }

        PunchResult Evaluate(int last, double time) => Evaluate(last, time, settings.minForward);

        PunchResult Evaluate(int last, double time, float minForward)
        {
            var result = new PunchResult { Kind = PunchKind.None, Reason = PunchReason.TooShort };
            var m = new PunchMeasure { PeakSpeed = peakSpeed, Duration = (float)(time - began), ArcRatio = 1f };
            if (last < 2 || m.Duration < MinDuration)
            {
                result.Measure = m;
                return result;
            }

            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f, minInward = 0f, maxInward = float.NegativeInfinity, path = 0f;
            float lowest = float.PositiveInfinity;
            int outer = 0, inner = 0;
            var jabEnd = Mathf.Min(last, peakForwardIndex);
            for (var i = 0; i <= last; i++)
            {
                var p = points[i] - neutral;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                lowest = Mathf.Min(lowest, p.y);
                m.Rise = Mathf.Max(m.Rise, p.y - lowest);
                m.Forward = Mathf.Max(m.Forward, p.z);
                var x = p.x * inwardSign;
                // 가장 바깥 지점(outer)과 그 뒤 가장 안쪽 지점(inner).
                if (x < minInward) { minInward = x; outer = i; maxInward = x; inner = i; }
                if (i >= outer && x >= maxInward) { maxInward = x; inner = i; }
                if (i > 0 && i <= jabEnd) path += Vector3.Distance(points[i], points[i - 1]);
            }
            m.Outset = -minInward;
            m.InwardSweep = Mathf.Max(0f, maxInward - minInward);
            var arc = 0f;
            for (var i = outer + 1; i <= inner; i++) arc += Vector3.Distance(points[i], points[i - 1]);
            var chord = Vector3.Distance(points[outer], points[inner]);
            m.ArcRatio = chord > 0.01f ? arc / chord : 1f;
            m.Straightness = path > 0.01f ? Vector3.Distance(points[0], points[jabEnd]) / path : 0f;
            m.SideRatio = m.Forward > 1e-4f ? (maxX - minX) / m.Forward : float.PositiveInfinity;
            result.Measure = m;

            // 어퍼컷은 앞으로 덜 나가고 위아래로 크게 움직이므로 전진·수직 거름보다 먼저 본다.
            if (peakSpeed >= settings.startSpeed && m.Rise >= settings.minUppercutRise
                && m.Rise >= Mathf.Max(0f, m.Forward) * settings.minUppercutRiseRatio
                && maxX - minX <= m.Rise * settings.maxUppercutSideRatio)
            {
                result.Kind = PunchKind.Uppercut;
                result.Reason = PunchReason.Uppercut;
                return result;
            }
            if (m.Forward < minForward) { result.Reason = PunchReason.InsufficientForward; return result; }
            if (peakSpeed < settings.startSpeed) { result.Reason = PunchReason.TooSlow; return result; }
            var span = settings.verticalAgainstWidestSpan ? Mathf.Max(m.Forward, maxX - minX) : m.Forward;
            if (maxY - minY > span * settings.maxVerticalRatio) { result.Reason = PunchReason.VerticalMotion; return result; }

            // 안쪽으로 크게 휘어 들어오면 훅이 먼저다(휘어진 22cm 스윕은 잽이 아니다). 그다음 곧게 나가면 잽.
            var hook = m.Outset >= settings.minHookOutset && m.InwardSweep >= settings.minHookSweep && m.ArcRatio >= settings.minHookArcRatio;
            var jab = m.SideRatio <= settings.maxJabSideRatio && m.Straightness >= settings.minJabStraightness;
            if (hook)
            {
                result.Kind = PunchKind.Hook;
                result.Reason = PunchReason.Hook;
            }
            else if (jab)
            {
                result.Kind = PunchKind.Jab;
                result.Reason = PunchReason.Jab;
            }
            else result.Reason = PunchReason.Ambiguous;
            return result;
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
    }
}
