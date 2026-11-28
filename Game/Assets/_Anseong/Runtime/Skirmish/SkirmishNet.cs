using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>이 PC가 맡은 자리. 호스트 = 메카(P1), 클라이언트 = 공중포대(P2).</summary>
    public enum SkirmishSeat : byte
    {
        Mech,
        Gunner,
    }

    /// <summary>
    /// 서버가 모든 PC에 보내는 교전 상태 한 장. 화면은 이것만 보고 그린다.
    /// 칸을 더하면 <see cref="NetworkSerialize{T}"/>, <see cref="From"/>, <see cref="CopyTo"/> 세 곳을 같이 고친다.
    /// </summary>
    public struct NetSkirmishState : INetworkSerializable
    {
        public const int MaxShells = 8;

        public int Epoch;
        public byte Phase;
        public float Elapsed;
        public Vector3 Mech;
        public float Hull;
        public bool Guarding;
        public Vector3 GuardDirection;
        /// <summary>화면 표시용: 메카 오른팔과 공중포대 포신이 향한 방향.</summary>
        public Vector3 MechAim, GunnerAim;

        public Vector3 Enemy0Position, Enemy1Position;
        public float Enemy0Hp, Enemy1Hp, Enemy0Timer, Enemy1Timer;
        public byte Enemy0Step, Enemy1Step;

        public byte Missiles;
        public sbyte LockTarget;
        public float LockProgress, CannonHeat;
        public bool CannonOverheated;
        public byte Furnace;

        public byte GuardSide;
        public sbyte GruntIndex;
        public Vector3 GruntPosition;
        public float GruntHp, GruntTimer;
        public byte GruntStep, GruntAttack, GruntsDown;
        public bool ShieldOpen, ShieldBroken, SwordBroken;
        public byte Act, GruntSide;
        public float GruntImpactIn, CannonJam, SideStep;
        public byte Finale;
        public float FinaleProgress;
        public bool FinaleMech, FinaleGunner;
        public float StatSeconds, StatTaken, StatGunnerDamage;
        public ushort StatBlocks, StatPerfect, StatDrones;
        public float SlowMotion;
        public float GruntShell, BackHp, BackShell, RebootTimer;
        public sbyte BackIndex;
        public Vector3 BackPosition;
        public byte BackStep, RebootsLeft;
        public float WeakTimer;
        public Vector3 WeakPoint;

        public byte ShellCount;
        public int[] ShellId;
        public byte[] ShellKind;
        public Vector3[] ShellPosition, ShellVelocity;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Epoch); s.SerializeValue(ref Phase); s.SerializeValue(ref Elapsed);
            s.SerializeValue(ref Mech); s.SerializeValue(ref Hull); s.SerializeValue(ref Guarding);
            s.SerializeValue(ref GuardDirection); s.SerializeValue(ref MechAim); s.SerializeValue(ref GunnerAim);
            s.SerializeValue(ref Enemy0Position); s.SerializeValue(ref Enemy1Position);
            s.SerializeValue(ref Enemy0Hp); s.SerializeValue(ref Enemy1Hp);
            s.SerializeValue(ref Enemy0Timer); s.SerializeValue(ref Enemy1Timer);
            s.SerializeValue(ref Enemy0Step); s.SerializeValue(ref Enemy1Step);
            s.SerializeValue(ref Missiles); s.SerializeValue(ref LockTarget);
            s.SerializeValue(ref LockProgress); s.SerializeValue(ref CannonHeat);
            s.SerializeValue(ref CannonOverheated); s.SerializeValue(ref Furnace);
            s.SerializeValue(ref GuardSide); s.SerializeValue(ref GruntIndex); s.SerializeValue(ref GruntPosition);
            s.SerializeValue(ref GruntHp); s.SerializeValue(ref GruntTimer);
            s.SerializeValue(ref GruntStep); s.SerializeValue(ref GruntAttack); s.SerializeValue(ref GruntsDown);
            s.SerializeValue(ref ShieldOpen); s.SerializeValue(ref ShieldBroken); s.SerializeValue(ref SlowMotion);
            s.SerializeValue(ref GruntShell); s.SerializeValue(ref BackHp); s.SerializeValue(ref BackShell); s.SerializeValue(ref RebootTimer);
            s.SerializeValue(ref BackIndex); s.SerializeValue(ref BackPosition); s.SerializeValue(ref BackStep); s.SerializeValue(ref RebootsLeft);
            s.SerializeValue(ref WeakTimer); s.SerializeValue(ref WeakPoint);
            s.SerializeValue(ref SwordBroken); s.SerializeValue(ref Act); s.SerializeValue(ref GruntSide);
            s.SerializeValue(ref GruntImpactIn); s.SerializeValue(ref CannonJam); s.SerializeValue(ref SideStep);
            s.SerializeValue(ref Finale); s.SerializeValue(ref FinaleProgress);
            s.SerializeValue(ref FinaleMech); s.SerializeValue(ref FinaleGunner);
            s.SerializeValue(ref StatSeconds); s.SerializeValue(ref StatTaken); s.SerializeValue(ref StatGunnerDamage);
            s.SerializeValue(ref StatBlocks); s.SerializeValue(ref StatPerfect); s.SerializeValue(ref StatDrones);
            s.SerializeValue(ref ShellCount);
            if (ShellCount > MaxShells) ShellCount = MaxShells;
            if (s.IsReader)
            {
                ShellId = new int[ShellCount]; ShellKind = new byte[ShellCount];
                ShellPosition = new Vector3[ShellCount]; ShellVelocity = new Vector3[ShellCount];
            }
            for (int i = 0; i < ShellCount; i++)
            {
                s.SerializeValue(ref ShellId[i]); s.SerializeValue(ref ShellKind[i]);
                s.SerializeValue(ref ShellPosition[i]); s.SerializeValue(ref ShellVelocity[i]);
            }
        }

        public static NetSkirmishState From(SkirmishState st, Vector3 mechAim, Vector3 gunnerAim)
        {
            int count = Mathf.Min(st.shells.Count, MaxShells);
            var net = new NetSkirmishState
            {
                Epoch = st.epoch, Phase = (byte)st.phase, Elapsed = st.elapsed,
                Mech = st.mech, Hull = st.hull, Guarding = st.guarding, GuardDirection = st.guardDirection,
                MechAim = mechAim, GunnerAim = gunnerAim,
                Enemy0Position = st.enemies[0].position, Enemy1Position = st.enemies[1].position,
                Enemy0Hp = st.enemies[0].hp, Enemy1Hp = st.enemies[1].hp,
                Enemy0Timer = st.enemies[0].timer, Enemy1Timer = st.enemies[1].timer,
                Enemy0Step = (byte)st.enemies[0].step, Enemy1Step = (byte)st.enemies[1].step,
                Missiles = (byte)st.missiles, LockTarget = (sbyte)st.lockTarget,
                LockProgress = st.lockProgress, CannonHeat = st.cannonHeat,
                CannonOverheated = st.cannonOverheated, Furnace = (byte)st.furnace,
                GuardSide = (byte)st.guardSide, GruntIndex = (sbyte)st.gruntIndex, GruntPosition = st.gruntPosition,
                GruntHp = st.gruntHp, GruntTimer = st.gruntTimer,
                GruntStep = (byte)st.gruntStep, GruntAttack = (byte)st.gruntAttack, GruntsDown = (byte)st.gruntsDown,
                ShieldOpen = st.shieldOpen, ShieldBroken = st.shieldBroken, SlowMotion = st.slowMotion,
                GruntShell = st.gruntShell, BackHp = st.backHp, BackShell = st.backShell, RebootTimer = st.rebootTimer,
                BackIndex = (sbyte)st.backIndex, BackPosition = st.backPosition, BackStep = (byte)st.backStep, RebootsLeft = (byte)st.rebootsLeft,
                WeakTimer = st.weakTimer, WeakPoint = st.weakPoint,
                SwordBroken = st.swordBroken, Act = (byte)st.act, GruntSide = (byte)st.gruntSide,
                GruntImpactIn = st.gruntImpactIn, CannonJam = st.cannonJam, SideStep = st.sideStep,
                Finale = (byte)st.finale, FinaleProgress = st.finaleProgress, FinaleMech = st.finaleMech, FinaleGunner = st.finaleGunner,
                StatSeconds = st.statSeconds, StatTaken = st.statTaken, StatGunnerDamage = st.statGunnerDamage,
                StatBlocks = (ushort)st.statBlocks, StatPerfect = (ushort)st.statPerfect, StatDrones = (ushort)st.statDrones,
                ShellCount = (byte)count,
                ShellId = new int[count], ShellKind = new byte[count],
                ShellPosition = new Vector3[count], ShellVelocity = new Vector3[count],
            };
            for (int i = 0; i < count; i++)
            {
                var shell = st.shells[i];
                net.ShellId[i] = shell.id; net.ShellKind[i] = (byte)shell.kind;
                net.ShellPosition[i] = shell.position; net.ShellVelocity[i] = shell.velocity;
            }
            return net;
        }

        /// <summary>받은 상태를 화면용 <see cref="SkirmishState"/>에 옮긴다(규칙 계산에는 쓰지 않는다).</summary>
        public void CopyTo(SkirmishState st)
        {
            st.epoch = Epoch; st.phase = (SkirmishPhase)Phase; st.elapsed = Elapsed;
            st.mech = Mech; st.hull = Hull; st.guarding = Guarding; st.guardDirection = GuardDirection;
            st.enemies ??= new[] { new SkirmishEnemy { id = 0 }, new SkirmishEnemy { id = 1 } };
            st.enemies[0].position = Enemy0Position; st.enemies[1].position = Enemy1Position;
            st.enemies[0].hp = Enemy0Hp; st.enemies[1].hp = Enemy1Hp;
            st.enemies[0].timer = Enemy0Timer; st.enemies[1].timer = Enemy1Timer;
            st.enemies[0].step = (EnemyStep)Enemy0Step; st.enemies[1].step = (EnemyStep)Enemy1Step;
            st.missiles = Missiles; st.lockTarget = LockTarget; st.lockProgress = LockProgress;
            st.cannonHeat = CannonHeat; st.cannonOverheated = CannonOverheated; st.furnace = Furnace;
            st.guardSide = (Anseong.Pilot.PilotSide)GuardSide; st.gruntIndex = GruntIndex; st.gruntPosition = GruntPosition;
            st.gruntHp = GruntHp; st.gruntTimer = GruntTimer;
            st.gruntStep = (GruntStep)GruntStep; st.gruntAttack = (GruntAttack)GruntAttack; st.gruntsDown = GruntsDown;
            st.shieldOpen = ShieldOpen; st.shieldBroken = ShieldBroken; st.slowMotion = SlowMotion;
            st.gruntShell = GruntShell; st.backHp = BackHp; st.backShell = BackShell; st.rebootTimer = RebootTimer;
            st.backIndex = BackIndex; st.backPosition = BackPosition; st.backStep = (GruntStep)BackStep; st.rebootsLeft = RebootsLeft;
            st.weakTimer = WeakTimer; st.weakPoint = WeakPoint;
            st.swordBroken = SwordBroken; st.act = Act; st.gruntSide = (Anseong.Pilot.PilotSide)GruntSide;
            st.gruntImpactIn = GruntImpactIn; st.cannonJam = CannonJam; st.sideStep = SideStep;
            st.finale = (FinaleStep)Finale; st.finaleProgress = FinaleProgress; st.finaleMech = FinaleMech; st.finaleGunner = FinaleGunner;
            st.statSeconds = StatSeconds; st.statTaken = StatTaken; st.statGunnerDamage = StatGunnerDamage;
            st.statBlocks = StatBlocks; st.statPerfect = StatPerfect; st.statDrones = StatDrones;
            st.shells.Clear();
            for (int i = 0; i < ShellCount; i++)
                st.shells.Add(new SkirmishShell
                {
                    id = ShellId[i], kind = (ShellKind)ShellKind[i],
                    position = ShellPosition[i], velocity = ShellVelocity[i],
                });
        }
    }

    /// <summary>
    /// 빈 자리를 대신하는 단순한 자동 조작. 혼자 헤드셋 하나로 시험할 때와 자동 접속 시험에 쓴다.
    /// 잘하도록 만든 것이 아니라 "상대가 뭔가 하고 있는" 상태를 만드는 용도다.
    /// </summary>
    public static class SkirmishBots
    {
        /// <summary>큰 탄이 오면 그쪽으로 가드, 아니면 G3(없으면 G4)를 쏜다.</summary>
        public static MechInput Mech(SkirmishState st, SkirmishSettings s)
        {
            if (st.GruntAlive && st.finale != FinaleStep.None)
            {
                // 마무리: 화면이 시키는 대로 한다(막기, 그다음 주먹).
                return new MechInput
                {
                    guard = st.finale == FinaleStep.Catch, guardDirection = st.gruntPosition - st.mech, guardSide = Anseong.Pilot.PilotSide.Left,
                    aim = (SkirmishSession.GruntCenter(st, s) - SkirmishSession.Gun(st, s)).normalized,
                    punchSeq = Mathf.FloorToInt(st.elapsed / 0.2f), punchDamage = st.finale == FinaleStep.Together ? 25f : 0f,
                };
            }
            if (st.GruntAlive)
            {
                // 돌격형: 예고가 보이면 오는 쪽으로 막고, 튕겨난 틈에 0.6초마다 잽. 큰 탄이 날아오면 그쪽으로 막는다.
                bool telegraph = st.gruntStep == GruntStep.Rise || st.gruntStep == GruntStep.Hold || st.gruntStep == GruntStep.Strike;
                var incoming = st.shells.FirstOrDefault(x => x.kind == ShellKind.BigShell);
                // 제자리에 서 있는 적에게는 걸어가서 맞붙는다.
                var toGrunt = st.gruntPosition - st.mech;
                toGrunt.y = 0f;
                bool walk = st.gruntStep == GruntStep.Approach && s.grunts != null && st.gruntIndex < s.grunts.Length
                            && s.grunts[st.gruntIndex].spec.standStill && toGrunt.magnitude > s.grunt.engageDistance - 1f;
                // 보스의 특별한 공격: 답을 아는 채로 한다(속임 베기의 쪽, 큰 내려베기의 때). 돌진은 닿기 1초 전에 옆으로 비킨다.
                var attack = st.gruntAttack;
                bool big = telegraph && attack == GruntAttack.BigSlam;
                bool guard = big ? st.gruntImpactIn <= 0.2f
                    : telegraph ? attack != GruntAttack.Thrust && attack != GruntAttack.CallSwarm : incoming != null;
                bool step = telegraph && attack == GruntAttack.Thrust && st.gruntImpactIn <= 1f;
                bool atShell = incoming != null && !telegraph;
                return new MechInput
                {
                    move = walk ? toGrunt.normalized : step ? Quaternion.Euler(0f, s.mechYaw, 0f) * Vector3.right : Vector3.zero,
                    guard = guard,
                    guardDirection = atShell ? incoming.position - SkirmishSession.Chest(st, s) : st.gruntPosition - st.mech,
                    guardSide = st.gruntSide,
                    aim = (SkirmishSession.GruntCenter(st, s) - SkirmishSession.Gun(st, s)).normalized,
                    punchSeq = Mathf.FloorToInt(st.elapsed / 0.6f), punchDamage = st.gruntStep == GruntStep.Stagger ? 25f : 0f,
                };
            }
            var shell = st.shells.FirstOrDefault(x => x.kind == ShellKind.BigShell);
            int target = st.enemies[SkirmishSession.G3].Alive ? SkirmishSession.G3 : SkirmishSession.G4;
            return new MechInput
            {
                guard = shell != null,
                guardDirection = shell != null ? shell.position - SkirmishSession.Chest(st, s) : Vector3.forward,
                fire = shell == null && st.enemies[target].Alive,
                aim = (SkirmishSession.Center(st, s, target) - SkirmishSession.Gun(st, s)).normalized,
            };
        }

        /// <summary>G4(없으면 G3)를 기관포로 쏜다. 요격과 미사일은 쓰지 않는다.</summary>
        public static GunnerInput Gunner(SkirmishState st, SkirmishSettings s)
        {
            if (st.GruntAlive && SkirmishSettings.IsBoss(s, st.gruntIndex))
            {
                // 보스: 튕겨난 틈에는 가슴을 쏘고, 그 밖에는 날아오는 자폭 드론을 쏜다.
                var chest = (SkirmishSession.GruntCenter(st, s) - s.gunnerPost).normalized;
                if (st.finale != FinaleStep.None)
                {
                    // 마무리: 가슴의 표시를 쏘고, 같이 끝낼 때는 지시기 버튼을 누르고 있는다.
                    var mark = (st.weakPoint - s.gunnerPost).normalized;
                    return new GunnerInput { fire = st.finale == FinaleStep.Open, aim = mark, pointer = mark, lockHeld = st.finale == FinaleStep.Together };
                }
                if (st.gruntStep == GruntStep.Stagger) return new GunnerInput { fire = true, aim = chest, pointer = chest };
                var drone = st.shells.FirstOrDefault(x => x.kind == ShellKind.BigShell);
                if (drone == null) return new GunnerInput { aim = chest, pointer = chest };
                var at = (drone.position - s.gunnerPost).normalized;
                return new GunnerInput { fire = true, aim = at, pointer = chest };
            }
            if (st.GruntAlive && st.gruntStep == GruntStep.Guard)
            {
                // 가드를 굳힌 돌격형을 쏴서 연다.
                var open = (SkirmishSession.GruntCenter(st, s) - s.gunnerPost).normalized;
                return new GunnerInput { fire = true, aim = open, pointer = open };
            }
            int target = st.enemies[SkirmishSession.G4].Alive ? SkirmishSession.G4 : SkirmishSession.G3;
            var aim = (SkirmishSession.Center(st, s, target) - s.gunnerPost).normalized;
            return new GunnerInput { fire = st.enemies[target].Alive, aim = aim, pointer = aim };
        }
    }
}
