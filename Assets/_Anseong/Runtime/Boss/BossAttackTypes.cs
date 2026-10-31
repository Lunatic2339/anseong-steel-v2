using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Boss
{
    /// <summary>보스 기술(05 기술표). 숫자가 네트워크·이벤트 Code로 가므로 끝에만 더한다.</summary>
    public enum BossAttackKind
    {
        PunchLeft,
        PunchRight,
        AlternatingCombo,
        SlamBoth,
        Charge,
    }

    /// <summary>요구 대응(01 "가드와 회피").</summary>
    public enum BossCounter
    {
        LeftGuard,
        RightGuard,
        BothGuard,
        /// <summary>좌우 연타: 타마다 들어오는 쪽 팔.</summary>
        AlternateGuard,
        /// <summary>가드 불가: 두 조종사 같은 방향 회피.</summary>
        Dodge,
    }

    /// <summary>예고 등급(05: 일반 = 짧은 경고음 + 그 팔 발광, 강 = 낮은 경고음 + 두 팔, 가드 불가 = 사이렌 + 몸 전체).</summary>
    public enum TelegraphLevel
    {
        Normal,
        Heavy,
        Unblockable,
    }

    /// <summary>보스 상태(서버만 계산, 복제). 숫자가 네트워크로 가므로 끝에만 더한다.</summary>
    public enum BossState
    {
        /// <summary>쉬는 중(가드 자세). 피해 없음.</summary>
        Rest,
        /// <summary>예고(준비 동작 + 경고음).</summary>
        Telegraph,
        /// <summary>명중 순간이 지나가는 중(연타는 타 사이 포함).</summary>
        Strike,
        /// <summary>마지막 명중 뒤 회복 동작.</summary>
        Recovery,
        /// <summary>빈틈(반격 기회). 피해가 들어간다.</summary>
        Opening,
        /// <summary>경직(내려찍기 양팔 가드 성공, 돌진 회피 성공). 피해가 들어간다.</summary>
        Staggered,
        /// <summary>HP 0. 더 움직이지 않는다.</summary>
        Defeated,
    }

    /// <summary>
    /// 기술 하나의 시간·대응·피해(05 기술표 제안값). 시간은 공격 시작(예고 시작) 기준 초.
    /// "명중 순간" = 예고 길이(첫 타). 연타는 그 뒤 간격마다 한 타.
    /// </summary>
    [Serializable]
    public class BossAttackSpec
    {
        public BossAttackKind kind;

        [Tooltip("예고 길이(초) = 공격 시작부터 첫 명중 순간까지. 05: 일반 1.2, 강 1.8, 가드 불가 2.0.")]
        [Min(0.1f)] public float telegraphSeconds = 1.2f;

        [Tooltip("타 수(좌우 연타 4).")]
        [Min(1)] public int hits = 1;

        [Tooltip("연타 타 사이 간격(초, 05: 0.7).")]
        [Min(0.05f)] public float hitIntervalSeconds = 0.7f;

        [Tooltip("마지막 명중 순간부터 회복이 끝날 때까지(초). 그 뒤 빈틈. 클립을 보고 맞춘다.")]
        [Min(0f)] public float recoverySeconds = 1.0f;

        [Tooltip("요구 대응.")]
        public BossCounter counter;

        [Tooltip("들어오는 쪽(단발) 또는 첫 타 쪽(연타). 조종사 기준 왼쪽/오른쪽.")]
        public PilotSide firstSide = PilotSide.Right;

        [Tooltip("예고 등급(소리·발광 구분).")]
        public TelegraphLevel level;

        [Tooltip("대응 실패 피해(조종사 로봇 HP 100 대비, 타당).")]
        [Min(0f)] public float failDamage = 6f;

        [Tooltip("대응 성공 시 빈틈(초). 연타는 모든 타를 막아야 성공.")]
        [Min(0f)] public float openingOnSuccess = 3f;

        [Tooltip("대응 실패 시 빈틈(초, 01: 기본 1.5).")]
        [Min(0f)] public float openingOnFail = 1.5f;

        [Tooltip("대응 성공 시 빈틈 앞 경직(초). 내려찍기 양팔 가드 0.8, 돌진 회피 후 휘청.")]
        [Min(0f)] public float staggerOnSuccess;

        public int HitCount => Mathf.Max(1, hits);
        public double StrikeTime(int hit) => telegraphSeconds + Mathf.Clamp(hit, 0, HitCount - 1) * hitIntervalSeconds;
        public double EndTime => StrikeTime(HitCount - 1) + recoverySeconds;

        /// <summary>그 타가 들어오는 쪽. 연타는 번갈아.</summary>
        public PilotSide SideOf(int hit)
        {
            if (counter != BossCounter.AlternateGuard || hit % 2 == 0) return firstSide;
            return firstSide == PilotSide.Left ? PilotSide.Right : PilotSide.Left;
        }

        public BossAttackSpec Clone() => (BossAttackSpec)MemberwiseClone();

        /// <summary>05 기술표 제안값.</summary>
        public static BossAttackSpec Default(BossAttackKind kind)
        {
            switch (kind)
            {
                case BossAttackKind.PunchLeft:
                    return new BossAttackSpec { kind = kind, counter = BossCounter.LeftGuard, firstSide = PilotSide.Left, failDamage = 6f };
                case BossAttackKind.PunchRight:
                    return new BossAttackSpec { kind = kind, counter = BossCounter.RightGuard, firstSide = PilotSide.Right, failDamage = 6f };
                case BossAttackKind.AlternatingCombo:
                    return new BossAttackSpec
                    {
                        kind = kind, counter = BossCounter.AlternateGuard, firstSide = PilotSide.Right, hits = 4, hitIntervalSeconds = 0.7f, failDamage = 4f,
                    };
                case BossAttackKind.SlamBoth:
                    return new BossAttackSpec
                    {
                        kind = kind, counter = BossCounter.BothGuard, level = TelegraphLevel.Heavy, telegraphSeconds = 1.8f, failDamage = 12f,
                        staggerOnSuccess = 0.8f, recoverySeconds = 1.2f,
                    };
                default:
                    return new BossAttackSpec
                    {
                        kind = BossAttackKind.Charge, counter = BossCounter.Dodge, level = TelegraphLevel.Unblockable, telegraphSeconds = 2.0f,
                        failDamage = 15f, staggerOnSuccess = 1.0f, recoverySeconds = 1.2f,
                    };
            }
        }
    }
}
