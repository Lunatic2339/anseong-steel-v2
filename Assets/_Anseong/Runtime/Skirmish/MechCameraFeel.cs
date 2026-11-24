using System;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>P1 카메라가 로봇의 걸음과 충격에 반응하는 정도. 전부 사람 크기(m)다. 멀미가 나면 줄이거나 끈다.</summary>
    [Serializable]
    public class CameraFeelSettings
    {
        [Tooltip("걸음마다 카메라가 살짝 내려앉는다(위아래만, 회전 없음).")]
        public bool steps = true;
        [Min(1f), Tooltip("한 걸음의 거리(m). 35m 로봇이 10m/s로 달리면 9m에 한 걸음 = 1초에 약 한 걸음.")]
        public float stepLength = 9f;
        [Range(0f, 0.08f), Tooltip("발을 디딜 때 내려앉는 깊이(m).")]
        public float stepDip = 0.025f;
        [Min(0.1f), Tooltip("이 속도(m/s)에서 걸음 반응이 전부 나온다. 느리면 그만큼 약하다.")]
        public float fullSpeed = 10f;

        [Tooltip("맞거나 막았을 때 카메라가 짧게 밀렸다 돌아온다(회전 없음).")]
        public bool impacts = true;
        [Range(0f, 0.2f), Tooltip("맞았을 때 밀리는 거리(m).")]
        public float hitPush = 0.08f;
        [Range(0f, 0.2f), Tooltip("막았을 때 밀리는 거리(m).")]
        public float blockPush = 0.03f;
        [Range(0.01f, 0.2f), Tooltip("가장 많이 밀리는 시점(초). 그 뒤로 서서히 돌아온다(약 6배 시간 뒤 거의 0).")]
        public float impactPeak = 0.04f;
        [Range(0f, 0.3f), Tooltip("걸음과 충격을 합친 최대 이동(m).")]
        public float maxOffset = 0.12f;
    }

    /// <summary>
    /// 로봇 위치와 충격으로 카메라 추가 이동을 계산한다(Unity 장면과 무관한 계산만).
    /// 위치만 움직이고 회전은 절대 넣지 않는다: 헤드셋에서 고개를 강제로 돌리면 멀미가 난다.
    /// </summary>
    public class MechCameraFeel
    {
        Vector3 last, pushDirection;
        bool hasLast;
        float travelled, weight, pushSize, pushAge = float.MaxValue;

        /// <summary>눈 위치에 더할 값(m, 월드).</summary>
        public Vector3 Offset { get; private set; }
        /// <summary>이번 Tick에서 발을 디뎠다(발소리·진동용).</summary>
        public bool Stepped { get; private set; }
        /// <summary>걸음 반응의 세기(0 = 서 있음, 1 = 전속력).</summary>
        public float StepWeight => weight;

        public void Reset()
        {
            hasLast = false;
            travelled = weight = 0f;
            pushAge = float.MaxValue;
            Offset = Vector3.zero;
            Stepped = false;
        }

        /// <param name="direction">밀리는 방향(월드). 수평 성분만 쓴다.</param>
        public void Impact(Vector3 direction, float size)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || size <= 0f) return;
            pushDirection = direction.normalized;
            pushSize = size;
            pushAge = 0f;
        }

        public void Tick(Vector3 mech, float dt, CameraFeelSettings s)
        {
            Stepped = false;
            if (dt <= 0f) return;
            float moved = 0f;
            if (hasLast)
            {
                var delta = mech - last;
                delta.y = 0f;
                moved = delta.magnitude;
                // 재시작처럼 순간 이동한 것은 걸음으로 치지 않는다.
                if (moved > s.fullSpeed * 3f * dt + 1f) moved = 0f;
            }
            last = mech;
            hasLast = true;

            float target = s.steps ? Mathf.Clamp01(moved / dt / s.fullSpeed) : 0f;
            weight = Mathf.MoveTowards(weight, target, 4f * dt);
            float before = travelled / s.stepLength;
            travelled += moved;
            float after = travelled / s.stepLength;
            if (Mathf.FloorToInt(after) != Mathf.FloorToInt(before) && weight > 0.2f) Stepped = true;
            // 발을 디디는 순간(정수)에 가장 낮고, 두 걸음 사이에 원래 높이로 돌아온다.
            float dip = s.stepDip * weight * (0.5f + 0.5f * Mathf.Cos(after * 2f * Mathf.PI));

            var push = Vector3.zero;
            if (s.impacts && pushAge < s.impactPeak * 12f)
            {
                pushAge += dt;
                float k = pushAge / s.impactPeak;
                push = pushDirection * (pushSize * k * Mathf.Exp(1f - k));
            }
            Offset = Vector3.ClampMagnitude(push + Vector3.down * dip, s.maxOffset);
        }
    }
}
