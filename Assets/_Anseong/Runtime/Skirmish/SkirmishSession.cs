using System.Collections.Generic;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Skirmish
{
    /// <summary>
    /// Z2 교전 규칙. 서버(호스트)만 돌린다. MonoBehaviour·네트워크와 분리된 순수 클래스.
    /// G3는 메카에게 큰 탄을 쏘고, G4는 자리를 잡고 고로를 포격한다.
    /// 공중포대는 기관포로 직접 쏘거나 큰 탄을 요격하고, 미사일을 누구에게 쓸지 고른다.
    /// 같은 입력을 같은 순서로 넣으면 항상 같은 결과가 나온다(난수 없음).
    /// </summary>
    public sealed class SkirmishSession
    {
        public const int G3 = 0, G4 = 1;
        /// <summary>강한 탄(미사일)이 맞붙은 적(보스)을 잠갔을 때의 표적 번호.</summary>
        public const int GruntTarget = 2;

        public readonly SkirmishSettings Settings;
        public SkirmishState State { get; private set; }
        /// <summary>마지막 Tick에서 일어난 일.</summary>
        public readonly List<SkirmishEvent> Events = new List<SkirmishEvent>();

        MechInput mech;
        GunnerInput gunner;
        float mechGunCooldown, cannonCooldown;
        bool lockWasHeld, lockCancelled;
        int nextShellId;
        GruntDuel grunt;
        float gruntWait;
        int lastPunchSeq;
        Vector3 mechVelocity;
        bool shieldBreakPending;
        GruntDuel back;         // 뒤에서 기다리는 돌격형
        int nextWave;           // 다음에 내보낼 돌격형 번호
        bool waveShellDone;     // 맞붙을 적의 "지나가면 한 발"을 이미 쐈는가
        bool actEnded;          // 맞붙은 적의 체력이 endAtHp까지 내려가 싸움이 끝남
        float droneTimer;       // 다음 자폭 드론까지 남은 초

        /// <summary>P2가 없을 때 true. 돌격형의 가드가 스스로 더 빨리 풀린다.</summary>
        public bool Solo;
        /// <summary>지금 나와 있는 돌격형의 규칙(서버 전용). 없으면 null.</summary>
        public GruntDuel Grunt => grunt;
        /// <summary>뒤에서 기다리는 돌격형(서버 전용). 없으면 null.</summary>
        public GruntDuel WaitingGrunt => back;

        public SkirmishSession(SkirmishSettings settings = null)
        {
            Settings = settings ?? new SkirmishSettings();
            State = NewState(1);
        }

        SkirmishState NewState(int epoch)
        {
            var s = Settings;
            var first = s.rangedEnemies ? EnemyStep.Advance : EnemyStep.Dead;
            return new SkirmishState
            {
                epoch = epoch,
                mech = s.mechStart,
                hull = s.mechHull,
                missiles = s.missiles,
                furnace = s.furnaceHits,
                rebootsLeft = s.reboots,
                enemies = new[]
                {
                    new SkirmishEnemy { id = G3, position = s.g3Start, hp = s.rangedEnemies ? s.g3Hp : 0f, step = first },
                    new SkirmishEnemy { id = G4, position = s.g4Start, hp = s.rangedEnemies ? s.g4Hp : 0f, step = first },
                },
            };
        }

        /// <summary>처음 상태로 되돌린다. 날아가던 탄도 사라지고 두 사람 모두 다시 준비해야 한다.</summary>
        public void Reset()
        {
            State = NewState(State.epoch + 1);
            Events.Clear();
            mech = default;
            gunner = default;
            mechGunCooldown = cannonCooldown = 0f;
            lockWasHeld = lockCancelled = false;
            grunt = null;
            gruntWait = 0f;
            lastPunchSeq = 0;
            mechVelocity = Vector3.zero;
            shieldBreakPending = false;
            back = null;
            nextWave = 0;
            waveShellDone = false;
            actEnded = false;
            droneTimer = 0f;
        }

        /// <summary>재기동이 끝났을 때: 그 장면의 적과 탄을 처음으로 돌리고 로봇 내구도를 100으로. 재기동 횟수는 되돌리지 않는다.</summary>
        void RestartScene()
        {
            var old = State;
            int left = old.rebootsLeft;
            Reset();
            var st = State;
            st.rebootsLeft = left;
            st.mechReady = old.mechReady; st.gunnerReady = old.gunnerReady;
            st.phase = SkirmishPhase.Fighting;
            if (Settings.restartElsewhere) st.mech = Settings.mechRestart;
            Emit(SkirmishEventKind.SceneRestarted, -1, st.mech, st.mech);
        }

        /// <summary>아군 드론이 돌격형을 멈춰 세운다. index는 돌격형 번호. 맞붙은 적도 기다리는 적도 된다. 그 번호의 적이 없으면 false.</summary>
        public bool FreezeGrunt(int index, float seconds)
        {
            var st = State;
            var target = grunt != null && grunt.Alive && st.gruntIndex == index ? grunt
                : back != null && st.backIndex == index ? back : null;
            if (target == null) return false;
            target.Freeze(seconds);
            if (target == grunt) CopyGrunt(); else CopyBack();
            return true;
        }

        /// <summary>
        /// 차폐판을 부순다(장면 6의 보스 두 손 내려찍기가 부른다). 다음 틱에 반영되고 <see cref="SkirmishEventKind.ShieldBroken"/>이 한 번 나온다.
        /// 부서진 뒤에도 막기 판정은 같다(같은 왼팔, 같은 자리).
        /// </summary>
        public void BreakShield() => shieldBreakPending = true;

        public void SetReady(bool mechReady, bool gunnerReady)
        {
            State.mechReady = mechReady;
            State.gunnerReady = gunnerReady;
            if (State.phase == SkirmishPhase.Waiting && mechReady && gunnerReady) State.phase = SkirmishPhase.Fighting;
        }

        public void SetMech(MechInput input) => mech = input;
        public void SetGunner(GunnerInput input) => gunner = input;

        public Vector3 MechChest => Chest(State, Settings);
        public Vector3 MechGun => Gun(State, Settings);
        public Vector3 EnemyCenter(int id) => Center(State, Settings, id);
        public Vector3 EnemyMuzzle(int id) => Muzzle(State, Settings, id);

        // 상태만 있는 쪽(클라이언트 화면, 자동 조작)도 같은 위치 계산을 쓴다.
        public static Vector3 Chest(SkirmishState state, SkirmishSettings s) => state.mech + Vector3.up * s.mechChestHeight;
        public static Vector3 Gun(SkirmishState state, SkirmishSettings s) => state.mech + Vector3.up * s.mechGunHeight;
        public static Vector3 Center(SkirmishState state, SkirmishSettings s, int id) => state.enemies[id].position + Vector3.up * (s.enemyHeight * 0.5f);
        public static Vector3 Muzzle(SkirmishState state, SkirmishSettings s, int id) => state.enemies[id].position + Vector3.up * s.enemyMuzzleHeight;
        public static Vector3 GruntCenter(SkirmishState state, SkirmishSettings s) => state.gruntPosition + Vector3.up * (s.gruntHeight * 0.5f);

        public void Tick(float dt)
        {
            Events.Clear();
            var st = State;
            if (st.phase == SkirmishPhase.Rebooting && dt > 0f)
            {
                st.rebootTimer -= dt;
                if (st.rebootTimer <= 0f) RestartScene();
                return;
            }
            if (st.phase != SkirmishPhase.Fighting || dt <= 0f) return;

            // 딱 맞춰 막은 직후에는 세상의 시간이 느리게 흐른다. 로봇의 이동만은 평소 속도다(눈이 타고 있는 몸의 속도가 갑자기 바뀌면 멀미가 난다).
            float world = dt;
            if (st.slowMotion > 0f)
            {
                float slow = Mathf.Min(dt, st.slowMotion);
                world = slow * Settings.perfectSlowScale + (dt - slow);
                st.slowMotion -= slow;
            }
            st.elapsed += world;

            MoveMech(dt);
            if (shieldBreakPending)
            {
                shieldBreakPending = false;
                if (!st.shieldBroken)
                {
                    st.shieldBroken = true;
                    st.shieldOpen = false;
                    Emit(SkirmishEventKind.ShieldBroken, -1, MechChest, MechChest);
                }
            }
            TickGrunt(world);
            TickEnemy(st.enemies[G3], world);
            TickEnemy(st.enemies[G4], world);
            MechGunFire(world);
            CannonFire(world);
            MissileLock(world);
            TickShells(world);
            Resolve();
        }

        void MoveMech(float dt)
        {
            var st = State; var s = Settings;
            var move = new Vector3(mech.move.x, 0f, mech.move.z);
            if (SkirmishMath.Finite(move))
            {
                // 속도를 바로 바꾸지 않고 올리고 내린다(출발·정지가 부드럽게).
                var wanted = Vector3.ClampMagnitude(move, 1f) * s.mechSpeed;
                bool slowing = wanted.sqrMagnitude < mechVelocity.sqrMagnitude;
                float seconds = slowing ? s.mechStopSeconds : s.mechAccelSeconds;
                mechVelocity = seconds <= 0f ? wanted : Vector3.MoveTowards(mechVelocity, wanted, s.mechSpeed / seconds * dt);
                var p = st.mech + mechVelocity * dt;
                Inside(s, p, out float x, out float z);
                // 벽에 닿으면 그 방향 속도는 버린다.
                if (x != p.x) mechVelocity.x = 0f;
                if (z != p.z) mechVelocity.z = 0f;
                st.mech = new Vector3(x, p.y, z);
            }
            st.guarding = mech.guard;
            st.guardDirection = SkirmishMath.Flat(mech.guardDirection);
            st.guardSide = mech.guardSide;
            st.shieldOpen = !st.shieldBroken && !mech.shieldFolded;
        }

        /// <summary>갈 수 있는 곳 안에서 p와 가장 가까운 점. 범위가 여러 칸이면 그 가운데 가장 가까운 칸으로 붙는다.</summary>
        static void Inside(SkirmishSettings s, Vector3 p, out float x, out float z)
        {
            if (s.mechZones == null || s.mechZones.Length == 0)
            {
                x = Mathf.Clamp(p.x, s.mechArea.x, s.mechArea.z);
                z = Mathf.Clamp(p.z, s.mechArea.y, s.mechArea.w);
                return;
            }
            x = p.x; z = p.z;
            float best = float.MaxValue;
            foreach (var zone in s.mechZones)
            {
                float cx = Mathf.Clamp(p.x, zone.x, zone.z), cz = Mathf.Clamp(p.z, zone.y, zone.w);
                float d = (cx - p.x) * (cx - p.x) + (cz - p.z) * (cz - p.z);
                if (d >= best) continue;
                best = d; x = cx; z = cz;
            }
        }

        /// <summary>메카의 지금 속도(m/s).</summary>
        public Vector3 MechVelocity => mechVelocity;

        /// <summary>돌격형은 한 번에 한 명. 앞의 적이 쓰러지면 gruntGap 뒤에 다음이 나온다.</summary>
        void TickGrunt(float dt)
        {
            var st = State; var s = Settings;
            bool punched = mech.punchSeq != lastPunchSeq && mech.punchDamage > 0f;
            lastPunchSeq = mech.punchSeq;
            if (s.grunts == null || s.grunts.Length == 0 || EndAct()) return;
            if (grunt == null || !grunt.Alive)
            {
                if (back != null)
                {
                    // 한 번에 한 명: 맞붙던 적이 쓰러지면 기다리던 적이 잠시 뒤에 들어온다. 그동안에도 큰 탄은 쏜다.
                    gruntWait -= dt;
                    if (gruntWait > 0f) { TickBack(dt); return; }
                    grunt = back;
                    back = null;
                    grunt.Release();
                    st.gruntIndex = st.backIndex;
                    st.backIndex = -1;
                    st.backShell = 0f;
                    waveShellDone = true;
                }
                else
                {
                    if (nextWave >= s.grunts.Length) return;
                    gruntWait -= dt;
                    if (gruntWait > 0f) return;
                    var wave = s.grunts[nextWave];
                    grunt = new GruntDuel(wave.start, wave.spec, s.grunt, Solo);
                    st.gruntIndex = nextWave++;
                    waveShellDone = false;
                    droneTimer = DroneInterval;
                    // 같이 출발하는 적은 35 거리에서 기다린다. 혼자 할 때는 앞의 적이 쓰러진 뒤에 따로 출발한다.
                    if (!Solo && nextWave < s.grunts.Length && s.grunts[nextWave].withPrevious)
                    {
                        var behind = s.grunts[nextWave];
                        back = new GruntDuel(behind.start, behind.spec, s.grunt);
                        back.Hold();
                        st.backIndex = nextWave++;
                    }
                }
            }
            grunt.Solo = Solo;
            // 위태로울수록 조금 쉬워진다: 내구도가 낮으면 예고가 길어진다.
            grunt.TelegraphScale = s.lowHullBelow > 0f && st.hull < s.lowHullBelow ? s.lowHullTelegraphScale : 1f;
            grunt.Tick(dt, st.mech, st.guarding, st.guardSide);
            if (punched) grunt.Punch(mech.punchDamage);
            TickDrones(dt);
            // 서 있는 적: 로봇이 정해진 선을 지나면 큰 탄을 한 번 겨눈다.
            float line = s.grunts[st.gruntIndex].shellWhenRobotPassesZ;
            if (!waveShellDone && line > 0f && st.mech.z >= line)
            {
                waveShellDone = true;
                grunt.BeginShell();
            }
            foreach (var e in grunt.Events)
            {
                if (!ShellEvent(grunt, st.gruntIndex, e)) OnGruntEvent(e);
            }
            CopyGrunt();
            TickBack(dt);
        }

        float DroneInterval => Settings.droneEvery * (Solo ? 2f : 1f);

        /// <summary>맞붙은 동안 자폭 드론이 하나씩 로봇 가슴으로 온다. 큰 탄과 같은 규칙이다(P2가 2발로 요격, P1이 막으면 피해 없음).</summary>
        void TickDrones(float dt)
        {
            if (Settings.droneEvery <= 0f || grunt == null || !grunt.Alive || grunt.Step == GruntStep.Approach) return;
            droneTimer -= dt;
            if (droneTimer > 0f) return;
            droneTimer += DroneInterval;
            FireBigShell(Settings.droneFrom);
        }

        /// <summary>맞붙은 적의 체력이 그 막의 끝(endAtHp)까지 내려갔으면 싸움을 끝낸다. 끝났으면 true.</summary>
        bool EndAct()
        {
            if (actEnded) return true;
            var st = State;
            if (grunt == null || !grunt.Alive || st.gruntIndex < 0) return false;
            var wave = Settings.grunts[st.gruntIndex];
            if (wave.endAtHp <= 0f) return false;
            // 혼자 할 때는 체력이 낮게 시작하므로 끝도 같은 비율로 낮춘다.
            float end = wave.endAtHp * grunt.MaxHp / Mathf.Max(1f, wave.spec.hp);
            if (grunt.Hp > end) return false;
            actEnded = true;
            st.gruntsDown++;
            CopyGrunt();
            // 날아오던 자폭 드론은 같이 사라진다(막이 끝난 뒤에 맞지 않게).
            st.shells.RemoveAll(x => x.kind == ShellKind.BigShell);
            return true;
        }

        bool GruntLockable => grunt != null && grunt.Alive && !actEnded && grunt.Spec.strongShotTarget && grunt.Step != GruntStep.Approach;

        /// <summary>강한 탄이 잠글 수 있는 것 가운데 지시선이 처음 닿는 것. 없으면 -1.</summary>
        int LockOnRay(Vector3 origin, Vector3 direction)
        {
            int enemy = EnemyOnRay(origin, direction, out float enemyDistance);
            return GruntLockable && GruntOnRay(origin, direction, out float gruntDistance) && gruntDistance < enemyDistance ? GruntTarget : enemy;
        }

        Vector3 TargetCenter(int id) => id == GruntTarget ? GruntCenter(State, Settings) : EnemyCenter(id);

        void TickBack(float dt)
        {
            if (back == null) return;
            back.Solo = Solo;
            back.FireShells = !Solo;
            back.Tick(dt, State.mech, false, PilotSide.Left);
            foreach (var e in back.Events) ShellEvent(back, State.backIndex, e);
            CopyBack();
        }

        void CopyBack()
        {
            var st = State;
            st.backPosition = back.Position; st.backHp = back.Hp; st.backStep = back.Step; st.backShell = back.ShellWindup;
        }

        /// <summary>돌격형의 큰 탄 사건이면 처리하고 true.</summary>
        bool ShellEvent(GruntDuel from, int index, GruntEvent e)
        {
            var muzzle = from.Position + Vector3.up * (Settings.gruntHeight * 0.6f);
            if (e.Kind == GruntEventKind.ShellWindupStarted)
            {
                Emit(SkirmishEventKind.GruntShellWindup, index, muzzle, MechChest);
                return true;
            }
            if (e.Kind != GruntEventKind.ShellFired) return false;
            FireBigShell(muzzle);
            return true;
        }

        /// <summary>메카 가슴을 향해 느린 큰 탄을 쏜다. 옆으로 피하면 지나가서 사라진다.</summary>
        void FireBigShell(Vector3 from)
        {
            var s = Settings;
            var to = MechChest;
            var shell = new SkirmishShell
            {
                id = nextShellId++, kind = ShellKind.BigShell, position = from,
                velocity = (to - from).normalized * s.bigShellSpeed, target = -1, hitsLeft = s.bigShellHits,
                life = Vector3.Distance(from, to) / s.bigShellSpeed + 1.5f,
            };
            State.shells.Add(shell);
            Emit(SkirmishEventKind.ShellFired, shell.id, from, to);
        }

        void CopyGrunt()
        {
            var st = State;
            st.gruntPosition = grunt.Position; st.gruntHp = grunt.Hp; st.gruntStep = grunt.Step;
            st.gruntTimer = grunt.Timer; st.gruntAttack = grunt.Attack; st.gruntShell = grunt.ShellWindup;
        }

        void OnGruntEvent(GruntEvent e)
        {
            var st = State;
            var center = grunt.Position + Vector3.up * (Settings.gruntHeight * 0.5f);
            switch (e.Kind)
            {
                case GruntEventKind.Arrived: Emit(SkirmishEventKind.GruntArrived, st.gruntIndex, center, MechChest); break;
                case GruntEventKind.TelegraphStarted: Emit(SkirmishEventKind.GruntTelegraph, (int)e.Attack, center, MechChest); break;
                case GruntEventKind.Blocked: Emit(SkirmishEventKind.GruntBlocked, (int)e.Attack, center, MechChest); break;
                case GruntEventKind.PerfectBlocked:
                    st.slowMotion = Settings.perfectSlowSeconds;
                    Emit(SkirmishEventKind.GruntPerfectBlock, (int)e.Attack, center, MechChest);
                    break;
                case GruntEventKind.RobotHit:
                    st.hull = Mathf.Max(0f, st.hull - e.Amount);
                    Emit(SkirmishEventKind.MechHit, -1, center, MechChest);
                    break;
                case GruntEventKind.PunchHit: Emit(SkirmishEventKind.PunchHit, st.gruntIndex, MechChest, center); break;
                case GruntEventKind.PunchBounced: Emit(SkirmishEventKind.PunchBounced, st.gruntIndex, MechChest, center); break;
                case GruntEventKind.GuardClosed: Emit(SkirmishEventKind.GuardClosed, st.gruntIndex, center, center); break;
                case GruntEventKind.GuardBroken: Emit(SkirmishEventKind.GuardBroken, st.gruntIndex, center, center); break;
                case GruntEventKind.Down:
                    st.gruntsDown++;
                    gruntWait = back != null ? Settings.gruntSwapSeconds : Settings.gruntGap;
                    Emit(SkirmishEventKind.GruntDown, st.gruntIndex, center, center);
                    break;
            }
        }

        /// <summary>조준선이 살아 있는 돌격형에 닿는 거리. 안 닿으면 false.</summary>
        bool GruntOnRay(Vector3 origin, Vector3 direction, out float distance)
        {
            distance = float.MaxValue;
            if (grunt == null || !grunt.Alive) return false;
            var s = Settings;
            var a = grunt.Position + Vector3.up * s.gruntRadius;
            var b = grunt.Position + Vector3.up * (s.gruntHeight - s.gruntRadius);
            if (SkirmishMath.RayToSegment(origin, direction, a, b, out float t) > s.gruntRadius) return false;
            distance = t;
            return true;
        }

        void TickEnemy(SkirmishEnemy e, float dt)
        {
            if (!e.Alive) return;
            var s = Settings;
            bool isG4 = e.id == G4;
            e.staggerImmunity = Mathf.Max(0f, e.staggerImmunity - dt);
            switch (e.step)
            {
                case EnemyStep.Advance:
                    var post = isG4 ? s.g4Post : s.g3Post;
                    e.position = Vector3.MoveTowards(e.position, post, (isG4 ? s.g4Speed : s.g3Speed) * dt);
                    if ((e.position - post).sqrMagnitude < 0.0001f) { e.atPost = true; BeginWindup(e); }
                    break;
                case EnemyStep.Windup:
                    e.timer -= dt;
                    if (e.timer > 0f) break;
                    Fire(e);
                    e.step = EnemyStep.Recover;
                    e.timer = isG4 ? s.g4Recover : s.g3Recover;
                    break;
                case EnemyStep.Recover:
                    e.timer -= dt;
                    if (e.timer <= 0f) BeginWindup(e);
                    break;
                case EnemyStep.Stagger:
                    e.timer -= dt;
                    if (e.timer > 0f) break;
                    if (e.atPost) BeginWindup(e); else e.step = EnemyStep.Advance;
                    break;
            }
        }

        void BeginWindup(SkirmishEnemy e)
        {
            e.step = EnemyStep.Windup;
            e.timer = e.id == G4 ? Settings.g4Charge : Settings.g3Windup;
            e.windupDamage = 0f;
            Emit(SkirmishEventKind.WindupStarted, e.id, EnemyMuzzle(e.id), e.id == G4 ? Settings.furnacePoint : MechChest);
        }

        void Fire(SkirmishEnemy e)
        {
            var s = Settings;
            bool isG4 = e.id == G4;
            var from = EnemyMuzzle(e.id);
            var to = isG4 ? s.furnacePoint : MechChest;
            float speed = isG4 ? s.furnaceShellSpeed : s.bigShellSpeed;
            float distance = Vector3.Distance(from, to);
            var shell = new SkirmishShell
            {
                id = nextShellId++,
                kind = isG4 ? ShellKind.FurnaceShell : ShellKind.BigShell,
                position = from,
                velocity = (to - from).normalized * speed,
                target = -1,
                hitsLeft = isG4 ? 0 : s.bigShellHits,
                // 큰 탄은 메카가 옆으로 피하면 지나가서 사라진다.
                life = distance / speed + (isG4 ? 0f : 1.5f),
            };
            State.shells.Add(shell);
            Emit(SkirmishEventKind.ShellFired, shell.id, from, to);
        }

        void Damage(SkirmishEnemy e, float amount, bool heavy)
        {
            if (!e.Alive) return;
            e.hp -= amount;
            if (e.hp <= 0f)
            {
                e.hp = 0f;
                e.step = EnemyStep.Dead;
                if (State.lockTarget == e.id) { State.lockTarget = -1; State.lockProgress = 0f; }
                Emit(SkirmishEventKind.EnemyDown, e.id, EnemyCenter(e.id), EnemyCenter(e.id));
                return;
            }
            bool stagger = heavy;
            if (!heavy && e.step == EnemyStep.Windup)
            {
                e.windupDamage += amount;
                stagger = e.windupDamage >= (e.id == G4 ? Settings.g4InterruptDamage : Settings.g3InterruptDamage);
            }
            if (!stagger || e.staggerImmunity > 0f || e.step == EnemyStep.Stagger) return;
            e.step = EnemyStep.Stagger;
            e.timer = Settings.staggerSeconds;
            e.staggerImmunity = Settings.staggerSeconds + Settings.staggerImmunity;
            e.windupDamage = 0f;
            Emit(SkirmishEventKind.EnemyStaggered, e.id, EnemyCenter(e.id), EnemyCenter(e.id));
        }

        /// <summary>조준선이 처음 닿는 살아 있는 적. 없으면 -1.</summary>
        public int EnemyOnRay(Vector3 origin, Vector3 direction, out float distance)
        {
            int best = -1;
            distance = float.MaxValue;
            var s = Settings;
            foreach (var e in State.enemies)
            {
                if (!e.Alive) continue;
                var a = e.position + Vector3.up * s.enemyRadius;
                var b = e.position + Vector3.up * (s.enemyHeight - s.enemyRadius);
                if (SkirmishMath.RayToSegment(origin, direction, a, b, out float t) > s.enemyRadius || t >= distance) continue;
                distance = t;
                best = e.id;
            }
            return best;
        }

        SkirmishShell BigShellOnRay(Vector3 origin, Vector3 direction, out float distance)
        {
            SkirmishShell best = null;
            distance = float.MaxValue;
            foreach (var shell in State.shells)
            {
                if (shell.kind != ShellKind.BigShell) continue;
                if (SkirmishMath.RayToSegment(origin, direction, shell.position, shell.position, out float t) > Settings.bigShellRadius || t >= distance) continue;
                distance = t;
                best = shell;
            }
            return best;
        }

        void MechGunFire(float dt)
        {
            mechGunCooldown -= dt;
            var aim = mech.aim;
            if (!Settings.mechGun || !mech.fire || mechGunCooldown > 0f || !SkirmishMath.Direction(ref aim)) return;
            mechGunCooldown = Settings.mechGunInterval;
            int hit = EnemyOnRay(MechGun, aim, out float d);
            Emit(SkirmishEventKind.MechGunShot, hit, MechGun, MechGun + aim * (hit >= 0 ? d : 400f));
            if (hit >= 0) Damage(State.enemies[hit], Settings.mechGunDamage, false);
        }

        void CannonFire(float dt)
        {
            var st = State; var s = Settings;
            st.cannonHeat = Mathf.Max(0f, st.cannonHeat - s.cannonCooling * dt);
            if (st.cannonOverheated && st.cannonHeat <= s.cannonResumeHeat) st.cannonOverheated = false;
            cannonCooldown -= dt;
            var aim = gunner.aim;
            if (!gunner.fire || st.cannonOverheated || cannonCooldown > 0f || !SkirmishMath.Direction(ref aim)) return;
            cannonCooldown = s.cannonInterval;
            st.cannonHeat += s.cannonHeatPerShot;
            if (st.cannonHeat >= 1f) { st.cannonHeat = 1f; st.cannonOverheated = true; }

            int enemy = EnemyOnRay(s.gunnerPost, aim, out float enemyDistance);
            var shell = BigShellOnRay(s.gunnerPost, aim, out float shellDistance);
            bool shellFirst = shell != null && shellDistance < enemyDistance;
            // 돌격형이 탄이나 다른 적보다 앞에 있으면 돌격형이 맞는다(가드 깨기).
            if (GruntOnRay(s.gunnerPost, aim, out float gruntDistance) && gruntDistance < Mathf.Min(enemyDistance, shellDistance))
            {
                Emit(SkirmishEventKind.CannonShot, -1, s.gunnerPost, s.gunnerPost + aim * gruntDistance);
                // 막이 끝난 뒤에는 더 깎이지 않는다.
                if (actEnded) return;
                grunt.CannonHit(s.cannonDamage);
                foreach (var e in grunt.Events)
                    if (e.Kind == GruntEventKind.GuardBroken) OnGruntEvent(e);
                CopyGrunt();
                return;
            }
            float reach = shellFirst ? shellDistance : enemy >= 0 ? enemyDistance : 400f;
            Emit(SkirmishEventKind.CannonShot, shellFirst ? shell.id : enemy, s.gunnerPost, s.gunnerPost + aim * reach);
            if (shellFirst)
            {
                if (--shell.hitsLeft > 0) return;
                st.shells.Remove(shell);
                Emit(SkirmishEventKind.ShellIntercepted, shell.id, shell.position, shell.position);
            }
            else if (enemy >= 0) Damage(st.enemies[enemy], s.cannonDamage, false);
        }

        void MissileLock(float dt)
        {
            var st = State; var s = Settings;
            if (gunner.cancel) { lockCancelled = true; st.lockTarget = -1; st.lockProgress = 0f; }
            if (gunner.lockHeld)
            {
                if (!lockCancelled)
                {
                    var pointer = gunner.pointer;
                    int target = SkirmishMath.Direction(ref pointer) ? LockOnRay(s.gunnerPost, pointer) : -1;
                    if (target < 0 || st.missiles <= 0) { st.lockTarget = -1; st.lockProgress = 0f; }
                    else if (target != st.lockTarget) { st.lockTarget = target; st.lockProgress = 0f; }
                    else st.lockProgress = Mathf.Min(1f, st.lockProgress + dt / s.lockSeconds);
                }
            }
            else
            {
                // 놓는 순간: 잠금이 끝났을 때만 발사하고 미사일을 쓴다.
                if (lockWasHeld && !lockCancelled && st.lockTarget >= 0 && st.lockProgress >= 1f && st.missiles > 0)
                {
                    st.missiles--;
                    var shell = new SkirmishShell
                    {
                        id = nextShellId++, kind = ShellKind.Missile, position = s.gunnerPost,
                        velocity = (TargetCenter(st.lockTarget) - s.gunnerPost).normalized * s.missileSpeed,
                        target = st.lockTarget, life = 12f,
                    };
                    st.shells.Add(shell);
                    Emit(SkirmishEventKind.MissileFired, shell.id, s.gunnerPost, TargetCenter(st.lockTarget));
                }
                st.lockTarget = -1;
                st.lockProgress = 0f;
                lockCancelled = false;
            }
            lockWasHeld = gunner.lockHeld;
        }

        void TickShells(float dt)
        {
            var st = State; var s = Settings;
            for (int i = st.shells.Count - 1; i >= 0; i--)
            {
                var shell = st.shells[i];
                shell.life -= dt;
                bool done = false;
                switch (shell.kind)
                {
                    case ShellKind.BigShell:
                        shell.position += shell.velocity * dt;
                        if (Vector3.Distance(shell.position, MechChest) <= s.bigShellContact)
                        {
                            var incoming = SkirmishMath.Flat(shell.position - shell.velocity * 0.5f - MechChest);
                            bool blocked = st.guarding && st.guardDirection.sqrMagnitude > 0f &&
                                           Vector3.Angle(st.guardDirection, incoming) <= s.guardDegrees;
                            if (blocked) Emit(SkirmishEventKind.ShellBlocked, shell.id, shell.position, MechChest);
                            else
                            {
                                st.hull = Mathf.Max(0f, st.hull - s.bigShellDamage);
                                Emit(SkirmishEventKind.MechHit, shell.id, shell.position, MechChest);
                            }
                            done = true;
                        }
                        else if (shell.life <= 0f)
                        {
                            Emit(SkirmishEventKind.ShellMissed, shell.id, shell.position, shell.position);
                            done = true;
                        }
                        break;

                    case ShellKind.FurnaceShell:
                        shell.position += shell.velocity * dt;
                        if (shell.life <= 0f)
                        {
                            st.furnace = Mathf.Max(0, st.furnace - 1);
                            Emit(SkirmishEventKind.FurnaceHit, shell.id, s.furnacePoint, s.furnacePoint);
                            done = true;
                        }
                        break;

                    case ShellKind.Missile:
                        bool atGrunt = shell.target == GruntTarget;
                        var enemy = atGrunt ? null : st.enemies[shell.target];
                        if (!(atGrunt ? GruntLockable : enemy.Alive) || shell.life <= 0f)
                        {
                            // 표적이 먼저 쓰러졌으면 미사일은 그대로 소모된다.
                            Emit(SkirmishEventKind.MissileLost, shell.id, shell.position, shell.position);
                            done = true;
                            break;
                        }
                        var center = TargetCenter(shell.target);
                        float step = s.missileSpeed * dt;
                        if (Vector3.Distance(shell.position, center) <= step + (atGrunt ? s.gruntRadius : s.enemyRadius))
                        {
                            Emit(SkirmishEventKind.MissileHit, shell.target, shell.position, center);
                            if (!atGrunt) Damage(enemy, s.missileDamage, true);
                            else if (grunt.StrongHit(s.missileDamage, s.staggerSeconds))
                            {
                                // 강한 탄은 끊는 패다: 어떤 예고든 끊기고 튕겨난다.
                                Emit(SkirmishEventKind.EnemyStaggered, GruntTarget, center, center);
                                CopyGrunt();
                            }
                            done = true;
                        }
                        else
                        {
                            shell.velocity = (center - shell.position).normalized * s.missileSpeed;
                            shell.position += shell.velocity * dt;
                        }
                        break;
                }
                if (done) st.shells.RemoveAt(i);
            }
        }

        void Resolve()
        {
            var st = State;
            if (st.hull <= 0f || st.furnace <= 0)
            {
                if (st.rebootsLeft > 0)
                {
                    // 한 판에 한 번: 3초 "기체 재기동" 뒤에 그 장면의 처음부터.
                    st.rebootsLeft--;
                    st.rebootTimer = Settings.rebootSeconds;
                    st.phase = SkirmishPhase.Rebooting;
                    Emit(SkirmishEventKind.Rebooting, st.furnace <= 0 ? G4 : G3, st.mech, st.mech);
                    return;
                }
                st.phase = SkirmishPhase.Failed;
                Emit(SkirmishEventKind.Failed, st.furnace <= 0 ? G4 : G3, st.mech, st.mech);
                return;
            }
            if (st.enemies[G3].Alive || st.enemies[G4].Alive) return;
            int gruntCount = Settings.grunts != null ? Settings.grunts.Length : 0;
            if (st.gruntsDown < gruntCount) return;
            foreach (var shell in st.shells)
                if (shell.kind != ShellKind.Missile) return;
            st.phase = SkirmishPhase.Cleared;
            Emit(SkirmishEventKind.Cleared, -1, st.mech, st.mech);
        }

        void Emit(SkirmishEventKind kind, int id, Vector3 from, Vector3 to) =>
            Events.Add(new SkirmishEvent { Kind = kind, Id = id, From = from, To = to });
    }

    /// <summary>교전 판정에 쓰는 기하 계산. 순수 함수.</summary>
    public static class SkirmishMath
    {
        public static bool Finite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary>수평 방향만 남긴 단위 벡터. 거의 수직이면 0.</summary>
        public static Vector3 Flat(Vector3 v)
        {
            if (!Finite(v)) return Vector3.zero;
            v.y = 0f;
            return v.sqrMagnitude < 1e-8f ? Vector3.zero : v.normalized;
        }

        /// <summary>쓸 수 있는 방향이면 단위 벡터로 바꾸고 true.</summary>
        public static bool Direction(ref Vector3 v)
        {
            if (!Finite(v) || v.sqrMagnitude < 1e-8f) return false;
            v.Normalize();
            return true;
        }

        /// <summary>
        /// 반직선(origin + direction·t, t ≥ 0)과 선분 a–b 사이의 가장 가까운 거리.
        /// <paramref name="along"/>은 그때의 반직선 위 거리. direction은 단위 벡터.
        /// </summary>
        public static float RayToSegment(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, out float along)
        {
            var v = b - a;
            var w = origin - a;
            float bDot = Vector3.Dot(direction, v);
            float c = Vector3.Dot(v, v);
            float d = Vector3.Dot(direction, w);
            float e = Vector3.Dot(v, w);
            float denominator = c - bDot * bDot;
            float s = denominator > 1e-6f ? Mathf.Clamp01((e - bDot * d) / denominator) : 0f;
            along = bDot * s - d;
            if (along < 0f)
            {
                along = 0f;
                s = c > 1e-6f ? Mathf.Clamp01(e / c) : 0f;
            }
            return Vector3.Distance(origin + direction * along, a + v * s);
        }
    }
}
