using System;
using System.Linq;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>Z2 교전 규칙. 문서의 시험 가지(미사일 선택, 요격, 가드, 고로 피격, 취소, 재시작)를 그대로 돌린다.</summary>
    public class SkirmishSessionTests
    {
        const float Dt = 0.02f;
        const int G3 = SkirmishSession.G3, G4 = SkirmishSession.G4;

        static SkirmishSession Started(SkirmishSettings settings = null)
        {
            var s = new SkirmishSession(settings);
            s.SetReady(true, true);
            return s;
        }

        /// <summary>seconds 동안 돌리면서 매 틱 입력을 넣고, 그동안 나온 사건 종류를 모은다.</summary>
        static SkirmishEventKind[] Run(SkirmishSession s, float seconds, Action<SkirmishSession> input = null, Func<SkirmishSession, bool> until = null)
        {
            var seen = new System.Collections.Generic.List<SkirmishEventKind>();
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                input?.Invoke(s);
                s.Tick(Dt);
                seen.AddRange(s.Events.Select(e => e.Kind));
                if (until != null && until(s)) break;
            }
            return seen.ToArray();
        }

        static Vector3 GunnerAimAt(SkirmishSession s, Vector3 point) => (point - s.Settings.gunnerPost).normalized;

        static void GuardTowardG3(SkirmishSession s) =>
            s.SetMech(new MechInput { guard = true, guardDirection = s.State.enemies[G3].position - s.State.mech });

        static void ShootEnemy(SkirmishSession s, int id) =>
            s.SetGunner(new GunnerInput { fire = true, aim = GunnerAimAt(s, s.EnemyCenter(id)) });

        static void HoldLock(SkirmishSession s, int id) =>
            s.SetGunner(new GunnerInput { lockHeld = true, pointer = GunnerAimAt(s, s.EnemyCenter(id)) });

        [Test]
        public void NothingHappensUntilBothAreReady()
        {
            var s = new SkirmishSession();
            s.SetReady(true, false);
            Run(s, 5f);
            Assert.AreEqual(SkirmishPhase.Waiting, s.State.phase);
            Assert.AreEqual(s.Settings.g3Start, s.State.enemies[G3].position);
            s.SetReady(true, true);
            Assert.AreEqual(SkirmishPhase.Fighting, s.State.phase);
        }

        [Test]
        public void IgnoredG4ShellsTheFurnaceUntilTheZoneFails()
        {
            var s = Started();
            var seen = Run(s, 80f, GuardTowardG3, x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Failed, s.State.phase);
            Assert.AreEqual(0, s.State.furnace);
            Assert.AreEqual(s.Settings.furnaceHits, seen.Count(k => k == SkirmishEventKind.FurnaceHit));
            Assert.AreEqual(s.Settings.mechHull, s.State.hull, "가드로 큰 탄을 전부 막았으므로 메카는 멀쩡해야 한다");
        }

        [Test]
        public void CannonAloneCanStopG4BeforeItsFirstShot()
        {
            var s = Started();
            var seen = Run(s, 60f, x => { GuardTowardG3(x); ShootEnemy(x, G4); }, x => !x.State.enemies[G4].Alive);
            Assert.IsFalse(s.State.enemies[G4].Alive);
            Assert.AreEqual(s.Settings.furnaceHits, s.State.furnace);
            Assert.AreEqual(s.Settings.missiles, s.State.missiles, "미사일 없이도 가능해야 한다");
            Assert.IsFalse(seen.Contains(SkirmishEventKind.FurnaceHit));
        }

        [Test]
        public void UnguardedBigShellDamagesTheMech()
        {
            var s = Started();
            var seen = Run(s, 20f, null, x => x.State.hull < x.Settings.mechHull);
            Assert.Contains(SkirmishEventKind.MechHit, seen);
            Assert.AreEqual(s.Settings.mechHull - s.Settings.bigShellDamage, s.State.hull);
        }

        [Test]
        public void GuardFacingTheShellBlocksItButFacingAwayDoesNot()
        {
            var facing = Started();
            var seen = Run(facing, 20f, GuardTowardG3, x => x.Events.Any(e => e.Kind == SkirmishEventKind.ShellBlocked));
            Assert.Contains(SkirmishEventKind.ShellBlocked, seen);
            Assert.AreEqual(facing.Settings.mechHull, facing.State.hull);

            var away = Started();
            seen = Run(away, 20f, x => x.SetMech(new MechInput { guard = true, guardDirection = Vector3.back }),
                x => x.State.hull < x.Settings.mechHull);
            Assert.Contains(SkirmishEventKind.MechHit, seen);
        }

        [Test]
        public void GunnerCanInterceptTheBigShell()
        {
            var s = Started();
            var seen = Run(s, 20f, x =>
            {
                var shell = x.State.shells.FirstOrDefault(sh => sh.kind == ShellKind.BigShell);
                x.SetGunner(shell == null ? default : new GunnerInput { fire = true, aim = GunnerAimAt(x, shell.position) });
            }, x => x.Events.Any(e => e.Kind == SkirmishEventKind.ShellIntercepted));
            Assert.Contains(SkirmishEventKind.ShellIntercepted, seen);
            Run(s, 4f);
            Assert.AreEqual(s.Settings.mechHull, s.State.hull, "요격했으면 가드 없이도 피해가 없어야 한다");
        }

        [Test]
        public void MissileNeedsAFullLockAndIsSpentOnlyWhenFired()
        {
            var s = Started();
            Run(s, 0.5f, x => HoldLock(x, G4));
            s.SetGunner(default);
            Run(s, 0.1f);
            Assert.AreEqual(s.Settings.missiles, s.State.missiles, "잠금이 덜 됐는데 놓으면 쓰지 않는다");

            Run(s, 1.1f, x => HoldLock(x, G4));
            Assert.AreEqual(1f, s.State.lockProgress);
            s.SetGunner(default);
            var seen = Run(s, 6f, null, x => x.Events.Any(e => e.Kind == SkirmishEventKind.MissileHit));
            Assert.AreEqual(s.Settings.missiles - 1, s.State.missiles);
            Assert.Contains(SkirmishEventKind.MissileHit, seen);
            Assert.AreEqual(s.Settings.g4Hp - s.Settings.missileDamage, s.State.enemies[G4].hp);
            Assert.AreEqual(EnemyStep.Stagger, s.State.enemies[G4].step);
        }

        [Test]
        public void CancelledLockDoesNotSpendAMissile()
        {
            var s = Started();
            Run(s, 1.1f, x => HoldLock(x, G4));
            s.SetGunner(new GunnerInput { lockHeld = true, cancel = true, pointer = GunnerAimAt(s, s.EnemyCenter(G4)) });
            Run(s, 0.1f);
            s.SetGunner(default);
            var seen = Run(s, 1f);
            Assert.AreEqual(s.Settings.missiles, s.State.missiles);
            Assert.IsFalse(seen.Contains(SkirmishEventKind.MissileFired));
        }

        [Test]
        public void LockResetsWhenThePointerLeavesTheTarget()
        {
            var s = Started();
            Run(s, 0.6f, x => HoldLock(x, G4));
            Assert.Greater(s.State.lockProgress, 0.4f);
            s.SetGunner(new GunnerInput { lockHeld = true, pointer = Vector3.up });
            Run(s, 0.1f);
            Assert.AreEqual(-1, s.State.lockTarget);
            Assert.AreEqual(0f, s.State.lockProgress);
        }

        [Test]
        public void SecondMissileStillHurtsButCannotStunLockTheEnemy()
        {
            var s = Started(new SkirmishSettings { g4Hp = 300f });
            var seen = new System.Collections.Generic.List<SkirmishEventKind>();
            for (int shot = 0; shot < 2; shot++)
            {
                seen.AddRange(Run(s, 1.1f, x => HoldLock(x, G4)));
                s.SetGunner(default);
                seen.AddRange(Run(s, 0.1f));
            }
            seen.AddRange(Run(s, 6f));
            Assert.AreEqual(0, s.State.missiles);
            Assert.AreEqual(2, seen.Count(k => k == SkirmishEventKind.MissileHit));
            Assert.AreEqual(1, seen.Count(k => k == SkirmishEventKind.EnemyStaggered));
            Assert.AreEqual(300f - 2 * s.Settings.missileDamage, s.State.enemies[G4].hp);
        }

        [Test]
        public void MissileSpentOnG3IsNotALossAndZoneClearsWhenBothAreDown()
        {
            var s = Started();
            Run(s, 1.1f, x => { GuardTowardG3(x); HoldLock(x, G3); });
            s.SetGunner(default);
            Run(s, 0.1f, GuardTowardG3);
            Assert.AreEqual(s.Settings.missiles - 1, s.State.missiles);
            var seen = Run(s, 60f, x =>
            {
                GuardTowardG3(x);
                ShootEnemy(x, x.State.enemies[G4].Alive ? G4 : G3);
            }, x => x.State.phase != SkirmishPhase.Fighting);
            Assert.AreEqual(SkirmishPhase.Cleared, s.State.phase);
            Assert.Contains(SkirmishEventKind.Cleared, seen);
            Assert.AreEqual(s.Settings.furnaceHits, s.State.furnace);
        }

        [Test]
        public void MechAloneCannotInterruptG3ButBothTogetherCan()
        {
            var alone = Started();
            var seen = Run(alone, 9f, x => x.SetMech(new MechInput
            {
                guard = true, guardDirection = x.State.enemies[G3].position - x.State.mech,
                fire = true, aim = (x.EnemyCenter(G3) - x.MechGun).normalized,
            }));
            Assert.Contains(SkirmishEventKind.ShellFired, seen, "메카 혼자서는 큰 탄 발사를 못 끊는다");

            var together = Started();
            seen = Run(together, 7f, x =>
            {
                x.SetMech(new MechInput { fire = true, aim = (x.EnemyCenter(G3) - x.MechGun).normalized });
                ShootEnemy(x, G3);
            }, x => !x.State.enemies[G3].Alive || x.Events.Any(e => e.Kind == SkirmishEventKind.EnemyStaggered));
            Assert.IsFalse(seen.Contains(SkirmishEventKind.ShellFired));
        }

        [Test]
        public void CannonOverheatsAndRecovers()
        {
            var s = Started(new SkirmishSettings { cannonHeatPerShot = 0.2f });
            Run(s, 2f, x => x.SetGunner(new GunnerInput { fire = true, aim = Vector3.up }), x => x.State.cannonOverheated);
            Assert.IsTrue(s.State.cannonOverheated);
            var seen = Run(s, 1f, x => x.SetGunner(new GunnerInput { fire = true, aim = Vector3.up }));
            Assert.IsFalse(seen.Contains(SkirmishEventKind.CannonShot), "과열 중에는 못 쏜다");
            s.SetGunner(default);
            Run(s, 4f);
            Assert.IsFalse(s.State.cannonOverheated);
        }

        [Test]
        public void ResetRestoresEverythingAndRequiresReadyAgain()
        {
            var s = Started();
            Run(s, 1.1f, x => HoldLock(x, G4));
            s.SetGunner(default);
            Run(s, 9f);
            Assert.Less(s.State.hull, s.Settings.mechHull);
            Assert.Less(s.State.missiles, s.Settings.missiles);

            s.Reset();
            Assert.AreEqual(2, s.State.epoch);
            Assert.AreEqual(SkirmishPhase.Waiting, s.State.phase);
            Assert.AreEqual(s.Settings.mechHull, s.State.hull);
            Assert.AreEqual(s.Settings.missiles, s.State.missiles);
            Assert.AreEqual(s.Settings.furnaceHits, s.State.furnace);
            Assert.AreEqual(0, s.State.shells.Count);
            Assert.AreEqual(s.Settings.g4Start, s.State.enemies[G4].position);
            Run(s, 3f);
            Assert.AreEqual(s.Settings.g3Start, s.State.enemies[G3].position, "준비 전에는 적이 움직이지 않는다");
        }

        [Test]
        public void SameInputsGiveTheSameResult()
        {
            string Play()
            {
                var s = Started();
                Run(s, 30f, x => { GuardTowardG3(x); ShootEnemy(x, G4); });
                return JsonUtility.ToJson(s.State);
            }
            Assert.AreEqual(Play(), Play());
        }

        [Test]
        public void RayToSegmentMeasuresDistanceAndIgnoresWhatIsBehind()
        {
            float d = SkirmishMath.RayToSegment(Vector3.zero, Vector3.forward, new Vector3(3, -5, 10), new Vector3(3, 5, 10), out float along);
            Assert.AreEqual(3f, d, 1e-4f);
            Assert.AreEqual(10f, along, 1e-4f);
            d = SkirmishMath.RayToSegment(Vector3.zero, Vector3.forward, new Vector3(0, 0, -10), new Vector3(0, 0, -10), out along);
            Assert.AreEqual(10f, d, 1e-4f);
            Assert.AreEqual(0f, along);
        }
    }
}
