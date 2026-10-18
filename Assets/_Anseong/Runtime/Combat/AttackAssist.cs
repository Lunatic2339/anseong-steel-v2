using System;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class AttackAssistSettings
    {
        [Tooltip("켜면 펀치 종류(잽·훅·어퍼컷)만 맞게 인식되면 주먹을 보스 쪽으로 끌어당겨 맞힌다(선우 10/18). " +
                 "끄면 주먹이 실제로 보스 부위에 닿아야만 명중(01 처음 규칙).")]
        public bool enabled = true;

        [Tooltip("보정 사거리: 로봇 어깨에서 보스 부위까지 거리가 로봇 팔 길이의 이 배수 안이면 끌어당긴다.")]
        [Min(0.5f)]
        public float rangeRatio = 1.4f;

        [Tooltip("보정 각도(도): 보스 부위가 로봇 정면에서 좌우로 이 안이면 끌어당긴다.")]
        [Range(5f, 90f)]
        public float coneDegrees = 40f;

        [Tooltip("주먹이 보스 쪽으로 끌려가는 시간(초). 길수록 묵직하다(육중한 메카, 순간이동처럼 보이지 않게).")]
        [Min(0.01f)]
        public float pullSeconds = 0.15f;

        [Tooltip("명중·동작 끝 뒤 주먹이 원래 손 위치로 돌아가는 시간(초).")]
        [Min(0.01f)]
        public float releaseSeconds = 0.25f;

        [Tooltip("끌림이 이만큼(0~1) 차면 닿은 것으로 보고 명중시킨다(보정 사거리가 팔 길이보다 멀어 실제로 닿지 못할 때).")]
        [Range(0.5f, 1f)]
        public float hitAtWeight = 0.95f;
    }

    /// <summary>
    /// 공격 보정 계산(순수). 보스 부위가 보정 범위 안인지, 주먹 목표를 얼마나 끌어당길지.
    /// 좌표는 월드, body = 로봇 정면 회전.
    /// </summary>
    public sealed class AttackAssist
    {
        readonly AttackAssistSettings settings;
        Vector3 point;

        public AttackAssist(AttackAssistSettings settings = null)
        {
            this.settings = settings ?? new AttackAssistSettings();
        }

        /// <summary>0 = 손 그대로, 1 = 보스 부위에 붙음.</summary>
        public float Weight { get; private set; }
        public bool Pulling { get; private set; }
        public Vector3 Point => point;
        public bool ReachedHit => Pulling && Weight >= settings.hitAtWeight;

        public static bool InRange(Vector3 shoulder, Quaternion body, Vector3 zonePoint, float armLength, AttackAssistSettings settings)
        {
            if (settings == null || !settings.enabled || armLength <= 0f) return false;
            var offset = zonePoint - shoulder;
            if (offset.magnitude > armLength * settings.rangeRatio) return false;
            var local = Quaternion.Inverse(body) * offset;
            local.y = 0f;
            if (local.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(Vector3.forward, local) <= settings.coneDegrees;
        }

        /// <summary>끌어당기기 시작(이미 당기는 중이면 목표만 바꾼다).</summary>
        public void Begin(Vector3 zonePoint)
        {
            point = zonePoint;
            Pulling = true;
        }

        /// <summary>명중했거나 동작이 끝나 놓는다. 주먹은 releaseSeconds 동안 원래 자리로 돌아간다.</summary>
        public void Release() => Pulling = false;

        public void Reset()
        {
            Pulling = false;
            Weight = 0f;
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;
            var seconds = Pulling ? settings.pullSeconds : settings.releaseSeconds;
            Weight = Mathf.MoveTowards(Weight, Pulling ? 1f : 0f, dt / Mathf.Max(0.01f, seconds));
        }

        /// <summary>손에서 나온 주먹 목표를 끌림만큼 보스 부위 쪽으로 옮긴다. 부드럽게 시작·끝나게 SmoothStep.</summary>
        public Vector3 Apply(Vector3 handTarget)
        {
            if (Weight <= 0f) return handTarget;
            return Vector3.Lerp(handTarget, point, Mathf.SmoothStep(0f, 1f, Weight));
        }
    }
}
