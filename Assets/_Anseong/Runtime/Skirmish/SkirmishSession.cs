using System.Collections.Generic;
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

        public readonly SkirmishSettings Settings;
        public SkirmishState State { get; private set; }
        /// <summary>마지막 Tick에서 일어난 일.</summary>
        public readonly List<SkirmishEvent> Events = new List<SkirmishEvent>();

        MechInput mech;
        GunnerInput gunner;
        float mechGunCooldown, cannonCooldown;
        bool lockWasHeld, lockCancelled;
        int nextShellId;

        public SkirmishSession(SkirmishSettings settings = null)
        {
            Settings = settings ?? new SkirmishSettings();
            State = NewState(1);
        }

        SkirmishState NewState(int epoch)
        {
            var s = Settings;
            return new SkirmishState
            {
                epoch = epoch,
                mech = s.mechStart,
                hull = s.mechHull,
                missiles = s.missiles,
                furnace = s.furnaceHits,
                enemies = new[]
                {
                    new SkirmishEnemy { id = G3, position = s.g3Start, hp = s.g3Hp },
                    new SkirmishEnemy { id = G4, position = s.g4Start, hp = s.g4Hp },
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
        }

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

        public void Tick(float dt)
        {
            Events.Clear();
            var st = State;
            if (st.phase != SkirmishPhase.Fighting || dt <= 0f) return;
            st.elapsed += dt;

            MoveMech(dt);
            TickEnemy(st.enemies[G3], dt);
            TickEnemy(st.enemies[G4], dt);
            MechGunFire(dt);
            CannonFire(dt);
            MissileLock(dt);
            TickShells(dt);
            Resolve();
        }

        void MoveMech(float dt)
        {
            var st = State; var s = Settings;
            var move = new Vector3(mech.move.x, 0f, mech.move.z);
            if (SkirmishMath.Finite(move))
            {
                var p = st.mech + Vector3.ClampMagnitude(move, 1f) * (s.mechSpeed * dt);
                p.x = Mathf.Clamp(p.x, s.mechArea.x, s.mechArea.z);
                p.z = Mathf.Clamp(p.z, s.mechArea.y, s.mechArea.w);
                st.mech = p;
            }
            st.guarding = mech.guard;
            st.guardDirection = SkirmishMath.Flat(mech.guardDirection);
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
            if (!mech.fire || mechGunCooldown > 0f || !SkirmishMath.Direction(ref aim)) return;
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
                    int target = SkirmishMath.Direction(ref pointer) ? EnemyOnRay(s.gunnerPost, pointer, out _) : -1;
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
                        velocity = (EnemyCenter(st.lockTarget) - s.gunnerPost).normalized * s.missileSpeed,
                        target = st.lockTarget, life = 12f,
                    };
                    st.shells.Add(shell);
                    Emit(SkirmishEventKind.MissileFired, shell.id, s.gunnerPost, EnemyCenter(st.lockTarget));
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
                        var enemy = st.enemies[shell.target];
                        if (!enemy.Alive || shell.life <= 0f)
                        {
                            // 표적이 먼저 쓰러졌으면 미사일은 그대로 소모된다.
                            Emit(SkirmishEventKind.MissileLost, shell.id, shell.position, shell.position);
                            done = true;
                            break;
                        }
                        var center = EnemyCenter(shell.target);
                        float step = s.missileSpeed * dt;
                        if (Vector3.Distance(shell.position, center) <= step + s.enemyRadius)
                        {
                            Emit(SkirmishEventKind.MissileHit, shell.target, shell.position, center);
                            Damage(enemy, s.missileDamage, true);
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
                st.phase = SkirmishPhase.Failed;
                Emit(SkirmishEventKind.Failed, st.furnace <= 0 ? G4 : G3, st.mech, st.mech);
                return;
            }
            if (st.enemies[G3].Alive || st.enemies[G4].Alive) return;
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
