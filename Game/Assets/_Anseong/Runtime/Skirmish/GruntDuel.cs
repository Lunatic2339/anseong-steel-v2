using System;
using System.Collections.Generic;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>돌격형 일반병의 상태. 제작 명세 "돌격형 일반병" 표와 같다.</summary>
    public enum GruntStep : byte
    {
        /// <summary>로봇을 향해 걷다가 마지막 구간은 달려든다. 주먹이 닿지 않는다.</summary>
        Approach,
        /// <summary>기본 자세. 주먹이 절반 들어간다.</summary>
        Idle,
        /// <summary>예고: 팔을 올리는 중.</summary>
        Rise,
        /// <summary>예고: 팔을 든 채 멈춤. 명세의 "예고 시간"이 이 구간이다.</summary>
        Hold,
        /// <summary>예고: 내려치는 중. 끝나는 순간에 막기 판정을 한다.</summary>
        Strike,
        /// <summary>공격이 안 막혀서 팔을 거두는 중.</summary>
        Recover,
        /// <summary>튕겨남. 가슴이 열려 있다.</summary>
        Stagger,
        /// <summary>가드 굳힘. 주먹이 튕긴다. 포로만 열 수 있다.</summary>
        Guard,
        /// <summary>아군 드론에 붙들려 멈춤.</summary>
        Frozen,
        Dead,
        /// <summary>맞붙은 적이 따로 있어서 로봇에서 35 떨어져 기다림. 주먹이 닿지 않는다. 둘이 할 때는 6초마다 큰 탄을 쏜다.</summary>
        Waiting,
        /// <summary>막이 바뀌는 사이: 한 발 물러서서 자세를 다시 잡는다. 서로 피해가 없다(보스).</summary>
        Regroup,
    }

    public enum GruntAttack : byte
    {
        /// <summary>내려치기. 적의 오른팔, 로봇의 왼쪽으로 온다.</summary>
        Slam,
        /// <summary>휘두르기. 적의 왼팔, 로봇의 오른쪽으로 온다.</summary>
        Swing,
        // 아래는 보스의 특별한 공격(기획안 4절의 표).
        /// <summary>속임 베기. 어느 쪽인지는 등 뒤 불의 색으로만 나온다. 그쪽으로 막으면 튕겨난다.</summary>
        Feint,
        /// <summary>헬기를 노림. P1이 오른쪽으로 막아 주면 튕겨난다. 아니면 P2가 숨어야 한다. 한 막에 한 번.</summary>
        AimHeli,
        /// <summary>자폭 떼 부르기. 대검을 땅에 꽂고, 끝나면 자폭 드론 넷이 한꺼번에 온다. 한 막에 한 번.</summary>
        CallSwarm,
        /// <summary>큰 내려베기. 딱 맞춰 막아야만 틈이 난다. 미리 막고 있으면 피해만 없다.</summary>
        BigSlam,
        /// <summary>찌르며 돌진. 막을 수 없다. 옆으로 비켜 있으면 지나쳐서 등을 보인다.</summary>
        Thrust,
    }

    public enum PunchResult : byte
    {
        /// <summary>아직 다가오는 중이거나 이미 쓰러져서 닿지 않음.</summary>
        OutOfReach,
        /// <summary>가드에 튕김. 피해 없음.</summary>
        Bounced,
        Hit,
        /// <summary>이 주먹으로 쓰러뜨림(마무리 한 방).</summary>
        Finished,
    }

    public enum GruntEventKind : byte
    {
        SprintStarted,
        Arrived,
        /// <summary>팔을 들기 시작함. Attack에 어느 공격인지.</summary>
        TelegraphStarted,
        Blocked,
        /// <summary>딱 맞춰 막음. 튕겨남이 길어진다.</summary>
        PerfectBlocked,
        /// <summary>로봇이 맞음. Amount가 피해.</summary>
        RobotHit,
        /// <summary>주먹이 들어감. Amount가 실제로 깎인 체력.</summary>
        PunchHit,
        PunchBounced,
        GuardClosed,
        /// <summary>포에 가드가 깨짐.</summary>
        GuardBroken,
        /// <summary>가드를 스스로 풂.</summary>
        GuardReleased,
        Frozen,
        /// <summary>쓰러짐. Amount가 뒤로 밀려나는 거리.</summary>
        Down,
        /// <summary>큰 탄을 쏘려고 멈춰 서서 겨누기 시작함.</summary>
        ShellWindupStarted,
        /// <summary>큰 탄이 나감.</summary>
        ShellFired,
        /// <summary>P2의 강한 탄에 맞아 예고가 끊기고 튕겨남(보스).</summary>
        StrongHit,
        /// <summary>검과 방패를 부수는 일격이 닿음(보스 3막에 들어갈 때). 막든 안 막든 피해는 없다.</summary>
        WeaponsBroken,
        /// <summary>헬기를 노린 일격이 막히지 않고 나감. P2가 숨었는지는 교전 규칙이 본다.</summary>
        HeliShot,
        /// <summary>대검을 땅에 꽂아 자폭 떼를 부름.</summary>
        SwarmCalled,
        /// <summary>돌진이 빗나가 로봇을 지나침. 등을 보인 채 멈춘다.</summary>
        ThrustPassed,
    }

    public struct GruntEvent
    {
        public GruntEventKind Kind;
        public GruntAttack Attack;
        public float Amount;
    }

    /// <summary>돌격형 공통 수치. 전부 첫 시험값이고 헤드셋으로 해 보고 고친다(제작 명세).</summary>
    [Serializable]
    public class GruntSettings
    {
        [Header("거리와 속도 (m, m/s)")]
        [Tooltip("맞붙는 거리. 로봇 중심에서 이만큼 떨어져 선다.")]
        [Min(1f)] public float engageDistance = 22f;
        [Min(0.1f)] public float walkSpeed = 8f;
        [Tooltip("설 자리까지 이 거리 안이면 달려든다.")]
        [Min(0f)] public float sprintDistance = 30f;
        [Min(0.1f)] public float sprintSpeed = 16f;
        [Tooltip("로봇이 물러나면 이 속도로 따라붙는다.")]
        [Min(0.1f)] public float chaseSpeed = 12f;

        [Tooltip("막이 바뀌는 사이에 맞붙는 거리에서 이만큼 더 물러선다(보스).")]
        [Min(0f)] public float regroupBack = 6f;

        [Tooltip("맞붙은 적이 따로 있을 때 기다리는 거리.")]
        [Min(1f)] public float waitDistance = 35f;

        [Header("큰 탄 (기다리는 적, 서 있는 적)")]
        [Tooltip("기다리는 적이 큰 탄을 쏘는 간격.")]
        [Min(0.5f)] public float shellEvery = 6f;
        [Tooltip("큰 탄을 쏘기 전에 멈춰 서서 겨누는 시간.")]
        [Min(0f)] public float shellWindup = 2f;

        [Header("시간 (초)")]
        [Min(0f)] public float idleSeconds = 0.5f;
        [Tooltip("팔을 올리는 시간. 동작 파일의 '올림' 구간.")]
        [Min(0f)] public float riseSeconds = 0.4f;
        [Tooltip("내려치는 시간. 동작 파일의 '타격' 구간.")]
        [Min(0f)] public float strikeSeconds = 0.3f;
        [Min(0f)] public float recoverSeconds = 0.7f;
        [Min(0.1f)] public float staggerSeconds = 2f;
        [Tooltip("공격이 닿기 전 이 시간 안에 막는 자세가 되었으면 딱 맞춰 막기.")]
        [Min(0f)] public float perfectWindow = 0.3f;
        [Tooltip("딱 맞춰 막으면 튕겨남이 이만큼 길어진다.")]
        [Min(0f)] public float perfectBonus = 1f;
        [Min(0.1f)] public float guardSeconds = 4f;
        [Tooltip("혼자 할 때 가드가 스스로 풀리는 시간.")]
        [Min(0.1f)] public float guardSecondsSolo = 3f;
        [Min(0.1f)] public float guardBreakStagger = 2.5f;
        [Min(0f), Tooltip("주먹이 꽂힌 순간 적이 멈추는 시간. 튕길 때는 멈추지 않는다. 0 = 끔.")]
        public float hitStopSeconds = 0.06f;
        [Min(0f), Tooltip("마무리 한 방은 이만큼 멈춘 뒤에 밀려나며 쓰러진다. 0 = 바로.")]
        public float finishStopSeconds = 0.1f;

        [Header("피해")]
        [Tooltip("막지 못했을 때 로봇이 받는 피해.")]
        [Min(0f)] public float hitDamage = 15f;
        [Tooltip("기본 자세와 드론에 멈춘 동안 주먹이 들어가는 비율.")]
        [Range(0f, 1f)] public float halfOpenMultiplier = 0.5f;
        [Tooltip("가드 중에 포 피해가 이만큼 쌓이면 가드가 깨진다.")]
        [Min(1f)] public float guardBreakDamage = 40f;
        [Tooltip("P2의 포가 체력을 깎는 비율. 쓰러뜨리는 것은 P1의 몫이라 포로는 죽지 않는다.")]
        [Range(0f, 1f)] public float cannonHpMultiplier = 0.1f;
        [Tooltip("마무리 한 방에 뒤로 밀려나는 거리.")]
        [Min(0f)] public float finishKnockback = 15f;
    }

    /// <summary>적 한 명의 설정. 장면별 표에서 적마다 다른 값.</summary>
    [Serializable]
    public class GruntSpec
    {
        [Min(1f)] public float hp = 100f;
        [Tooltip("공격 순서. 끝까지 가면 처음으로 돌아간다.")]
        public GruntAttack[] attacks = { GruntAttack.Slam };
        [Tooltip("팔을 든 채 멈춰 있는 시간. 장면 1의 첫 적만 1.2.")]
        [Min(0f)] public float holdSeconds = 0.9f;
        [Tooltip("첫 가드까지 필요한 주먹 피해. 0이면 guardEveryDamage를 쓴다.")]
        [Min(0f)] public float firstGuardDamage;
        [Tooltip("가드와 가드 사이에 필요한 주먹 피해. 0이면 가드를 굳히지 않는다.")]
        [Min(0f)] public float guardEveryDamage = 60f;
        [Tooltip("다가오지 않고 제자리에 서 있다가, 로봇이 맞붙는 거리 안으로 오면 싸운다(장면 2의 첫 적).")]
        public bool standStill;

        [Header("보스 (기획안 2026-11-26)")]
        [Tooltip("튕겨난 동안에만 주먹과 포가 체력을 깎는다. 그 밖에는 튕긴다.")]
        public bool openOnlyWhenStaggered;
        [Tooltip("공격마다 처음 나올 때 예고를 이만큼 늘린다. 1 = 그대로.")]
        [Min(1f)] public float firstTimeScale = 1f;
        [Tooltip("혼자 할 때의 체력. 0이면 hp와 같다.")]
        [Min(0f)] public float soloHp;
        [Tooltip("P2의 강한 탄이 잠글 수 있다. 맞으면 어떤 예고든 끊기고 튕겨난다.")]
        public bool strongShotTarget;

        [Header("보스의 특별한 공격 (예고 = 올림 0.4 + 멈춤 + 내려침 0.3)")]
        [Min(0f), Tooltip("속임 베기의 멈춤. 예고 2.2초.")] public float feintHold = 1.5f;
        [Tooltip("속임 베기가 오는 쪽의 순서(난수 없이 차례로 돈다).")]
        public PilotSide[] feintSides = { PilotSide.Right, PilotSide.Left, PilotSide.Left, PilotSide.Right, PilotSide.Left, PilotSide.Right, PilotSide.Right };
        [Min(0f), Tooltip("헬기를 노림의 멈춤. 예고 2.5초.")] public float aimHold = 1.8f;
        [Min(0f), Tooltip("자폭 떼 부르기의 멈춤. 3초 뒤에 온다.")] public float swarmHold = 2.3f;
        [Tooltip("큰 내려베기의 멈춤 순서. 예고 3.0, 2.4, 3.6초.")]
        public float[] bigHolds = { 2.3f, 1.7f, 2.9f };
        [Min(0f), Tooltip("큰 내려베기를 딱 맞춰 막았을 때 튕겨나는 시간.")] public float bigStagger = 4f;
        [Min(0f), Tooltip("큰 내려베기를 못 막았을 때의 피해.")] public float bigDamage = 40f;
        [Min(0f), Tooltip("찌르며 돌진의 멈춤. 예고 1.6초.")] public float thrustHold = 0.9f;
        [Min(0f), Tooltip("돌진에 맞았을 때의 피해. 막아도 들어온다.")] public float thrustDamage = 30f;
        [Min(0f), Tooltip("돌진의 순간 로봇이 옆으로 이만큼 넘게 비켜 있으면 빗나간다.")] public float dodgeDistance = 6f;
        [Min(0f), Tooltip("빗나간 돌진이 앞으로 나가는 거리.")] public float thrustLunge = 14f;
        [Min(0f), Tooltip("돌진이 빗나간 뒤 등을 보인 채 멈춰 있는 시간(긴 틈).")] public float thrustStagger = 3f;
    }

    /// <summary>
    /// 돌격형 일반병 한 명과 로봇의 싸움 규칙. 서버(호스트)만 돌린다. MonoBehaviour·네트워크와 분리된 순수 클래스.
    /// 같은 입력을 같은 순서로 넣으면 항상 같은 결과가 나온다(난수 없음).
    /// 쓰는 순서: Tick → (그 틱의 Punch, CannonHit, Freeze) → Events 읽기. Tick이 Events를 비운다.
    /// </summary>
    public sealed class GruntDuel
    {
        public readonly GruntSettings Settings;
        public readonly GruntSpec Spec;
        /// <summary>true면 가드가 guardSecondsSolo 뒤에 스스로 풀린다(P2가 없을 때).</summary>
        public bool Solo;

        public GruntStep Step { get; private set; }
        public Vector3 Position { get; private set; }
        public float Hp { get; private set; }
        /// <summary>지금 단계가 끝날 때까지 남은 초.</summary>
        public float Timer { get; private set; }
        /// <summary>지금 예고 중이거나 마지막으로 한 공격.</summary>
        public GruntAttack Attack { get; private set; }
        public bool Alive => Step != GruntStep.Dead;
        /// <summary>타격 멈춤이 끝날 때까지 남은 초. 이 동안 적의 시간은 흐르지 않는다.</summary>
        public float Stop { get; private set; }
        /// <summary>마무리 한 방을 맞고 멈춰 있는 중. 멈춤이 끝나면 밀려나며 쓰러진다.</summary>
        public bool Finishing { get; private set; }
        /// <summary>맞붙은 적이 따로 있어서 뒤에서 기다리는 역할. <see cref="Hold"/>와 <see cref="Release"/>로 바꾼다.</summary>
        public bool Held { get; private set; }
        /// <summary>기다리는 동안 6초마다 큰 탄을 쏜다. 혼자 할 때는 끈다.</summary>
        public bool FireShells;
        /// <summary>큰 탄을 겨누는 중이면 발사까지 남은 초. 아니면 0.</summary>
        public float ShellWindup { get; private set; }
        public readonly List<GruntEvent> Events = new List<GruntEvent>();
        float shellCooldown;

        /// <summary>처음 체력(혼자 할 때는 soloHp).</summary>
        public float MaxHp { get; private set; }
        /// <summary>예고 길이에 곱하는 값. 로봇 내구도가 낮을 때 교전 규칙이 올린다. 다음 예고부터 적용된다.</summary>
        public float TelegraphScale = 1f;
        /// <summary>체력이 이 값 아래로는 내려가지 않는다(보스: 한 방에 막 하나를 건너뛰지 않게). 교전 규칙이 막마다 정한다. 0 = 없음.</summary>
        public float HpFloor;
        /// <summary>검과 방패를 부수는 일격을 예고하거나 내려치는 중. 끊을 수 없고 피해가 없다.</summary>
        public bool Breaker { get; private set; }

        /// <summary>로봇이 지금 옆으로 비켜 있는 거리. 교전 규칙이 매 틱 넣는다(돌진 판정에 쓴다).</summary>
        public float Dodge;
        /// <summary>지금 공격을 막아야 하는 쪽(속임 베기는 숨겨진 답). 큰 내려베기는 어느 쪽이든 된다.</summary>
        public PilotSide Side => Attack == GruntAttack.Feint ? feintSide : Attack == GruntAttack.Slam || Attack == GruntAttack.BigSlam ? PilotSide.Left : PilotSide.Right;
        /// <summary>예고하는 중(팔을 올림, 멈춤, 내려침).</summary>
        public bool Telegraphing => Step == GruntStep.Rise || Step == GruntStep.Hold || Step == GruntStep.Strike;
        /// <summary>P2가 보고 말해 줘야 하는 예고 중. 이 동안에는 자폭 드론을 띄우지 않는다.</summary>
        public bool NeedsCall => Telegraphing && !Breaker && (Attack == GruntAttack.Feint || Attack == GruntAttack.BigSlam || Attack == GruntAttack.AimHeli);
        /// <summary>공격이 닿을 때까지 남은 초. 예고 중이 아니면 0. 큰 내려베기의 불 셋이 이것을 보고 꺼진다.</summary>
        public float ImpactIn => Step == GruntStep.Rise ? Timer + (holdFor + Settings.strikeSeconds) * phaseScale
            : Step == GruntStep.Hold ? Timer + Settings.strikeSeconds * phaseScale
            : Step == GruntStep.Strike ? Timer : 0f;
        /// <summary>돌진이 빗나가 로봇을 지나쳐 등을 보이고 있다.</summary>
        public bool Passed => passed;

        GruntAttack[] attackList;   // 막이 바뀌며 갈아 끼운 공격 순서. 없으면 Spec.attacks
        PilotSide feintSide;
        int feintCount, bigCount, onceUsed;
        float holdFor;              // 지금 예고의 멈춤 시간(배율 적용 전)
        bool passed;
        int attackIndex, guardsDone;
        int attacksSeen;        // 한 번이라도 나온 공격(비트)
        float phaseScale = 1f;  // 지금 예고에 적용된 배율
        float guardMeter, cannonMeter, guardHeldFor;
        bool wasGuarding, sprinting;
        Vector3 lastRobot;
        PilotSide lastGuardSide;

        /// <param name="solo">혼자 하는 판이면 true. soloHp가 있으면 그 체력으로 시작한다.</param>
        public GruntDuel(Vector3 start, GruntSpec spec = null, GruntSettings settings = null, bool solo = false)
        {
            Settings = settings ?? new GruntSettings();
            Spec = spec ?? new GruntSpec();
            Solo = solo;
            Position = start;
            Hp = MaxHp = solo && Spec.soloHp > 0f ? Spec.soloHp : Spec.hp;
            Step = GruntStep.Approach;
        }

        /// <summary>공격이 로봇의 어느 쪽으로 오는가.</summary>
        /// <summary>맞붙지 말고 35 거리에서 기다리게 한다. 등장하자마자 부른다.</summary>
        public void Hold() => Held = true;

        /// <summary>기다리던 적이 싸우러 들어온다. 겨누던 큰 탄은 취소된다.</summary>
        public void Release()
        {
            if (!Held) return;
            Held = false;
            ShellWindup = 0f;
            if (Step == GruntStep.Waiting) { Step = GruntStep.Approach; sprinting = false; }
        }

        /// <summary>큰 탄 한 발을 겨누기 시작한다. 다가오는 중이거나 기다리는 중에만 된다.</summary>
        public bool BeginShell()
        {
            if ((Step != GruntStep.Approach && Step != GruntStep.Waiting) || ShellWindup > 0f) return false;
            ShellWindup = Mathf.Max(0.0001f, Settings.shellWindup);
            Emit(GruntEventKind.ShellWindupStarted);
            return true;
        }

        void TickShell(float dt)
        {
            if (ShellWindup <= 0f) return;
            ShellWindup -= dt;
            if (ShellWindup > 0f) return;
            ShellWindup = 0f;
            Emit(GruntEventKind.ShellFired);
        }

        /// <summary>막이 바뀌는 사이: seconds 동안 물러서서 자세를 다시 잡는다. 하던 공격은 끊긴다.</summary>
        public void Regroup(float seconds)
        {
            if (!Alive || Finishing) return;
            Stop = 0f; ShellWindup = 0f; Breaker = false; passed = false;
            Step = GruntStep.Regroup;
            Timer = Mathf.Max(0.0001f, seconds);
        }

        /// <summary>마무리가 시작될 때: 하던 것을 모두 끊고 선다. 이후의 자세는 화면이 마무리 단계를 보고 잡는다.</summary>
        public void Halt()
        {
            if (!Alive) return;
            Stop = 0f; ShellWindup = 0f; Breaker = false; passed = false; Finishing = false;
            Step = GruntStep.Idle;
            Timer = 0f;
        }

        /// <summary>마무리의 마지막 일격이 끝났을 때: 체력과 상관없이 쓰러진다.</summary>
        public void Fall()
        {
            if (!Alive) return;
            Hp = 0f;
            Finish();
        }

        /// <summary>검과 방패를 부수는 일격을 시작한다(왼쪽 내려베기, 예고는 처음 나오는 공격만큼 길다). 하던 공격은 끊긴다.</summary>
        public void BeginBreaker()
        {
            if (!Alive || Finishing) return;
            Stop = 0f; ShellWindup = 0f; Breaker = true; passed = false;
            Attack = GruntAttack.Slam;
            holdFor = Spec.holdSeconds;
            phaseScale = Mathf.Max(1f, Spec.firstTimeScale);
            Step = GruntStep.Rise;
            Timer = Settings.riseSeconds * phaseScale;
            Emit(GruntEventKind.TelegraphStarted);
        }

        /// <summary>다음 예고부터 이 순서로 공격한다(막마다 공격 목록이 다르다).</summary>
        public void SetAttacks(GruntAttack[] attacks)
        {
            attackList = attacks;
            attackIndex = 0;
            onceUsed = 0;
        }

        /// <summary>재기동 뒤에 막의 처음 체력에서 다시 시작할 때.</summary>
        public void StartAt(float hp) => Hp = Mathf.Clamp(hp, 1f, MaxHp);

        /// <summary>체력을 깎되 least와 <see cref="HpFloor"/> 아래로는 내리지 않는다. 실제로 깎인 양을 돌려준다.</summary>
        float Lose(float amount, float least)
        {
            float before = Hp;
            Hp = Mathf.Max(Mathf.Min(Hp, Mathf.Max(least, HpFloor)), Hp - Mathf.Max(0f, amount));
            return before - Hp;
        }

        public static PilotSide IncomingSide(GruntAttack attack) => attack == GruntAttack.Slam ? PilotSide.Left : PilotSide.Right;

        /// <summary>이 상태에서 주먹이 들어가는 비율. 0이면 닿지 않거나 튕긴다.</summary>
        public float PunchMultiplier()
        {
            switch (Step)
            {
                case GruntStep.Approach:
                case GruntStep.Guard:
                case GruntStep.Dead:
                case GruntStep.Waiting:
                case GruntStep.Regroup:
                    return 0f;
                case GruntStep.Stagger:
                    return 1f;
                case GruntStep.Idle:
                case GruntStep.Frozen:
                    return Spec.openOnlyWhenStaggered ? 0f : Settings.halfOpenMultiplier;
                default:
                    return Spec.openOnlyWhenStaggered ? 0f : 1f;
            }
        }

        /// <param name="robot">로봇의 위치(발).</param>
        /// <param name="guarding">왼팔이 막는 자세인가(기존 막기 판정의 결과).</param>
        /// <param name="guardSide">막는 팔이 덮고 있는 쪽.</param>
        public void Tick(float dt, Vector3 robot, bool guarding, PilotSide guardSide)
        {
            Events.Clear();
            lastRobot = robot;
            if (!Alive || dt <= 0f) return;

            // 막는 자세를 같은 쪽으로 얼마나 유지했는지. 딱 맞춰 막기 판정에 쓴다.
            if (guarding && wasGuarding && guardSide == lastGuardSide) guardHeldFor += dt;
            else guardHeldFor = 0f;
            wasGuarding = guarding;
            lastGuardSide = guardSide;

            if (Stop > 0f)
            {
                // 타격 멈춤: 남은 시간만큼 적의 시간을 세우고, 넘친 시간만 흘려보낸다.
                Stop -= dt;
                if (Stop > 0f) return;
                dt = -Stop;
                Stop = 0f;
                if (Finishing) { Finish(); return; }
            }

            if (Step == GruntStep.Approach)
            {
                // 겨누는 동안에는 멈춰 선다.
                if (ShellWindup <= 0f)
                {
                    if (Spec.standStill && !Held) StandStill(robot); else Advance(dt, robot);
                }
                TickShell(dt);
                return;
            }
            if (Step == GruntStep.Waiting)
            {
                if (ShellWindup <= 0f)
                {
                    Chase(dt, robot);
                    if (FireShells)
                    {
                        shellCooldown -= dt;
                        if (shellCooldown <= 0f && BeginShell()) shellCooldown = Settings.shellEvery - Settings.shellWindup;
                    }
                }
                TickShell(dt);
                return;
            }
            // 돌진이 빗나가 지나친 동안에는 그 자리에 멈춰 있다.
            if (Step != GruntStep.Frozen && !(passed && Step == GruntStep.Stagger)) Chase(dt, robot);

            Timer -= dt;
            // 한 틱에 여러 단계가 끝날 수 있다. 남은 시간은 다음 단계로 넘긴다.
            for (int guard = 0; Timer <= 0f && Alive && guard < 8; guard++)
            {
                switch (Step)
                {
                    case GruntStep.Idle: BeginTelegraph(); break;
                    case GruntStep.Rise: Enter(GruntStep.Hold, holdFor * phaseScale); break;
                    case GruntStep.Hold: Enter(GruntStep.Strike, Settings.strikeSeconds * phaseScale); break;
                    case GruntStep.Strike: Impact(guarding, guardSide); break;
                    case GruntStep.Stagger:
                        // 지나쳤던 돌진은 돌아서서 제자리로 걸어온다.
                        if (passed) { passed = false; Enter(GruntStep.Recover, Settings.recoverSeconds); break; }
                        Enter(GruntStep.Idle, Settings.idleSeconds); break;
                    case GruntStep.Recover:
                    case GruntStep.Regroup:
                        Enter(GruntStep.Idle, Settings.idleSeconds); break;
                    case GruntStep.Frozen:
                        // 기다리던 적은 풀려나도 다시 기다린다.
                        if (Held) { Step = GruntStep.Waiting; Timer = 0f; shellCooldown = Settings.shellEvery - Settings.shellWindup; }
                        else Enter(GruntStep.Idle, Settings.idleSeconds);
                        break;
                    case GruntStep.Guard:
                        Emit(GruntEventKind.GuardReleased);
                        BeginTelegraph();
                        break;
                }
            }
        }

        /// <summary>P1의 주먹이 몸통에 닿았을 때. damage는 잽 25, 훅 35 같은 원래 피해.</summary>
        public PunchResult Punch(float damage)
        {
            if (Step == GruntStep.Approach || Step == GruntStep.Waiting || Step == GruntStep.Dead || Finishing) return PunchResult.OutOfReach;
            if (Step == GruntStep.Guard || PunchMultiplier() <= 0f)
            {
                Emit(GruntEventKind.PunchBounced);
                return PunchResult.Bounced;
            }
            float applied = Lose(damage * PunchMultiplier(), 0f);
            Emit(GruntEventKind.PunchHit, applied);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Finishing = true;
                Stop = Settings.finishStopSeconds;
                if (Stop <= 0f) Finish();
                return PunchResult.Finished;
            }
            Stop = Settings.hitStopSeconds;
            // 가드는 튕겨남이나 회복이 끝나기를 기다리지 않고 그 자리에서 바로 굳힌다.
            guardMeter += applied;
            float threshold = guardsDone == 0 && Spec.firstGuardDamage > 0f ? Spec.firstGuardDamage : Spec.guardEveryDamage;
            if (Spec.guardEveryDamage > 0f && Step != GruntStep.Frozen && guardMeter >= threshold)
            {
                guardMeter = 0f;
                cannonMeter = 0f;
                guardsDone++;
                Step = GruntStep.Guard;
                Timer = Solo ? Settings.guardSecondsSolo : Settings.guardSeconds;
                Emit(GruntEventKind.GuardClosed);
            }
            return PunchResult.Hit;
        }

        /// <summary>P2의 포가 맞았을 때. 체력은 조금만 깎고, 가드 중이면 가드를 연다.</summary>
        public void CannonHit(float damage)
        {
            if (!Alive || Finishing || damage <= 0f) return;
            // 보스는 튕겨난 동안에만, 그 대신 온전히 들어간다.
            float share = !Spec.openOnlyWhenStaggered ? Settings.cannonHpMultiplier : Step == GruntStep.Stagger ? 1f : 0f;
            // 포로는 쓰러지지 않는다.
            Lose(damage * share, 1f);
            if (Step != GruntStep.Guard) return;
            cannonMeter += damage;
            if (cannonMeter < Settings.guardBreakDamage) return;
            cannonMeter = 0f;
            Step = GruntStep.Stagger;
            Timer = Settings.guardBreakStagger;
            Emit(GruntEventKind.GuardBroken);
        }

        /// <summary>
        /// P2의 강한 탄이 맞았을 때(보스). 피해는 언제나 들어가고, 예고 중이든 아니든 seconds 동안 튕겨난다.
        /// 아직 다가오는 중이면 안 맞는다.
        /// </summary>
        public bool StrongHit(float damage, float seconds)
        {
            if (!Alive || Finishing || Step == GruntStep.Approach || Step == GruntStep.Waiting) return false;
            // 막이 바뀌는 사이와 검·방패를 부수는 일격은 끊지 못한다.
            if (Step == GruntStep.Regroup || Breaker) return false;
            // 쓰러뜨리는 것은 P1의 몫이다.
            Lose(damage, 1f);
            Timer = Step == GruntStep.Stagger ? Mathf.Max(Timer, seconds) : seconds;
            Step = GruntStep.Stagger;
            Emit(GruntEventKind.StrongHit, damage);
            return true;
        }

        /// <summary>아군 드론이 붙어 멈춰 세운다. 끝나면 기본 자세로 돌아간다.</summary>
        public void Freeze(float seconds)
        {
            if (!Alive || Finishing || seconds <= 0f) return;
            Step = GruntStep.Frozen;
            Timer = seconds;
            ShellWindup = 0f;
            Emit(GruntEventKind.Frozen);
        }

        /// <summary>마무리 한 방의 멈춤이 끝난 순간: 뒤로 밀려나며 쓰러진다.</summary>
        void Finish()
        {
            Finishing = false;
            Stop = 0f;
            Step = GruntStep.Dead;
            Timer = 0f;
            var away = SkirmishMath.Flat(Position - lastRobot);
            Position += (away == Vector3.zero ? Vector3.forward : away) * Settings.finishKnockback;
            Emit(GruntEventKind.Down, Settings.finishKnockback);
        }

        Vector3 StandPoint(Vector3 robot)
        {
            var away = SkirmishMath.Flat(Position - robot);
            if (away == Vector3.zero) away = Vector3.forward;
            float distance = Held ? Settings.waitDistance : Settings.engageDistance + (Step == GruntStep.Regroup ? Settings.regroupBack : 0f);
            var p = robot + away * distance;
            p.y = Position.y;
            return p;
        }

        void Advance(float dt, Vector3 robot)
        {
            var s = Settings;
            var stand = StandPoint(robot);
            float left = Vector3.Distance(Position, stand);
            if (!sprinting && left <= s.sprintDistance)
            {
                sprinting = true;
                Emit(GruntEventKind.SprintStarted);
            }
            float step = (sprinting ? s.sprintSpeed : s.walkSpeed) * dt;
            if (left > step) { Position = Vector3.MoveTowards(Position, stand, step); return; }
            Position = stand;
            if (Held)
            {
                Step = GruntStep.Waiting;
                Timer = 0f;
                shellCooldown = s.shellEvery - s.shellWindup;
                return;
            }
            Step = GruntStep.Idle;
            Timer = s.idleSeconds;
            Emit(GruntEventKind.Arrived);
        }

        /// <summary>제자리에 서 있다가 로봇이 맞붙는 거리 안으로 오면 기본 자세로 싸움을 시작한다.</summary>
        void StandStill(Vector3 robot)
        {
            var flat = Position - robot;
            flat.y = 0f;
            if (flat.magnitude > Settings.engageDistance) return;
            Step = GruntStep.Idle;
            Timer = Settings.idleSeconds;
            Emit(GruntEventKind.Arrived);
        }

        void Chase(float dt, Vector3 robot)
        {
            var stand = StandPoint(robot);
            if (Vector3.Distance(Position, stand) < 0.01f) return;
            Position = Vector3.MoveTowards(Position, stand, Settings.chaseSpeed * dt);
        }

        void BeginTelegraph()
        {
            var attacks = attackList != null && attackList.Length > 0 ? attackList
                : Spec.attacks != null && Spec.attacks.Length > 0 ? Spec.attacks : new[] { GruntAttack.Slam };
            Attack = GruntAttack.Slam;
            for (int tries = 0; tries < attacks.Length; tries++)
            {
                var next = attacks[attackIndex % attacks.Length];
                attackIndex++;
                // 헬기를 노림과 자폭 떼 부르기는 한 막에 한 번. 혼자 할 때는 헬기를 노리지 않는다.
                bool once = next == GruntAttack.AimHeli || next == GruntAttack.CallSwarm;
                if (once && ((onceUsed & (1 << (int)next)) != 0 || (Solo && next == GruntAttack.AimHeli))) continue;
                if (once) onceUsed |= 1 << (int)next;
                Attack = next;
                break;
            }
            switch (Attack)
            {
                case GruntAttack.Feint:
                    holdFor = Spec.feintHold;
                    feintSide = Spec.feintSides != null && Spec.feintSides.Length > 0 ? Spec.feintSides[feintCount++ % Spec.feintSides.Length] : PilotSide.Left;
                    break;
                case GruntAttack.AimHeli: holdFor = Spec.aimHold; break;
                case GruntAttack.CallSwarm: holdFor = Spec.swarmHold; break;
                case GruntAttack.BigSlam:
                    holdFor = Spec.bigHolds != null && Spec.bigHolds.Length > 0 ? Spec.bigHolds[bigCount++ % Spec.bigHolds.Length] : Spec.holdSeconds;
                    break;
                case GruntAttack.Thrust: holdFor = Spec.thrustHold; break;
                default: holdFor = Spec.holdSeconds; break;
            }
            int bit = 1 << (int)Attack;
            phaseScale = Mathf.Max(0.1f, TelegraphScale) * ((attacksSeen & bit) == 0 ? Mathf.Max(1f, Spec.firstTimeScale) : 1f);
            attacksSeen |= bit;
            Enter(GruntStep.Rise, Settings.riseSeconds * phaseScale);
            Emit(GruntEventKind.TelegraphStarted);
        }

        void Impact(bool guarding, PilotSide guardSide)
        {
            var s = Settings;
            if (Breaker)
            {
                Breaker = false;
                Enter(GruntStep.Recover, s.recoverSeconds);
                Emit(GruntEventKind.WeaponsBroken);
                return;
            }
            switch (Attack)
            {
                case GruntAttack.CallSwarm:
                    Enter(GruntStep.Recover, s.recoverSeconds);
                    Emit(GruntEventKind.SwarmCalled);
                    return;
                case GruntAttack.AimHeli:
                    // P1이 오른쪽(헬기 쪽)으로 막아 주면 튕겨난다. 아니면 헬기로 나간다(로봇 피해는 없다).
                    if (guarding && guardSide == PilotSide.Right)
                    {
                        Enter(GruntStep.Stagger, s.staggerSeconds);
                        Emit(GruntEventKind.Blocked);
                        return;
                    }
                    Enter(GruntStep.Recover, s.recoverSeconds);
                    Emit(GruntEventKind.HeliShot);
                    return;
                case GruntAttack.Thrust:
                    // 막을 수 없다. 옆으로 비켜 있으면 지나쳐서 등을 보인 채 멈춘다(긴 틈).
                    if (Dodge > Spec.dodgeDistance)
                    {
                        var toward = SkirmishMath.Flat(lastRobot - Position);
                        Position += toward * Spec.thrustLunge;
                        passed = true;
                        Step = GruntStep.Stagger;
                        Timer = Spec.thrustStagger;
                        Emit(GruntEventKind.ThrustPassed);
                        return;
                    }
                    Enter(GruntStep.Recover, s.recoverSeconds);
                    Emit(GruntEventKind.RobotHit, Spec.thrustDamage);
                    return;
                case GruntAttack.BigSlam:
                    // 딱 맞춰 막아야만 틈이 난다. 미리 막고 있으면 피해만 없다.
                    if (guarding && guardHeldFor <= s.perfectWindow)
                    {
                        Enter(GruntStep.Stagger, Spec.bigStagger);
                        Emit(GruntEventKind.PerfectBlocked);
                        return;
                    }
                    Enter(GruntStep.Recover, s.recoverSeconds);
                    if (guarding) Emit(GruntEventKind.Blocked); else Emit(GruntEventKind.RobotHit, Spec.bigDamage);
                    return;
            }
            if (guarding && guardSide == Side)
            {
                bool perfect = guardHeldFor <= s.perfectWindow;
                Enter(GruntStep.Stagger, s.staggerSeconds + (perfect ? s.perfectBonus : 0f));
                Emit(perfect ? GruntEventKind.PerfectBlocked : GruntEventKind.Blocked);
                return;
            }
            Enter(GruntStep.Recover, s.recoverSeconds);
            Emit(GruntEventKind.RobotHit, s.hitDamage);
        }

        /// <summary>다음 단계로 넘어간다. 지난 단계에서 넘친 시간은 새 단계에서 뺀다.</summary>
        void Enter(GruntStep step, float seconds)
        {
            Step = step;
            Timer = seconds + Mathf.Min(0f, Timer);
        }

        void Emit(GruntEventKind kind, float amount = 0f) =>
            Events.Add(new GruntEvent { Kind = kind, Attack = Attack, Amount = amount });
    }
}
