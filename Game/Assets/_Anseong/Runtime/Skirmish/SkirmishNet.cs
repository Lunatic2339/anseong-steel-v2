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
            if (st.GruntAlive)
            {
                // 돌격형: 예고가 보이면 오는 쪽으로 막고, 튕겨난 틈에 0.6초마다 잽.
                bool telegraph = st.gruntStep == GruntStep.Rise || st.gruntStep == GruntStep.Hold || st.gruntStep == GruntStep.Strike;
                return new MechInput
                {
                    guard = telegraph, guardDirection = st.gruntPosition - st.mech, guardSide = GruntDuel.IncomingSide(st.gruntAttack),
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
