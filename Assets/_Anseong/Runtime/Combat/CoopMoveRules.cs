using System;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>협동 이동 종류(01 "이동"). 좌/우는 회피(M2-03 DodgeRules가 판정).</summary>
    public enum MoveKind : byte
    {
        None,
        Approach,
        Retreat,
        DodgeLeft,
        DodgeRight,
    }

    [Serializable]
    public class CoopMoveSettings
    {
        [Tooltip("두 스틱을 이만큼 이상 밀어야 입력(01: 0.5).")]
        [Range(0.1f, 1f)] public float minMagnitude = 0.5f;

        [Tooltip("두 스틱 방향 차이가 이 각도(°) 안이어야 같은 방향(01: 25).")]
        [Range(1f, 90f)] public float maxAngle = 25f;

        [Tooltip("앞/뒤로 치는 방향: 수직에서 이 각도(°) 안.")]
        [Range(10f, 80f)] public float maxFromVertical = 45f;

        [Tooltip("한 번 움직이는 시간(초). 01·07: 약 0.4.")]
        [Range(0.1f, 2f)] public float stepSeconds = 0.4f;

        [Tooltip("접근·후퇴 한 번 거리(m, 로봇 크기). 계속 밀고 있으면 이어서 한 번 더.")]
        [Min(0.5f)] public float stepMeters = 8f;

        [Tooltip("접근은 보스와 이 거리(m)까지만(근접 거리: 팔 19.4 m × 0.95 + 몸통, M1-05 샌드박스 26 m).")]
        [Min(1f)] public float approachStopMeters = 26f;

        [Tooltip("후퇴는 보스와 이 거리(m)까지만(사거리 밖).")]
        [Min(1f)] public float retreatStopMeters = 60f;

        [Tooltip("접근·후퇴 한 번 뒤 다음 걸음까지 쉬는 시간(초). 묵직하게 끊어 걷기.")]
        [Range(0f, 1f)] public float stepGapSeconds = 0.1f;

        [Tooltip("회피 거리(m, 로봇 크기). M2-03 임시 미끄러짐과 같은 값.")]
        [Min(0.5f)] public float dodgeMeters = 10f;

        [Tooltip("회피 뒤 다음 회피까지 쉬는 시간(초). 앞뒤 걸음은 회피가 끝나면 바로(걸음 사이 쉼만).")]
        [Range(0f, 2f)] public float dodgeCooldownSeconds = 0.8f;

        [Tooltip("회피 입력을 이 시간(초) 동안 기억했다가 쉬는 시간이 끝나면 실행한다(걸음·쉼 중에 민 회피가 사라지지 않게).")]
        [Range(0f, 1f)] public float dodgeBufferSeconds = 0.3f;

        [Tooltip("보스가 로봇 정면에서 이 각도(°)를 넘게 벗어나면 몸을 돌린다(01: 20).")]
        [Range(1f, 90f)] public float turnStartDegrees = 20f;

        [Tooltip("돌기 시작하면 이 각도(°) 안까지 돌린다.")]
        [Range(0f, 20f)] public float turnStopDegrees = 2f;

        [Tooltip("몸 회전 최대 속도(°/초, 01: 30).")]
        [Range(1f, 180f)] public float turnDegreesPerSecond = 30f;
    }

    /// <summary>로봇 루트 한 번 이동(서버가 정하고 모든 PC가 같은 서버 시각으로 재생).</summary>
    public struct MoveSegment
    {
        public MoveKind Kind;
        public Vector3 From, To;
        /// <summary>시작 서버 시각(초).</summary>
        public double Start;
        public float Duration;

        public bool IsMoving(double now) => Kind != MoveKind.None && now >= Start && now < Start + Duration;
        public double End => Start + Duration;

        /// <summary>그 시각의 위치. 묵직하게: 천천히 출발해 천천히 멈춘다.</summary>
        public Vector3 Evaluate(double now)
        {
            if (Kind == MoveKind.None || Duration <= 0f) return To;
            var t = Mathf.Clamp01((float)((now - Start) / Duration));
            return Vector3.Lerp(From, To, Mathf.SmoothStep(0f, 1f, t));
        }
    }

    /// <summary>
    /// 협동 이동 규칙(M3-03, 순수). 01 "이동": 두 보조 스틱이 같은 방향(크기 ≥ 0.5, 차이 25° 이내)일 때만 인정, 한 명이 놓으면 정지.
    /// 앞 = 근접 거리까지 접근, 뒤 = 사거리 밖까지 후퇴, 좌/우 = 회피(새로 민 순간만, DodgeRules, 걸음 도중이면 끊고 회피). 자유 이동 없음.
    /// 몸은 보스가 정면 ±20° 밖이면 초당 30° 이하로 돌린다.
    /// </summary>
    public static class CoopMoveRules
    {
        /// <summary>두 스틱이 같은 앞/뒤 방향이면 Approach/Retreat. 좌/우·불일치·약함은 None(좌/우는 회피 규칙이 따로 본다).</summary>
        public static MoveKind ForwardBack(Vector2 a, Vector2 b, CoopMoveSettings s)
        {
            if (a.magnitude < s.minMagnitude || b.magnitude < s.minMagnitude) return MoveKind.None;
            if (Vector2.Angle(a, b) > s.maxAngle) return MoveKind.None;
            var mean = (a.normalized + b.normalized) * 0.5f;
            if (mean.sqrMagnitude < 1e-6f) return MoveKind.None;
            var fromVertical = Mathf.Atan2(Mathf.Abs(mean.x), Mathf.Abs(mean.y)) * Mathf.Rad2Deg;
            if (fromVertical > s.maxFromVertical) return MoveKind.None;
            return mean.y > 0f ? MoveKind.Approach : MoveKind.Retreat;
        }

        /// <summary>
        /// 회피 한 번을 정한다(앞뒤 걸음보다 먼저). 앞뒤 걸음 도중이면 그 자리(<paramref name="robot"/> = 지금 위치)에서 끊고 옆으로 간다.
        /// 회피 도중이거나 회피 쉼(<paramref name="nextDodgeAllowed"/>) 중이면 false(부르는 쪽이 잠깐 기억했다가 다시 부른다).
        /// </summary>
        public static bool TryPlanDodge(double now, int direction, MoveSegment current, Vector3 robot, Quaternion robotRotation, Vector3 boss,
            CoopMoveSettings s, ref double nextAllowed, ref double nextDodgeAllowed, out MoveSegment segment)
        {
            segment = default;
            if (direction == 0 || now < nextDodgeAllowed) return false;
            if ((current.Kind == MoveKind.DodgeLeft || current.Kind == MoveKind.DodgeRight) && current.IsMoving(now)) return false;
            var to = DodgeTarget(robot, robotRotation, boss, Mathf.Sign(direction) * s.dodgeMeters);
            segment = Make(direction < 0 ? MoveKind.DodgeLeft : MoveKind.DodgeRight, robot, to, now, s.stepSeconds);
            nextAllowed = now + s.stepSeconds + s.stepGapSeconds;
            nextDodgeAllowed = now + s.stepSeconds + s.dodgeCooldownSeconds;
            return true;
        }

        /// <summary>
        /// 다음 앞뒤 한 걸음을 정한다. 쉬는 중이면 false. <paramref name="nextAllowed"/>는 다음 이동이 가능한 서버 시각(이 함수가 갱신).
        /// </summary>
        public static bool TryPlan(double now, MoveKind held, Vector3 robot, Quaternion robotRotation, Vector3 boss,
            CoopMoveSettings s, ref double nextAllowed, out MoveSegment segment)
        {
            segment = default;
            if (now < nextAllowed) return false;

            if (held != MoveKind.Approach && held != MoveKind.Retreat) return false;
            var toBoss = boss - robot;
            toBoss.y = 0f;
            var distance = toBoss.magnitude;
            var forward = distance > 1e-3f ? toBoss / distance : Flat(robotRotation * Vector3.forward);
            float meters;
            if (held == MoveKind.Approach) meters = Mathf.Min(s.stepMeters, distance - s.approachStopMeters);
            else meters = Mathf.Min(s.stepMeters, s.retreatStopMeters - distance);
            if (meters < 0.25f) return false;   // 이미 근접 거리·사거리 끝
            var direction = held == MoveKind.Approach ? forward : -forward;
            var duration = s.stepSeconds * Mathf.Clamp01(meters / s.stepMeters);
            duration = Mathf.Max(duration, s.stepSeconds * 0.5f);
            segment = Make(held, robot, robot + direction * meters, now, duration);
            nextAllowed = now + duration + s.stepGapSeconds;
            return true;
        }

        /// <summary>
        /// 회피 도착점: 보스를 중심으로 같은 거리를 유지하며 옆으로 <paramref name="meters"/>(+ 오른쪽, − 왼쪽)만큼 돈 자리(옆구르기, 보스 고정 시점).
        /// 보스가 너무 가까우면(1 m 안) 로봇 오른쪽으로 곧게.
        /// </summary>
        public static Vector3 DodgeTarget(Vector3 robot, Quaternion robotRotation, Vector3 boss, float meters)
        {
            var from = robot - boss;
            from.y = 0f;
            var radius = from.magnitude;
            if (radius < 1f)
            {
                var right = Flat(robotRotation * Vector3.right);
                return robot + right * meters;
            }
            // 보스를 보는 사람 기준 오른쪽 = 위에서 볼 때 시계 방향(Unity y축 +회전).
            var angle = -meters / radius * Mathf.Rad2Deg;
            var rotated = Quaternion.Euler(0f, angle, 0f) * from;
            var to = boss + rotated;
            to.y = robot.y;
            return to;
        }

        /// <summary>보스를 바로 보는 yaw(°). 보스 고정 시점.</summary>
        public static float LookYaw(Vector3 robot, Vector3 boss, float fallback)
        {
            var to = boss - robot;
            to.y = 0f;
            return to.sqrMagnitude < 1e-4f ? fallback : Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 몸 자동 회전 한 번(보스 고정 시점을 끈 때만). 보스 방향과의 차이가 시작 각도를 넘으면 돌기 시작해 멈춤 각도 안까지 초당 최대 속도로 돈다.
        /// 돌리는 중인지(<paramref name="turning"/>)를 이어 받아 히스테리시스를 둔다. 새 yaw(°)를 돌려준다.
        /// </summary>
        public static float TurnYaw(float yaw, Vector3 robot, Vector3 boss, float dt, CoopMoveSettings s, ref bool turning)
        {
            var to = boss - robot;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f)
            {
                turning = false;
                return yaw;
            }
            var desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            var diff = Mathf.DeltaAngle(yaw, desired);
            if (!turning && Mathf.Abs(diff) > s.turnStartDegrees) turning = true;
            if (!turning) return yaw;
            if (Mathf.Abs(diff) <= s.turnStopDegrees)
            {
                turning = false;
                return yaw;
            }
            var step = Mathf.Clamp(diff, -s.turnDegreesPerSecond * dt, s.turnDegreesPerSecond * dt);
            return Mathf.Repeat(yaw + step + 180f, 360f) - 180f;
        }

        static MoveSegment Make(MoveKind kind, Vector3 from, Vector3 to, double now, float duration)
        {
            return new MoveSegment { Kind = kind, From = from, To = to, Start = now, Duration = duration };
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
