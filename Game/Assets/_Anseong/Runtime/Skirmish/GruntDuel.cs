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
    }

    public enum GruntAttack : byte
    {
        /// <summary>내려치기. 적의 오른팔, 로봇의 왼쪽으로 온다.</summary>
        Slam,
        /// <summary>휘두르기. 적의 왼팔, 로봇의 오른쪽으로 온다.</summary>
        Swing,
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

        int attackIndex, guardsDone;
        float guardMeter, cannonMeter, guardHeldFor;
        bool wasGuarding, sprinting;
        Vector3 lastRobot;
        PilotSide lastGuardSide;

        public GruntDuel(Vector3 start, GruntSpec spec = null, GruntSettings settings = null)
        {
            Settings = settings ?? new GruntSettings();
            Spec = spec ?? new GruntSpec();
            Position = start;
            Hp = Spec.hp;
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
                    return 0f;
                case GruntStep.Idle:
                case GruntStep.Frozen:
                    return Settings.halfOpenMultiplier;
                default:
                    return 1f;
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
            if (Step != GruntStep.Frozen) Chase(dt, robot);

            Timer -= dt;
            // 한 틱에 여러 단계가 끝날 수 있다. 남은 시간은 다음 단계로 넘긴다.
            for (int guard = 0; Timer <= 0f && Alive && guard < 8; guard++)
            {
                switch (Step)
                {
                    case GruntStep.Idle: BeginTelegraph(); break;
                    case GruntStep.Rise: Enter(GruntStep.Hold, Spec.holdSeconds); break;
                    case GruntStep.Hold: Enter(GruntStep.Strike, Settings.strikeSeconds); break;
                    case GruntStep.Strike: Impact(guarding, guardSide); break;
                    case GruntStep.Recover:
                    case GruntStep.Stagger:
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
            if (Step == GruntStep.Guard)
            {
                Emit(GruntEventKind.PunchBounced);
                return PunchResult.Bounced;
            }
            float applied = Mathf.Max(0f, damage) * PunchMultiplier();
            Hp -= applied;
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
            // 포로는 쓰러지지 않는다.
            Hp = Mathf.Max(1f, Hp - damage * Settings.cannonHpMultiplier);
            if (Step != GruntStep.Guard) return;
            cannonMeter += damage;
            if (cannonMeter < Settings.guardBreakDamage) return;
            cannonMeter = 0f;
            Step = GruntStep.Stagger;
            Timer = Settings.guardBreakStagger;
            Emit(GruntEventKind.GuardBroken);
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
            var p = robot + away * (Held ? Settings.waitDistance : Settings.engageDistance);
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
            var attacks = Spec.attacks != null && Spec.attacks.Length > 0 ? Spec.attacks : new[] { GruntAttack.Slam };
            Attack = attacks[attackIndex % attacks.Length];
            attackIndex++;
            Enter(GruntStep.Rise, Settings.riseSeconds);
            Emit(GruntEventKind.TelegraphStarted);
        }

        void Impact(bool guarding, PilotSide guardSide)
        {
            var s = Settings;
            if (guarding && guardSide == IncomingSide(Attack))
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
