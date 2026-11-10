using UnityEngine;

namespace Anseong.Net
{
    /// <summary>
    /// 한 조종사 PC가 보내는 자세 한 장(M3-02). 팔 뼈 회전은 로봇 루트 기준, 머리·양손은 조종석 기준.
    /// 받는 PC는 자기 로봇 루트·조종석에 다시 붙이므로 두 PC의 로봇 위치가 조금 달라도 팔 모양은 같다.
    /// </summary>
    public struct PilotPose
    {
        /// <summary>보낸 시각(서버 시계, 초).</summary>
        public double Time;
        public Quaternion Upper, Lower, Hand;
        public Vector3 HeadPosition;
        public Quaternion HeadRotation;
        public Vector3 LeftHandPosition, RightHandPosition;
        public Quaternion LeftHandRotation, RightHandRotation;

        public static PilotPose Lerp(in PilotPose a, in PilotPose b, float t)
        {
            return new PilotPose
            {
                Time = a.Time + (b.Time - a.Time) * t,
                Upper = Quaternion.Slerp(a.Upper, b.Upper, t),
                Lower = Quaternion.Slerp(a.Lower, b.Lower, t),
                Hand = Quaternion.Slerp(a.Hand, b.Hand, t),
                HeadPosition = Vector3.Lerp(a.HeadPosition, b.HeadPosition, t),
                HeadRotation = Quaternion.Slerp(a.HeadRotation, b.HeadRotation, t),
                LeftHandPosition = Vector3.Lerp(a.LeftHandPosition, b.LeftHandPosition, t),
                RightHandPosition = Vector3.Lerp(a.RightHandPosition, b.RightHandPosition, t),
                LeftHandRotation = Quaternion.Slerp(a.LeftHandRotation, b.LeftHandRotation, t),
                RightHandRotation = Quaternion.Slerp(a.RightHandRotation, b.RightHandRotation, t),
            };
        }
    }

    /// <summary>
    /// 상대 자세 보간 버퍼(순수, 02 "상대 PC가 100ms 버퍼로 보간"). 비신뢰 전송이라 늦게·순서가 바뀌어 온 것은 버린다.
    /// 그리는 시각 = 지금 서버 시각 − 지연. 마지막 것보다 뒤면 마지막 자세를 유지하고(앞질러 예측하지 않음),
    /// 마지막 것이 <c>staleSeconds</c>보다 오래되면 "끊김"으로 본다(받는 쪽은 그 팔을 대기 자세로 둔다).
    /// </summary>
    public sealed class PoseBuffer
    {
        readonly PilotPose[] samples;
        int start, count;

        public PoseBuffer(int capacity = 32)
        {
            samples = new PilotPose[Mathf.Max(2, capacity)];
        }

        public int Count => count;
        public bool HasAny => count > 0;
        public double LatestTime => count > 0 ? At(count - 1).Time : double.NegativeInfinity;

        PilotPose At(int i) => samples[(start + i) % samples.Length];

        /// <summary>새 자세. 마지막 것보다 이르거나 같은 시각이면 버리고 false.</summary>
        public bool Add(in PilotPose pose)
        {
            if (count > 0 && pose.Time <= LatestTime) return false;
            if (count == samples.Length)
            {
                start = (start + 1) % samples.Length;
                count--;
            }
            samples[(start + count) % samples.Length] = pose;
            count++;
            return true;
        }

        public void Clear()
        {
            start = 0;
            count = 0;
        }

        /// <summary><paramref name="renderTime"/>의 자세. 비었거나 마지막 것이 <paramref name="staleSeconds"/>보다 오래되면 false.</summary>
        public bool TrySample(double renderTime, double staleSeconds, out PilotPose pose)
        {
            pose = default;
            if (count == 0) return false;
            var last = At(count - 1);
            if (renderTime - last.Time > staleSeconds) return false;
            if (renderTime >= last.Time)
            {
                pose = last;
                return true;
            }
            var first = At(0);
            if (renderTime <= first.Time)
            {
                pose = first;
                return true;
            }
            for (var i = count - 1; i > 0; i--)
            {
                var a = At(i - 1);
                if (a.Time > renderTime) continue;
                var b = At(i);
                var span = b.Time - a.Time;
                var t = span > 1e-9 ? (float)((renderTime - a.Time) / span) : 1f;
                pose = PilotPose.Lerp(a, b, t);
                return true;
            }
            pose = first;
            return true;
        }

        /// <summary>오래된 것 정리: <paramref name="renderTime"/>보다 이른 것 중 바로 앞 하나만 남긴다(보간에 필요).</summary>
        public void TrimBefore(double renderTime)
        {
            while (count >= 2 && At(1).Time <= renderTime)
            {
                start = (start + 1) % samples.Length;
                count--;
            }
        }
    }
}
