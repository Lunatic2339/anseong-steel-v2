using System;
using Anseong.Combat;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Haptics
{
    /// <summary>
    /// 외골격 세기·시간(01_combat_rules "외골격 피드백"). 세기 값은 실제 장력(kg)이 아니다.
    /// 고건 책상 시제품 세기표(Hardware/Exo/, T3) 전까지 기본 세기는 임시값.
    /// </summary>
    [Serializable]
    public class ExoCueSettings
    {
        [Tooltip("펀치 궤적이 인식된 순간(발사) 끈을 미리 약하게 감아 둔다(P). 감는 시간이 지연이 되므로 켜 둔다.")]
        public bool prepareOnStrike = true;

        [Range(50, 1000), Tooltip("펀치 발사 준비 P의 대기(ms). 이 안에 명중 R이 안 오면 펌웨어가 다시 푼다.")]
        public int strikePrepareMs = 400;

        [Range(0, 255), Tooltip("명중 저항 세기(0-255). 실제 장력은 고건 세기표(T3)를 보고 맞춘다. 지금은 임시값.")]
        public int hitStrength = 140;

        [Range(20, 500), Tooltip("명중 저항 시간(ms). 01: 120ms.")]
        public int hitMs = 120;

        [Range(0, 255), Tooltip("가드 접촉 세기(0-255, 임시). M2-03 가드 이벤트가 들어오면 쓴다.")]
        public int guardStrength = 220;

        [Range(20, 500), Tooltip("가드 접촉 저항 시간(ms). 01: 300ms.")]
        public int guardMs = 300;

        [Tooltip("헛치면 준비 대기를 기다리지 않고 바로 푼다(X).")]
        public bool releaseOnMiss = true;
    }

    /// <summary>
    /// 전투 이벤트 하나 → 그 팔 외골격에 보낼 명령 한 줄(순수). 보낼 것이 없으면 null.
    /// 각 PC는 자기 팔 장치만 움직인다(02 "외골격·컨트롤러 진동: 각 PC가 자기 장치만"). 1인 양팔은 링크 두 개.
    /// 보스 공격 예고 P·가드 접촉 R은 M2-02·03 이벤트가 생기면 여기에 한 줄씩 더한다.
    /// </summary>
    public static class ExoCueRules
    {
        public static string CommandFor(in CombatEvent e, PilotSide arm, ExoCueSettings s)
        {
            switch (e.Cue)
            {
                case CombatCue.StrikeLaunch:
                    return e.Side == arm && s.prepareOnStrike ? ExoProtocol.Prepare(s.strikePrepareMs) : null;
                case CombatCue.Hit:
                    return e.Side == arm ? ExoProtocol.Resist(s.hitStrength, s.hitMs) : null;
                case CombatCue.Miss:
                    return e.Side == arm && s.releaseOnMiss ? ExoProtocol.Release : null;
                case CombatCue.Boost:
                    // 부스트 점화: 순간 가벼워짐(즉시 해제).
                    return e.Side == arm ? ExoProtocol.Release : null;
                case CombatCue.BossDefeated:
                    return ExoProtocol.Release;
                default:
                    return null;
            }
        }
    }
}
