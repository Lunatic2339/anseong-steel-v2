using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>명중을 어떻게 얻었는지.</summary>
    public enum HitQuality
    {
        /// <summary>주먹이 실제로 보스 부위에 닿았다.</summary>
        Contact,
        /// <summary>공격 보정이 주먹을 끌어당겨 맞혔다.</summary>
        Assisted,
    }

    /// <summary>보스 맞는 부위. 피해 배율·반응은 나중에 부위별로 나눌 수 있게 이름만 둔다.</summary>
    public enum BossZone
    {
        Body,
        Head,
    }

    /// <summary>
    /// 때린 쪽 PC가 만든 명중 보고(02_architecture "누가 무엇을 계산하나"). M1은 로컬 호출, M3에서 서버 RPC 인자 그대로.
    /// </summary>
    public struct HitReport
    {
        public PilotSide Side;
        public PunchKind Kind;
        public HitQuality Quality;
        public BossZone Zone;
        /// <summary>그 팔의 동작 번호(PunchClassifier StrikeId). 한 동작은 한 번만 명중.</summary>
        public int StrikeId;
        /// <summary>보낸 쪽 시각(초). 서버 판정에는 서버 시각을 쓴다.</summary>
        public double ClientTime;
        /// <summary>맞은 자리(월드). 연출용.</summary>
        public Vector3 Point;
    }

    /// <summary>
    /// 전투에서 일어난 일. 소리·진동·보스 애니메이션·효과·외골격·안내판이 모두 이것만 듣고 각자 재생한다.
    /// 새 연출은 이 목록에 없는 것만 추가하고, 규칙 코드는 연출을 모른다.
    /// </summary>
    public enum CombatCue
    {
        /// <summary>그립을 쥐어 주먹이 잠김.</summary>
        GripLock,
        /// <summary>그립을 놓아 주먹이 풀림.</summary>
        GripRelease,
        /// <summary>공격 동작이 시작됨(잽 발사·훅 스윙 소리). Kind = 그 순간 미리 본 종류.</summary>
        StrikeLaunch,
        /// <summary>보스에 명중, 피해 확정. Damage·ComboStep 채움.</summary>
        Hit,
        /// <summary>그립을 쥐고 친 공격이 명중 없이 끝남.</summary>
        Miss,
        /// <summary>연계 단계가 올라감(ComboStep = 1·2·3).</summary>
        ComboStep,
        /// <summary>연계가 끊김(헛침·차례 아닌 공격·시간 초과·직접 피해).</summary>
        ComboBroken,
        /// <summary>마지막 훅 명중 뒤 부스트 입력창이 열림.</summary>
        BoostReady,
        /// <summary>부스트 성공: 추가 피해. Damage 채움.</summary>
        Boost,
        /// <summary>부스트 입력창이 그냥 닫힘(시간 초과·그립 놓음).</summary>
        BoostMissed,
        /// <summary>보스 HP 0.</summary>
        BossDefeated,

        // ── M2 (enum 값이 소리 표 에셋에 숫자로 저장되므로 항상 끝에만 더한다) ──

        /// <summary>보스가 빈틈·경직이 아닐 때 친 명중: 피해 0, 연계 차례 그대로(M2-02).</summary>
        Blocked,
        /// <summary>보스 공격 예고 시작. Code = 기술(BossAttackKind), Amount = 예고 등급(0 일반, 1 강, 2 가드 불가), Side = 들어오는 쪽.</summary>
        BossTelegraph,
        /// <summary>보스 공격 명중 순간(연타는 타마다). Code = 기술, Amount = 몇 번째 타(0부터), Side = 들어오는 쪽.</summary>
        BossStrike,
        /// <summary>보스 빈틈 시작(반격 기회). Amount = 빈틈 길이(초).</summary>
        BossOpening,
        /// <summary>보스 공격을 가드로 막음(M2-03). Side = 들어온 쪽, Code = 기술. 외골격 R(강한 저항 300ms) 자리.</summary>
        GuardBlock,
        /// <summary>가드가 모자람(반대 팔·한 팔만). Side = 들어온 쪽, Code = 기술. 이어서 PilotHit이 온다.</summary>
        GuardFail,
        /// <summary>두 스틱이 같은 좌/우로 새로 밀림(회피 동작 시작). Amount = 방향(-1 왼쪽, +1 오른쪽).</summary>
        DodgeStart,
        /// <summary>돌진 회피 성공. Code = 기술.</summary>
        Dodge,
        /// <summary>조종사 로봇이 맞음. Damage = 깎인 HP(%), Amount = 깎인 뒤 HP(%), Side = 들어온 쪽, Code = 기술.</summary>
        PilotHit,
        /// <summary>조종사 로봇 HP 0(패배 연출은 M4).</summary>
        PilotDefeated,
    }

    /// <summary>연계가 끊긴 이유.</summary>
    public enum ComboBreak
    {
        None,
        Miss,
        WrongTurn,
        Timeout,
        Damaged,
    }

    public struct CombatEvent
    {
        public CombatCue Cue;
        public PilotSide Side;
        public PunchKind Kind;
        /// <summary>이 사건으로 준 피해(보스 HP %).</summary>
        public float Damage;
        /// <summary>사건 뒤 연계 단계(0 = 연계 없음, 3 = 잽–잽–훅 완성).</summary>
        public int ComboStep;
        public ComboBreak Break;
        /// <summary>사건 뒤 보스 HP(%).</summary>
        public float BossHealth;
        public HitQuality Quality;
        public BossZone Zone;
        public Vector3 Point;
        public double Time;
        /// <summary>사건별 번호(보스 기술, 페이즈 번호 등). 뜻은 <see cref="CombatCue"/> 설명에.</summary>
        public int Code;
        /// <summary>사건별 수치(예고 등급, 빈틈 길이, 충전 비율 등). 뜻은 <see cref="CombatCue"/> 설명에.</summary>
        public float Amount;
    }
}
