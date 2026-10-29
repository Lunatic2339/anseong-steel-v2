using System;
using UnityEngine;

namespace Anseong.Pilot
{
    [Serializable]
    public class HandDeadzoneSettings
    {
        [Tooltip("켜면 손의 작은 떨림은 무시하고 큰 움직임에만 로봇 팔이 반응한다(선우 10/25: 컨트롤러가 민감해 잔움직임이 많다). 펀치 인식에는 영향 없음.")]
        public bool enabled = true;

        [Tooltip("무시할 손 떨림 크기(m, 사람 크기). 손이 이보다 멀리 움직여야 로봇 주먹이 따라간다. 로봇 배율이 곱해져 크게 보이므로 작게 둔다.")]
        [Range(0f, 0.05f)]
        public float radiusMeters = 0.015f;

        [Tooltip("무시할 손 각도 떨림(도). 컨트롤러가 이보다 많이 돌아야 로봇 주먹이 돈다.")]
        [Range(0f, 10f)]
        public float angleDegrees = 3f;
    }

    /// <summary>
    /// 손 위치·회전의 작은 떨림을 걸러낸다(순수). 손이 기준점에서 radius 안이면 기준점 그대로, 넘으면 기준점을 손 쪽으로 끌고 간다
    /// (끈으로 끄는 것처럼). 그래서 큰 움직임은 그대로 따라가고, 멈추면 바로 멈춘다(늦게 따라오는 묵직함과는 따로).
    /// </summary>
    public sealed class HandDeadzone
    {
        Vector3 position;
        Quaternion rotation = Quaternion.identity;
        bool hasValue;

        public void Reset() => hasValue = false;

        public (Vector3 position, Quaternion rotation) Filter(Vector3 handPosition, Quaternion handRotation, HandDeadzoneSettings settings)
        {
            if (settings == null || !settings.enabled || !hasValue)
            {
                position = handPosition;
                rotation = handRotation;
                hasValue = true;
                return (handPosition, handRotation);
            }

            var offset = handPosition - position;
            var distance = offset.magnitude;
            if (distance > settings.radiusMeters)
                position += offset * ((distance - settings.radiusMeters) / distance);

            var angle = Quaternion.Angle(rotation, handRotation);
            if (angle > settings.angleDegrees)
                rotation = Quaternion.Slerp(rotation, handRotation, (angle - settings.angleDegrees) / angle);
            return (position, rotation);
        }
    }
}
