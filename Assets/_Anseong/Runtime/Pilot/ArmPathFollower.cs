using System;
using UnityEngine;

namespace Anseong.Pilot
{
    /// <summary>로봇 주먹이 손 목표를 따라가는 방식.</summary>
    public enum ArmResponseMode
    {
        /// <summary>손이 지나간 경로를 그대로 따라가되 처음엔 느리게 출발해 점점 빨라진다(기본, 선우 10/28: 묵직하게 시작해 펀치 끝에 타격감).</summary>
        PathAccelerate,
        /// <summary>목표 쪽으로 부드럽게 늦게 따라간다(10/25까지 방식, 팔 반응 시간).</summary>
        Smooth,
    }

    [Serializable]
    public class ArmPathSettings
    {
        [Tooltip("출발 속도(로봇 팔 길이/초). 멈춰 있다가 움직이기 시작할 때 이 속도로 출발한다. 작을수록 묵직하게 출발.")]
        [Min(0.01f)]
        public float startSpeed = 0.6f;

        [Tooltip("가속도(로봇 팔 길이/초²). 손을 따라가는 동안 이만큼씩 빨라진다. 클수록 펀치 끝이 빠르고 세다.")]
        [Min(0f)]
        public float acceleration = 10f;

        [Tooltip("최대 속도(로봇 팔 길이/초).")]
        [Min(0.1f)]
        public float maxSpeed = 7f;

        [Tooltip("남은 경로가 이보다 짧으면(로봇 팔 길이 배수) 따라잡은 것으로 보고 다음 출발은 다시 느리게.")]
        [Min(0f)]
        public float settleRatio = 0.02f;
    }

    /// <summary>
    /// 손 목표가 지나간 경로를 저장해 두고, 주먹이 그 경로를 따라 처음엔 느리게 출발해 가속하며 따라간다(순수).
    /// 좌표는 로봇 어깨 기준·로봇 정면 기준(m), length = 로봇 팔 길이. 훅처럼 휘두른 경로도 직선으로 질러가지 않고 그 호를 따라간다.
    /// </summary>
    public sealed class ArmPathFollower
    {
        const int Capacity = 256;
        const float MinSpacingRatio = 0.005f;

        readonly Vector3[] points = new Vector3[Capacity];
        int head, count;
        Vector3 position;
        float speed;
        bool hasValue;

        public Vector3 Position => position;
        public float Speed => speed;

        /// <summary>남은 경로 길이(m).</summary>
        public float Remaining { get; private set; }

        public void Reset(Vector3 at)
        {
            position = at;
            head = count = 0;
            speed = 0f;
            Remaining = 0f;
            hasValue = true;
        }

        public Vector3 Step(Vector3 target, float length, float dt, ArmPathSettings settings)
        {
            if (!hasValue || settings == null || length <= 0f)
            {
                Reset(target);
                return position;
            }
            var last = count > 0 ? points[(head + count - 1) % Capacity] : position;
            if ((target - last).magnitude >= length * MinSpacingRatio) Push(target);

            Remaining = PathLength(target);
            if (Remaining <= length * settings.settleRatio)
            {
                // 따라잡음: 남은 짧은 거리는 바로 붙고, 다음 출발은 다시 느리게.
                head = count = 0;
                position = target;
                speed = 0f;
                Remaining = 0f;
                return position;
            }

            speed = speed <= 0f ? settings.startSpeed * length : speed;
            speed = Mathf.Min(settings.maxSpeed * length, speed + settings.acceleration * length * Mathf.Max(0f, dt));
            Advance(speed * Mathf.Max(0f, dt), target);
            Remaining = PathLength(target);
            return position;
        }

        void Push(Vector3 p)
        {
            if (count == Capacity)
            {
                // 너무 길게 밀리면 가장 오래된 점으로 건너뛴다.
                position = points[head];
                head = (head + 1) % Capacity;
                count--;
            }
            points[(head + count) % Capacity] = p;
            count++;
        }

        void Advance(float distance, Vector3 target)
        {
            while (distance > 0f)
            {
                var next = count > 0 ? points[head] : target;
                var gap = (next - position).magnitude;
                if (gap > distance)
                {
                    position += (next - position) * (distance / gap);
                    return;
                }
                position = next;
                distance -= gap;
                if (count == 0) return;
                head = (head + 1) % Capacity;
                count--;
            }
        }

        float PathLength(Vector3 target)
        {
            var total = 0f;
            var from = position;
            for (var i = 0; i < count; i++)
            {
                var p = points[(head + i) % Capacity];
                total += (p - from).magnitude;
                from = p;
            }
            return total + (target - from).magnitude;
        }
    }
}
