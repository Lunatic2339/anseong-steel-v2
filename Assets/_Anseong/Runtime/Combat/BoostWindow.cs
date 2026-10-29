using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class BoostSettings
    {
        [Tooltip("연계 마지막 훅 명중 뒤 부스트(트리거) 입력창(초).")]
        [Min(0.05f)]
        public float windowSeconds = 0.45f;
    }

    /// <summary>
    /// 팔꿈치 부스터 추가타 입력창(01 "부스트"). 연계 마지막 훅이 명중한 뒤에만 열리고, 그 팔 그립을 유지한 채 트리거를 새로 누르면 성공.
    /// 미리 누르고 있던 트리거는 세지 않는다(누르는 순간만). 그립을 놓으면 취소.
    /// </summary>
    public sealed class BoostWindow
    {
        readonly BoostSettings settings;
        double deadline;

        public BoostWindow(BoostSettings settings = null)
        {
            this.settings = settings ?? new BoostSettings();
        }

        public bool IsOpen { get; private set; }
        public PilotSide Side { get; private set; }
        public double Deadline => deadline;

        public void Open(PilotSide side, double time)
        {
            IsOpen = true;
            Side = side;
            deadline = time + settings.windowSeconds;
        }

        public void Close() => IsOpen = false;

        public enum Result { None, Boosted, Missed }

        /// <summary>
        /// 프레임마다 그 팔 입력. triggerPressed = 이번 프레임에 트리거를 새로 누름.
        /// Boosted = 성공(창 닫힘), Missed = 시간 초과나 그립 놓음(창 닫힘).
        /// </summary>
        public Result Update(PilotSide side, bool gripHeld, bool triggerPressed, double time)
        {
            if (!IsOpen || side != Side) return Result.None;
            if (time > deadline || !gripHeld)
            {
                IsOpen = false;
                return Result.Missed;
            }
            if (!triggerPressed) return Result.None;
            IsOpen = false;
            return Result.Boosted;
        }

        /// <summary>입력이 없어도 시간이 지나면 닫는다.</summary>
        public Result Tick(double time)
        {
            if (!IsOpen || time <= deadline) return Result.None;
            IsOpen = false;
            return Result.Missed;
        }
    }
}
