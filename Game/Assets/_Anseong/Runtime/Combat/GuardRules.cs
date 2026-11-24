using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    /// <summary>가드가 아닌 이유(가드면 Guarding).</summary>
    public enum GuardReason { Guarding, NotCalibrated, Untracked, OutOfZone, BehindFace, ForearmTilted }

    /// <summary>판정에 쓸 전완 방향. Human = 사람 컨트롤러(보정한 펀치 자세 기준), Robot = 화면의 로봇 전완(IK 결과), Either = 둘 중 하나.</summary>
    public enum ForearmSource { Human, Robot, Either }

    [Serializable]
    public class GuardSettings
    {
        [Tooltip("보정한 가드 위치에서 몸 기준 좌우·위아래·앞뒤로 이만큼(m) 안이면 가드 영역(01: ±25cm). " +
                 "선우 10/17: 가드를 일부러 넓게 잡으므로 기준은 각자 보정한 가드 위치.")]
        public Vector3 zoneHalfSize = new Vector3(0.25f, 0.25f, 0.25f);

        [Tooltip("주먹이 얼굴(머리)보다 몸 정면으로 이만큼(m) 앞이어야 한다.")]
        public float minFrontOfFace = 0.05f;

        [Tooltip("전완이 수직에서 이 각도(도) 안이면 가드(01: 40°).")]
        [Range(5f, 90f)]
        public float maxForearmTiltDegrees = 40f;

        [Tooltip("판정에 쓸 전완. 둘 다 CSV에 남으니 비교 뒤 고른다.")]
        public ForearmSource forearmSource = ForearmSource.Human;

        [Tooltip("가드 중일 때 풀리는 기준을 이만큼(m) 넓힌다(경계에서 표시등이 깜빡이지 않게).")]
        [Min(0f)]
        public float releaseMargin = 0.03f;

        [Tooltip("가드 중일 때 각도 기준을 이만큼(도) 넓힌다.")]
        [Min(0f)]
        public float releaseDegrees = 5f;
    }

    /// <summary>팔 하나의 한 프레임 입력. 좌표는 같은 공간(트래킹 공간)이면 된다. 방향이 0이면 그 전완은 모름.</summary>
    public struct GuardInput
    {
        public bool Calibrated;
        public bool Tracked;
        public Vector3 Hand;
        public Vector3 Guard;
        public Vector3 Head;
        public Quaternion Body;
        /// <summary>좌우 영역을 이만큼(m) 더 넓힌다(M4-03 방패: 왼팔이 얼굴 앞 왼쪽·오른쪽으로 옮겨 막음).</summary>
        public float ExtraHalfWidth;
        public Vector3 HumanForearm;
        public Vector3 RobotForearm;
        public bool WasGuarding;
    }

    public struct GuardResult
    {
        public bool IsGuarding;
        public GuardReason Reason;
        /// <summary>손 − 보정 가드 위치, 몸 기준(x 좌우, y 위아래, z 앞뒤, m).</summary>
        public Vector3 Offset;
        /// <summary>손이 얼굴보다 앞인 거리(m, 몸 정면).</summary>
        public float FrontOfFace;
        /// <summary>손이 얼굴 가운데에서 옆으로 간 거리(m, 몸 기준, + = 오른쪽). 방패가 어느 쪽을 막는지.</summary>
        public float SideOfFace;
        /// <summary>수직에서 기운 각도(도). 모르면 NaN.</summary>
        public float HumanTilt, RobotTilt;
    }

    /// <summary>
    /// 가드 판정(M1-06, 01 "가드와 회피"). 사람 손 기준: 보정 가드 위치 ±영역 안 + 얼굴 앞 + 전완이 수직 근처 + 추적 유효.
    /// 기존 레포의 "가드보다 15cm 위"만 보던 판정(피할 버그 6)은 쓰지 않는다. 순수 클래스.
    /// </summary>
    public static class GuardRules
    {
        /// <summary>사람 전완 방향 추정: 보정한 펀치(도달) 자세에서 전완 = 몸 정면이므로, 그때부터 돌아간 만큼 몸 정면을 돌린다.
        /// 컨트롤러 축 방향을 가정하지 않는다.</summary>
        public static Vector3 HumanForearm(Quaternion controller, Quaternion reachRotation, Quaternion body)
        {
            return (controller * Quaternion.Inverse(reachRotation)) * (body * Vector3.forward);
        }

        /// <summary>수직(위)에서 기운 각도. 방향을 모르면 NaN.</summary>
        public static float Tilt(Vector3 forearm)
        {
            return forearm.sqrMagnitude < 1e-8f ? float.NaN : Vector3.Angle(Vector3.up, forearm);
        }

        public static GuardResult Evaluate(GuardInput input, GuardSettings settings)
        {
            settings = settings ?? new GuardSettings();
            var result = new GuardResult
            {
                HumanTilt = Tilt(input.HumanForearm),
                RobotTilt = Tilt(input.RobotForearm),
                Reason = GuardReason.Guarding,
            };
            var inverse = Quaternion.Inverse(input.Body);
            result.Offset = inverse * (input.Hand - input.Guard);
            var fromHead = inverse * (input.Hand - input.Head);
            result.FrontOfFace = fromHead.z;
            result.SideOfFace = fromHead.x;

            if (!input.Calibrated) return Fail(result, GuardReason.NotCalibrated);
            if (!input.Tracked) return Fail(result, GuardReason.Untracked);

            var margin = input.WasGuarding ? settings.releaseMargin : 0f;
            var half = settings.zoneHalfSize + Vector3.one * margin;
            half.x += Mathf.Max(0f, input.ExtraHalfWidth);
            var o = result.Offset;
            if (Mathf.Abs(o.x) > half.x || Mathf.Abs(o.y) > half.y || Mathf.Abs(o.z) > half.z) return Fail(result, GuardReason.OutOfZone);
            if (result.FrontOfFace < settings.minFrontOfFace - margin) return Fail(result, GuardReason.BehindFace);

            var limit = settings.maxForearmTiltDegrees + (input.WasGuarding ? settings.releaseDegrees : 0f);
            if (!TiltOk(result, settings.forearmSource, limit)) return Fail(result, GuardReason.ForearmTilted);

            result.IsGuarding = true;
            return result;
        }

        /// <summary>방패가 막는 쪽(M4-03): 손이 얼굴 가운데보다 왼쪽이면 왼쪽.</summary>
        public static PilotSide CoverSide(GuardResult r) => r.SideOfFace < 0f ? PilotSide.Left : PilotSide.Right;

        /// <summary>양팔 가드(강공격): 두 팔 모두 가드.</summary>
        public static bool BothGuarding(GuardResult left, GuardResult right) => left.IsGuarding && right.IsGuarding;

        static bool TiltOk(GuardResult r, ForearmSource source, float limit)
        {
            var human = !float.IsNaN(r.HumanTilt) && r.HumanTilt <= limit;
            var robot = !float.IsNaN(r.RobotTilt) && r.RobotTilt <= limit;
            switch (source)
            {
                case ForearmSource.Robot: return robot;
                case ForearmSource.Either: return human || robot;
                default: return human;
            }
        }

        static GuardResult Fail(GuardResult result, GuardReason reason)
        {
            result.IsGuarding = false;
            result.Reason = reason;
            return result;
        }
    }
}
