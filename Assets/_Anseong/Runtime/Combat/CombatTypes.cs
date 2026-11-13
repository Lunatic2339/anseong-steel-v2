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
        /// <summary>보스 공격 예고 시작. Code = 기술(BossAttackKind), Amount = 예고 등급(0 일반, 1 강, 2 가드 불가), Side = 첫 타가 들어오는 쪽(돌진만: 피해야 하는 쪽), ComboStep = 타 수(연타 4), Damage = 예고 길이(초, 연속기 뒤쪽은 줄어든 값).</summary>
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
        /// <summary>페이즈가 바뀜(M2-04). Code = 새 페이즈 번호(2·3).</summary>
        PhaseChanged,
        /// <summary>보스가 무릎 꿇음(HP 10%, 필살기 대기).</summary>
        BossKneel,
        /// <summary>사령관 대사(M2-04). Code = 대사 번호(<c>CommanderLines</c>). 소리 파일이 없으면 무음, 안내판에 글자.</summary>
        CommanderLine,
        /// <summary>보스가 크게 밀려남(부스트, M2-04). Amount = 거리(m).</summary>
        BossKnockback,
        /// <summary>필살기 충전 시작(M2-05). Amount = 충전 시간(초). 각 PC가 그 시간 동안 발광을 키운다.</summary>
        FinisherCharge,
        /// <summary>충전 중 한 팔이 놓아 처음부터.</summary>
        FinisherReset,
        /// <summary>필살기 발사(광선). Amount = 광선 시간(초). 외골격 X(즉시 해제) 자리.</summary>
        FinisherFire,
        /// <summary>승리: 쓰러짐 연출 뒤. Amount = 보스전 걸린 시간(초). 결과 화면은 M4.</summary>
        EncounterWon,
        /// <summary>협동 이동 시작(M3-03, 앞/뒤). Amount = MoveKind(1 접근, 2 후퇴). 좌/우 회피는 DodgeStart.</summary>
        MoveStart,
        /// <summary>판 장면이 바뀜(M4-01). Code = 새 장면(Anseong.Flow.MatchScene), Amount = 이전 장면. 소리·연출·맵 장치가 장면에 맞춰 켜고 끈다.</summary>
        MatchScene,
        // ── M4-04 드론 (Code = 드론 번호, Point = 드론 위치) ──
        /// <summary>숨은 드론이 쐈다(포탄 방향 = 왼쪽 HUD에만). Amount = 0 은폐 사격, 1 달려들기.</summary>
        DroneFire,
        /// <summary>스캐너에 드러남(3초).</summary>
        DroneRevealed,
        /// <summary>다시 숨음.</summary>
        DroneHidden,
        /// <summary>격추·쳐냄.</summary>
        DroneDown,
        /// <summary>오른팔 포 발사(Side = 쏜 팔, Point = 조준점). Amount = 1이면 보스 장갑에 튕김.</summary>
        CannonFire,
        /// <summary>해킹 드론이 팔에 붙음(Side = 해킹된 팔).</summary>
        HackStart,
        /// <summary>동료가 떼어 냄.</summary>
        HackCleared,
        // ── M4-05·07 ──
        /// <summary>기체 점검 단계가 바뀜. Code = 단계 번호(PilotCheckup).</summary>
        CheckupStep,
        /// <summary>맵 장치 작동. Code = MapEventKind(크레인 코일·컨베이어·사일로), Point = 보스 자리. 코일은 Amount = 떨어져 보스에 닿기까지(초, 닿는 순간은 MapImpact).</summary>
        MapEvent,
        // ── 잡고 베기 (선우 11/13, GrabSlashRules) ──
        /// <summary>왼팔이 보스를 붙잡음. Amount = 버틸 수 있는 시간(초), Code = 1이면 보스가 익숙해져 곧 뿌리친다.</summary>
        GrabStart,
        /// <summary>붙잡힌 보스를 오른팔이 벰. Damage = 추가 피해, Amount = 보스 휘청 시간(초).</summary>
        GrabSlash,
        /// <summary>잡기가 풀림. Amount = 0 놓음, 1 보스가 뿌리침.</summary>
        GrabBreak,
        // ── 검 맞부딪힘 힘겨루기 (11/13, ClashRules) ──
        /// <summary>검이 맞물림. Amount = 최대 시간(초).</summary>
        ClashStart,
        /// <summary>버티는 중 불꽃. Amount = 게이지 0~1.</summary>
        ClashTick,
        /// <summary>둘이 밀어냄. Damage = 핵에 들어간 피해.</summary>
        ClashWin,
        /// <summary>밀림.</summary>
        ClashLose,
        // ── 정비고·맵 장치 실물 연출 (11/13, CheckupDirector·MatchFlow·MapEvents가 낸다. 이 네 개는 한 블록) ──
        /// <summary>정비 로봇 공 던지기(장면 1-3). Code = 0 조준 시작(Side = 공이 오는 쪽, Amount = 조준 시간(초), Damage = 날아오는 시간(초)), 1 방패로 막음, 2 못 막음(피해 없음). Point = 공이 닿는 자리.</summary>
        CheckupBall,
        /// <summary>무장이 끼워짐(점검 1-3·1-4, 싸움 중 무장 바꿈). Code = Anseong.Flow.Loadout(1 검·방패, 2 스캐너·포).</summary>
        WeaponMount,
        /// <summary>정비고 출격 문(장면 1-5). Code = 0 열리기 시작(Amount = 걸리는 시간(초)), 1 다 열림. Point = 문 가운데.</summary>
        HangarDoor,
        /// <summary>맵 장치가 보스에 부딪힌 순간(크레인 코일이 떨어져 맞음). Code = MapEventKind, Amount = 세기(0~1), Point = 부딪힌 자리. MapEvent는 장치가 움직이기 시작한 순간.</summary>
        MapImpact,
        // ── (정비고·맵 장치 블록 끝) ──
        // ── 드론 연출 (11/13, DroneDirector). 이 블록은 드론 담당이 더한 것. 다른 사건은 이 블록 아래에 더한다 ──
        /// <summary>보스 등 뒤에서 드론이 튀어나옴. Point = 튀어나온 자리, Code = 드론 수, Amount = 0 복선(장면 2, 흩어져 사라지는 겉모습만), 1 진짜 드론이 날아 들어옴.</summary>
        DroneScatter,
        /// <summary>우리 포탄이 보스 장갑에 맞고 튕김(대본 3-4). Point = 명중점(보스 몸 높이). 금속음 + 불꽃.</summary>
        CannonRicochet,
        /// <summary>달려드는 드론 경고음(가까울수록 자주 온다). Point = 드론 위치, Code = 드론 번호, Amount = 다가온 정도 0~1, Side = 노리는 쪽.</summary>
        DroneWarn,
        /// <summary>동료 손이 팔에 붙은 해킹 드론을 붙잡음(이제 당기면 뜯긴다). Side = 잡은 팔, Point = 드론 위치.</summary>
        HackGrab,
        // ── 드론 연출 끝 ──
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
