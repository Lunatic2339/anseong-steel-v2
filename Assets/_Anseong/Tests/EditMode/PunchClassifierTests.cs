using System;
using System.Collections.Generic;
using Anseong.Combat;
using Anseong.Pilot;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class PunchClassifierTests
    {
        // 중립 = 보정한 가드 위치(몸 기준). 원점이 아니어도 되는지 보려고 일부러 옮겨 둔다.
        static readonly Vector3 Neutral = new Vector3(0.05f, -0.1f, 0.3f);
        static readonly int[] Rates = { 30, 60, 90, 120 };

        sealed class Run
        {
            public readonly PunchClassifier Classifier;
            public readonly List<PunchResult> Results = new List<PunchResult>();
            public readonly float Hz;
            public double Time;

            public Run(bool left, float hz, PunchClassifierSettings settings = null)
            {
                Classifier = new PunchClassifier(left, settings ?? new PunchClassifierSettings());
                Classifier.SetNeutral(Neutral);
                Hz = hz;
            }

            public void Sample(Vector3 offset, bool intent = true, bool tracked = true)
            {
                PunchResult r;
                if (Classifier.Sample(Neutral + offset, tracked, intent, Time, out r)) Results.Add(r);
                Time += 1.0 / Hz;
            }

            public void Hold(Vector3 offset, float seconds, bool intent = true)
            {
                var n = Mathf.RoundToInt(seconds * Hz);
                for (var i = 0; i < n; i++) Sample(offset, intent);
            }

            /// <summary>중립에서 기다린 뒤 0.4초 동안 path(0→1)를 따라 휘두르고 잠깐 멈춘 다음 중립으로 돌아온다.</summary>
            public void Stroke(Func<float, Vector3> path, bool intent = true)
            {
                Hold(Vector3.zero, 0.5f, intent);
                var n = Mathf.RoundToInt(0.4f * Hz);
                for (var i = 1; i <= n; i++) Sample(path((float)i / n), intent);
                Hold(path(1f), 0.2f, intent);
                for (var i = 1; i <= n; i++) Sample(Vector3.Lerp(path(1f), Vector3.zero, (float)i / n), intent);
                Hold(Vector3.zero, 0.3f, intent);
            }
        }

        static PunchClassifierSettings GuardReturn() => new PunchClassifierSettings { rearmMode = RearmMode.GuardReturn };

        static Vector3 Jab(float u) => new Vector3(0f, 0f, 0.4f * u);

        /// <summary>
        /// 바깥(왼팔이면 −X)으로 outset만큼 준비했다가(앞으로 10cm) 안쪽으로 110° 원호를 그리며 2 × outset 휘어 들어온다.
        /// 10/25 헤드셋 CSV에서 실제 훅은 호/현 1.16~1.33이었다(곧은 대각선은 1.01~1.04). 원호 110°는 호/현 약 1.17.
        /// </summary>
        static Func<float, Vector3> Hook(bool left, float outset = 0.23f)
        {
            var s = left ? 1f : -1f;
            const float windUp = 0.3f, sweep = 110f * Mathf.Deg2Rad;
            var radius = 2f * outset / (1f - Mathf.Cos(sweep));
            return u =>
            {
                if (u < windUp) return new Vector3(-s * outset * u / windUp, 0f, 0.1f * u / windUp);
                var phi = (u - windUp) / (1f - windUp) * sweep;
                return new Vector3(s * (-outset + radius * (1f - Mathf.Cos(phi))), 0f, 0.1f + radius * Mathf.Sin(phi));
            };
        }

        static void AssertSingle(Run run, PunchKind expected, string label)
        {
            Assert.AreEqual(1, run.Results.Count, label + ": 동작 하나만 센다");
            var r = run.Results[0];
            Assert.AreEqual(expected, r.Kind, $"{label}: {r.Reason}, 좌우 {r.Measure.SideRatio:F2}, 직진 {r.Measure.Straightness:F2}, " +
                                              $"바깥 {r.Measure.Outset:F2}, 스윕 {r.Measure.InwardSweep:F2}, 호 {r.Measure.ArcRatio:F3}, 올림 {r.Measure.Rise:F2}");
            Assert.AreEqual(PunchPhase.Ready, run.Classifier.Phase, label + ": 중립으로 돌아오면 다시 준비");
        }

        [Test]
        public void Rearm_WhenGuardReturnsOffset_StillCountsNextJabs()
        {
            // 10/25 헤드셋: 친 뒤 가드로 돌아오는 자리가 보정한 가드에서 조금 어긋나면 다음 펀치를 못 셌다.
            var run = new Run(true, 90f);
            var home = new Vector3(0.15f, -0.12f, -0.05f); // 옆 15cm, 아래 12cm, 몸 쪽 5cm
            run.Hold(Vector3.zero, 0.5f);
            for (var k = 0; k < 3; k++)
            {
                var n = Mathf.RoundToInt(0.4f * run.Hz);
                var start = k == 0 ? Vector3.zero : home;
                for (var i = 1; i <= n; i++) run.Sample(start + Jab((float)i / n));
                run.Hold(start + Jab(1f), 0.2f);
                for (var i = 1; i <= n; i++) run.Sample(Vector3.Lerp(start + Jab(1f), home, (float)i / n));
                run.Hold(home, 0.4f);
            }
            Assert.AreEqual(3, run.Results.Count);
            foreach (var r in run.Results) Assert.AreEqual(PunchKind.Jab, r.Kind, r.Reason.ToString());
        }

        [Test]
        public void GuardReturnMode_Rearm_NeedsHandBackFromForward()
        {
            var run = new Run(true, 90f, GuardReturn());
            run.Stroke(Jab);
            Assert.AreEqual(PunchPhase.Ready, run.Classifier.Phase);
            // 한 번 더 뻗은 채(가드보다 20cm 앞) 멈춰 있으면 다시 준비되지 않는다.
            var n = Mathf.RoundToInt(0.4f * run.Hz);
            for (var i = 1; i <= n; i++) run.Sample(Jab((float)i / n));
            run.Hold(Jab(1f), 0.2f);
            for (var i = 1; i <= n; i++) run.Sample(Vector3.Lerp(Jab(1f), new Vector3(0f, 0f, 0.2f), (float)i / n));
            run.Hold(new Vector3(0f, 0f, 0.2f), 0.5f);
            Assert.AreNotEqual(PunchPhase.Ready, run.Classifier.Phase);
        }

        [Test]
        public void Retract_HalfPullBack_CountsNextJab()
        {
            // 가드까지 안 돌아와도 반쯤(20cm) 거둬들였다 다시 뻗으면 센다(연타).
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            var n = 36;
            for (var k = 0; k < 3; k++)
            {
                var from = k == 0 ? Vector3.zero : Jab(0.5f);
                var m = k == 0 ? n : n / 2; // 다시 뻗기도 초속 1m
                for (var i = 1; i <= m; i++) run.Sample(Vector3.Lerp(from, Jab(1f), (float)i / m));
                run.Hold(Jab(1f), 0.2f);
                for (var i = 1; i <= n; i++) run.Sample(Vector3.Lerp(Jab(1f), Jab(0.5f), (float)i / n));
            }
            Assert.AreEqual(3, run.Results.Count);
            foreach (var r in run.Results) Assert.AreEqual(PunchKind.Jab, r.Kind, r.Reason.ToString());
        }

        [Test]
        public void Retract_SmallWobble_DoesNotRearm()
        {
            // 뻗은 채 5cm만 흔들리면 다시 준비되지 않는다(한 번 친 걸 두 번 세지 않음).
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            var n = 36;
            for (var i = 1; i <= n; i++) run.Sample(Jab((float)i / n));
            run.Hold(Jab(1f), 0.2f);
            Assert.AreEqual(1, run.Results.Count);
            for (var k = 0; k < 4; k++)
            {
                for (var i = 1; i <= 9; i++) run.Sample(Vector3.Lerp(Jab(1f), Jab(0.875f), i / 9f));
                for (var i = 1; i <= 9; i++) run.Sample(Vector3.Lerp(Jab(0.875f), Jab(1f), i / 9f));
            }
            Assert.AreEqual(1, run.Results.Count);
            Assert.AreEqual(PunchPhase.Recovering, run.Classifier.Phase);
        }

        [Test]
        public void Retract_RestingWhereverHandIs_Rearms()
        {
            // 거둬들이지 않아도 그 자리에서 잠깐(0.4초) 멈추면 다음 준비.
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            var n = 36;
            for (var i = 1; i <= n; i++) run.Sample(Jab((float)i / n));
            run.Hold(Jab(1f), 0.3f);
            Assert.AreEqual(PunchPhase.Recovering, run.Classifier.Phase);
            run.Hold(Jab(1f), 0.3f);
            Assert.AreEqual(PunchPhase.Ready, run.Classifier.Phase);
        }

        [Test]
        public void Retract_MinInterval_BlocksInstantRearm()
        {
            var settings = new PunchClassifierSettings { minStrikeInterval = 1.0f };
            var run = new Run(true, 90, settings);
            run.Hold(Vector3.zero, 0.5f);
            var n = 36;
            for (var i = 1; i <= n; i++) run.Sample(Jab((float)i / n));
            run.Hold(Jab(1f), 0.2f);
            for (var i = 1; i <= 18; i++) run.Sample(Vector3.Lerp(Jab(1f), Jab(0.5f), i / 18f));
            Assert.AreEqual(PunchPhase.Recovering, run.Classifier.Phase, "최소 간격 전에는 거둬들여도 아직");
            run.Hold(Jab(0.5f), 0.8f);
            Assert.AreEqual(PunchPhase.Ready, run.Classifier.Phase);
        }

        [Test]
        public void StraightJab_IsJab_AtAllRates_BothArms()
        {
            foreach (var hz in Rates)
            foreach (var left in new[] { true, false })
            {
                var run = new Run(left, hz);
                run.Stroke(Jab);
                AssertSingle(run, PunchKind.Jab, $"잽 {hz}Hz {(left ? "L" : "R")}");
                Assert.IsTrue(run.Results[0].IsAttack);
                Assert.AreEqual(1, run.Results[0].StrikeId);
            }
        }

        [Test]
        public void OutsideInHook_IsHook_AtAllRates_BothArms()
        {
            foreach (var hz in Rates)
            foreach (var left in new[] { true, false })
            {
                var run = new Run(left, hz);
                run.Stroke(Hook(left));
                AssertSingle(run, PunchKind.Hook, $"훅 {hz}Hz {(left ? "L" : "R")}");
            }
        }

        [Test]
        public void HookWithWrongDirection_IsNotHook()
        {
            // 오른팔 궤적을 왼팔로: 안쪽 → 바깥은 훅이 아니다.
            var run = new Run(true, 90);
            run.Stroke(Hook(false));
            Assert.AreEqual(1, run.Results.Count);
            Assert.AreNotEqual(PunchKind.Hook, run.Results[0].Kind);
        }

        [Test]
        public void ShortHook_IsNotHook()
        {
            foreach (var left in new[] { true, false })
            {
                var run = new Run(left, 90);
                run.Stroke(Hook(left, 0.06f));
                // 안쪽 스윕이 짧으면(12cm) 훅이 아니다. 거의 곧게 나가므로 잽으로 잡힐 수는 있다(규칙상 맞음).
                Assert.AreEqual(1, run.Results.Count);
                Assert.AreNotEqual(PunchKind.Hook, run.Results[0].Kind, "짧은 훅 " + (left ? "L" : "R"));
                Assert.Less(run.Results[0].Measure.Outset, 0.10f);
            }
        }

        [Test]
        public void WideGuardHook_WithoutWindUp_IsHook_BothArms()
        {
            // 가드를 넓게 잡으면 바깥으로 더 빼지 않고 가드에서 바로 안쪽으로 휘두른다.
            foreach (var left in new[] { true, false })
            {
                var s = left ? 1f : -1f;
                var run = new Run(left, 90);
                // 안쪽으로 30cm 휘어 들어오는 110° 원호(바깥 준비 없음).
                var radius = 0.3f / (1f - Mathf.Cos(110f * Mathf.Deg2Rad));
                run.Stroke(u => new Vector3(s * radius * (1f - Mathf.Cos(u * 110f * Mathf.Deg2Rad)), 0f, radius * Mathf.Sin(u * 110f * Mathf.Deg2Rad)));
                AssertSingle(run, PunchKind.Hook, "넓은 가드 훅 " + (left ? "L" : "R"));
            }
        }

        [Test]
        public void BigLoopingHook_WithRiseAndDip_IsHook_NotVerticalMotion()
        {
            // 1차 헤드셋 로그(오른손 #14): 옆으로 크게 돌며 위아래로 25cm 넘게 움직인 훅이 '위아래 움직임'으로 버려졌다.
            Func<float, Vector3> loop = u => Hook(false, 0.25f)(u) + new Vector3(0f, 0.26f * Mathf.Sin(u * Mathf.PI), -0.07f * Mathf.Sin(u * Mathf.PI / 2f));
            var run = new Run(false, 90);
            run.Stroke(loop);
            AssertSingle(run, PunchKind.Hook, "크게 도는 훅");

            var old = new PunchClassifier(false, new PunchClassifierSettings { verticalAgainstWidestSpan = false, maxVerticalRatio = 0.5f });
            old.SetNeutral(Neutral);
            var results = new List<PunchResult>();
            double t = 0;
            Action<Vector3> sample = o => { PunchResult r; if (old.Sample(Neutral + o, true, true, t, out r)) results.Add(r); t += 1.0 / 90; };
            for (var i = 0; i < 45; i++) sample(Vector3.zero);
            for (var i = 1; i <= 36; i++) sample(loop(i / 36f));
            for (var i = 0; i < 18; i++) sample(loop(1f));
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(PunchReason.VerticalMotion, results[0].Reason, "끄면 이전처럼 위아래 움직임");
        }

        [Test]
        public void StrikeHold_WaitsForRobotFist()
        {
            var hold = new StrikeHold();
            hold.StartStroke(Vector3.zero);
            hold.Track(new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 0.8f));
            hold.Begin(new PunchResult { Kind = PunchKind.Jab, StrikeId = 3 }, 0.0);
            hold.Track(new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 0.8f));
            Assert.IsFalse(hold.Step(new Vector3(0f, 0f, 0.5f), 0.3f, 0.1, 0.08f, 0.8f), "로봇 주먹이 아직 뻗는 중");
            hold.Track(new Vector3(0f, 0f, 0.75f), new Vector3(0f, 0f, 0.8f));
            Assert.IsTrue(hold.Step(new Vector3(0f, 0f, 0.75f), 0.05f, 0.2, 0.08f, 0.8f), "따라잡으면 낸다");
            var result = hold.Finish(0.6f);
            Assert.AreEqual(PunchKind.Jab, result.Kind);
            Assert.AreEqual(3, result.StrikeId);
            Assert.IsFalse(hold.Pending);
        }

        [Test]
        public void StrikeHold_QuickJabRobotBarelyMoved_IsNotAPunch()
        {
            // 10/25 헤드셋: 묵직함 0.6에서 빨리 치고 빼면 로봇 팔은 거의 안 움직였는데 잽·어퍼컷으로 셌다.
            var hold = new StrikeHold();
            hold.StartStroke(Vector3.zero);
            hold.Track(new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, 0.8f));
            hold.Begin(new PunchResult { Kind = PunchKind.Jab }, 0.0);
            hold.Track(new Vector3(0f, 0f, 0.2f), Vector3.zero);
            Assert.IsFalse(hold.Step(new Vector3(0f, 0f, 0.2f), 0.2f, 0.05, 0.08f, 0.8f));
            hold.Track(new Vector3(0f, 0f, 0.15f), Vector3.zero);
            Assert.IsTrue(hold.Step(new Vector3(0f, 0f, 0.15f), 0.15f, 0.1, 0.08f, 0.8f), "로봇 주먹이 돌아오기 시작");
            Assert.AreEqual(0.25f, hold.Reach, 1e-4f);
            var result = hold.Finish(0.6f);
            Assert.AreEqual(PunchKind.None, result.Kind);
            Assert.AreEqual(PunchReason.RobotLagged, result.Reason);
        }

        [Test]
        public void StrikeHold_ReleasesAtMaxSeconds()
        {
            var hold = new StrikeHold();
            hold.StartStroke(Vector3.zero);
            hold.Begin(new PunchResult { Kind = PunchKind.Hook }, 1.0);
            hold.Track(new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, 0.12f));
            Assert.IsFalse(hold.Step(new Vector3(0f, 0f, 0.1f), 0.5f, 1.5, 0.08f, 0.8f));
            hold.Track(new Vector3(0f, 0f, 0.11f), new Vector3(0f, 0f, 0.12f));
            Assert.IsTrue(hold.Step(new Vector3(0f, 0f, 0.11f), 0.5f, 1.85, 0.08f, 0.8f), "최대 시간");
            Assert.AreEqual(PunchKind.Hook, hold.Finish(0.6f).Kind, "로봇이 거의 따라왔으면 펀치");
        }

        [Test]
        public void SmallerHook_IsHook()
        {
            // 10/25 헤드셋: 훅이 빡빡하다. 바깥 준비 없이 안쪽으로 19cm만 휘어 들어와도 훅.
            foreach (var left in new[] { true, false })
            {
                var run = new Run(left, 90);
                run.Stroke(Hook(left, 0.095f));
                AssertSingle(run, PunchKind.Hook, "작은 훅 " + (left ? "L" : "R"));
            }
        }

        [Test]
        public void StraightInwardDiagonal_IsJab_NotHook()
        {
            // 10/25 헤드셋: 대각선으로 곧게 친 펀치가 훅으로 잡혔다. 안쪽으로 크게 가도 곧으면 훅이 아니라 잽이다.
            foreach (var left in new[] { true, false })
            {
                var s = left ? 1f : -1f;
                var run = new Run(left, 90);
                // 살짝 휜(4cm) 대각선: 안쪽 36cm, 앞 40cm(CSV 대각선 잽 좌우/전진 0.85~0.96). 손은 완전히 곧게 못 친다.
                run.Stroke(u => new Vector3(s * (0.36f * u - 0.04f * Mathf.Sin(u * Mathf.PI)), 0f, 0.4f * u));
                Assert.AreEqual(1, run.Results.Count);
                var r = run.Results[0];
                Assert.AreEqual(PunchKind.Jab, r.Kind, $"대각선 {(left ? "L" : "R")}: 꺾임 {r.Measure.TurnDegrees:F0}°, 호 {r.Measure.ArcRatio:F3}");
            }
        }

        [Test]
        public void DiagonalSwipe_IsAmbiguous()
        {
            var run = new Run(true, 90);
            run.Stroke(u => new Vector3(0.4f * u, 0f, 0.3f * u));
            AssertSingle(run, PunchKind.None, "비스듬한 잽");
            Assert.AreEqual(PunchReason.Ambiguous, run.Results[0].Reason);
        }

        [Test]
        public void LowBodyJab_IsJab()
        {
            var run = new Run(false, 90);
            run.Stroke(u => new Vector3(0f, -0.15f * u, 0.4f * u));
            AssertSingle(run, PunchKind.Jab, "낮은 잽");
        }

        [Test]
        public void Uppercut_IsUppercut_BothArms()
        {
            foreach (var hz in Rates)
            foreach (var left in new[] { true, false })
            {
                var run = new Run(left, hz);
                run.Stroke(u => new Vector3(0f, 0.3f * Mathf.Sin(u * Mathf.PI / 2f), 0.12f * u));
                AssertSingle(run, PunchKind.Uppercut, $"어퍼컷 {hz}Hz {(left ? "L" : "R")}");
            }
        }

        [Test]
        public void SlowGuardRaise_IsNotAStrike()
        {
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            for (var i = 1; i <= 90; i++) run.Sample(new Vector3(0f, 0.25f * i / 90f, 0f));
            run.Hold(new Vector3(0f, 0.25f, 0f), 0.3f);
            Assert.AreEqual(0, run.Results.Count, "1초에 걸쳐 가드를 올리면 공격 시작 속도가 안 된다");
        }

        [Test]
        public void GuardReturnMode_SecondStrikeWithoutReload_IsNotCounted_UntilNeutral()
        {
            var run = new Run(true, 90, GuardReturn());
            run.Hold(Vector3.zero, 0.5f);
            var n = 36;
            for (var i = 1; i <= n; i++) run.Sample(Jab((float)i / n));
            run.Hold(Jab(1f), 0.2f);
            Assert.AreEqual(1, run.Results.Count);
            // 중립까지 안 돌아오고 반쯤만 당겼다 다시 뻗는다.
            for (var i = 1; i <= n; i++) run.Sample(Vector3.Lerp(Jab(1f), Jab(0.5f), (float)i / n));
            for (var i = 1; i <= n; i++) run.Sample(Vector3.Lerp(Jab(0.5f), Jab(1f), (float)i / n));
            run.Hold(Jab(1f), 0.2f);
            Assert.AreEqual(1, run.Results.Count, "재장전 전 두 번째 동작은 세지 않는다");
            Assert.AreEqual(PunchPhase.Recovering, run.Classifier.Phase);

            // 중립 반경에 들어갔지만 0.15초가 안 되면 아직 아니다.
            for (var i = 1; i <= n; i++) run.Sample(Vector3.Lerp(Jab(1f), Vector3.zero, (float)i / n));
            run.Hold(Vector3.zero, 0.08f);
            Assert.AreEqual(PunchPhase.Recovering, run.Classifier.Phase);
            run.Hold(Vector3.zero, 0.15f);
            Assert.AreEqual(PunchPhase.Ready, run.Classifier.Phase);

            run.Stroke(Jab);
            Assert.AreEqual(2, run.Results.Count);
            Assert.AreEqual(2, run.Results[1].StrikeId, "동작마다 번호가 하나씩 는다");
        }

        [Test]
        public void NoGrip_ClassifiesButIsNotAttack()
        {
            var run = new Run(true, 90);
            run.Stroke(Jab, intent: false);
            AssertSingle(run, PunchKind.Jab, "그립 없는 잽");
            Assert.IsFalse(run.Results[0].GripHeld);
            Assert.IsFalse(run.Results[0].IsAttack);
        }

        [Test]
        public void GripReleasedMidStroke_IsNotAttack()
        {
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            var n = 36;
            for (var i = 1; i <= n; i++) run.Sample(Jab((float)i / n), intent: i < n / 2);
            run.Hold(Jab(1f), 0.2f, intent: false);
            Assert.AreEqual(1, run.Results.Count);
            Assert.AreEqual(PunchKind.Jab, run.Results[0].Kind);
            Assert.IsFalse(run.Results[0].IsAttack);
        }

        [Test]
        public void TrackingLostMidStroke_EndsAsNotAttack_AndNeedsNeutral()
        {
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            for (var i = 1; i <= 20; i++) run.Sample(Jab(i / 36f));
            run.Sample(Jab(20 / 36f), tracked: false);
            Assert.AreEqual(1, run.Results.Count, "끊긴 동작도 기록은 남긴다");
            Assert.IsFalse(run.Results[0].Tracked);
            Assert.IsFalse(run.Results[0].IsAttack);
            Assert.AreEqual(PunchPhase.TrackingLost, run.Classifier.Phase);

            for (var i = 0; i < 10; i++) run.Sample(Jab(1f), tracked: false);
            Assert.AreEqual(1, run.Results.Count, "끊겨 있는 동안은 아무것도 세지 않는다");
            run.Sample(Jab(1f));
            Assert.AreEqual(PunchPhase.WaitingForNeutral, run.Classifier.Phase, "돌아와도 중립부터");
            run.Stroke(Jab);
            Assert.AreEqual(2, run.Results.Count);
            Assert.IsTrue(run.Results[1].IsAttack);
        }

        [Test]
        public void SampleGap_DiscardsStroke_WithoutResult()
        {
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            for (var i = 1; i <= 10; i++) run.Sample(Jab(i / 36f));
            run.Time += 0.3;
            run.Sample(Jab(1f));
            Assert.AreEqual(0, run.Results.Count, "렉으로 끊긴 동작은 버린다");
            Assert.AreEqual(PunchPhase.WaitingForNeutral, run.Classifier.Phase);
        }

        [Test]
        public void NaNPosition_IsTreatedAsTrackingLoss()
        {
            var run = new Run(true, 90);
            run.Hold(Vector3.zero, 0.5f);
            run.Sample(new Vector3(float.NaN, 0f, 0f));
            Assert.AreEqual(PunchPhase.TrackingLost, run.Classifier.Phase);
        }

        [Test]
        public void StrikeLogRow_MatchesHeaderColumns()
        {
            var run = new Run(true, 90);
            run.Stroke(Jab);
            var row = StrikeLog.FormatRow(PilotSide.Left, run.Results[0]);
            Assert.AreEqual(StrikeLog.Header.Split(',').Length, row.Split(',').Length);
            StringAssert.Contains(",Left,Jab,Jab,", row);
            StringAssert.EndsWith(",1,", row, "strike_id 1, 명중 칸은 비움");
            Assert.AreEqual("strikes_20261017_134500.csv",
                System.IO.Path.GetFileName(StrikeLog.DefaultPath("root", new DateTime(2026, 10, 17, 13, 45, 0))));
        }
    }
}
