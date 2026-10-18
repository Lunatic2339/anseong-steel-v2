using System;
using UnityEngine;

namespace Anseong.Pilot
{
    [Serializable]
    public class BodyFollowSettings
    {
        [Tooltip("보정 뒤 플레이어가 방 안에서 걷거나 자세를 옮겨도 보정한 어깨·가드가 머리를 따라 같이 옮겨 가게 한다. " +
                 "끄면 보정한 순간의 위치에 고정된다(이전 동작).")]
        public bool enabled = true;

        [Tooltip("몸 위치를 따라가는 시간(초). 길수록 펀치 중 몸을 숙이는 것은 덜 지우고, 걸어 옮긴 것만 지운다. 0이면 머리를 바로 따라간다.")]
        [Min(0f)]
        public float followSeconds = 0.6f;

        [Tooltip("앉거나 무릎을 굽혀 머리 높이가 바뀌면 어깨 높이도 따라간다.")]
        public bool followHeight = true;

        [Tooltip("따라가는 최대 거리(m). 넘으면 이 거리에서 멈춘다(머리 추적이 튀었을 때 대비).")]
        [Min(0f)]
        public float maxOffset = 2f;
    }

    /// <summary>
    /// 보정한 순간의 머리 위치를 기준으로, 지금 몸이 얼마나 옮겨 갔는지(트래킹 공간, m)를 천천히 따라간다.
    /// 손 위치에서 이 값을 빼면 "보정한 자리에 그대로 서 있는 것처럼" 잴 수 있다. 머리를 돌리는 것(회전)은 쓰지 않는다.
    /// 짧은 움직임(펀치 중 몸 숙임)은 followSeconds 만큼 늦게 따라가므로 대부분 남는다.
    /// </summary>
    public sealed class BodyFollow
    {
        Vector3 reference;
        bool hasReference;

        /// <summary>지금 몸이 보정 때보다 옮겨 간 양(트래킹 공간, m). 기준이 없거나 꺼져 있으면 0.</summary>
        public Vector3 Offset { get; private set; }

        public bool HasReference => hasReference;

        /// <summary>보정이 끝난 순간 머리 위치를 기준으로 잡는다.</summary>
        public void SetReference(Vector3 head)
        {
            if (!Finite(head)) return;
            reference = head;
            hasReference = true;
            Offset = Vector3.zero;
        }

        public void Clear()
        {
            hasReference = false;
            Offset = Vector3.zero;
        }

        public void Update(Vector3 head, float dt, BodyFollowSettings settings)
        {
            if (!hasReference || settings == null || !settings.enabled)
            {
                Offset = Vector3.zero;
                return;
            }
            if (!Finite(head) || dt <= 0f) return;
            var target = head - reference;
            if (!settings.followHeight) target.y = 0f;
            target = Vector3.ClampMagnitude(target, settings.maxOffset);
            Offset = settings.followSeconds > 0f
                ? Vector3.Lerp(Offset, target, 1f - Mathf.Exp(-dt / settings.followSeconds))
                : target;
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
    }
}
