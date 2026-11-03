using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class ComboSettings
    {
        [Tooltip("연계 다음 명중까지 제한 시간(초).")]
        [Min(0.1f)]
        public float comboSeconds = 2.4f;

        [Tooltip("연계 단계 사이 최소 간격(초). 두 손을 동시에 내밀어 한 번에 두 단계 오르는 것을 막는다.")]
        [Min(0f)]
        public float minComboGap = 0.15f;

        [Tooltip("어퍼컷은 연계(잽–잽–훅) 밖의 단독 기술. 켜면 어퍼컷을 쳐도 연계가 이어진다(시간은 흐름). 끄면 차례 아닌 공격으로 보고 끊는다.")]
        public bool uppercutKeepsCombo = true;
    }

    /// <summary>한 번의 명중이 연계에 준 영향.</summary>
    public struct ComboOutcome
    {
        /// <summary>명중 뒤 단계. 잽–잽–훅이 완성되면 3(그 뒤 다음 명중 전에 0으로 돌아간다).</summary>
        public int Step;
        /// <summary>단계가 올라갔다(새 연계 시작 포함).</summary>
        public bool Advanced;
        /// <summary>잽–잽–훅 완성.</summary>
        public bool Finished;
        /// <summary>이 명중 전에 있던 연계가 끊겼다.</summary>
        public ComboBreak Broken;
    }

    /// <summary>
    /// 잽(A팔) → 잽(B팔) → 훅(A팔) 연계(01_combat_rules "잽–잽–훅 연계"). A는 어느 쪽이든 된다.
    /// 각 단계는 명중해야 진행. 이미 준 피해는 연계가 끊겨도 유지되므로 여기서는 단계만 다룬다.
    /// 기존 레포 RobotCombat.Hit의 단계 처리를 옮겼다.
    /// </summary>
    public sealed class ComboRules
    {
        readonly ComboSettings settings;
        PilotSide first, next;
        double deadline, lastStepTime = double.NegativeInfinity;

        public ComboRules(ComboSettings settings = null)
        {
            this.settings = settings ?? new ComboSettings();
        }

        /// <summary>0 = 연계 없음, 1 = 첫 잽, 2 = 두 번째 잽(다음은 첫 팔 훅).</summary>
        public int Step { get; private set; }

        /// <summary>다음 차례 팔(Step &gt; 0일 때만 의미).</summary>
        public PilotSide NextSide => next;

        public double Deadline => deadline;

        public void Reset()
        {
            Step = 0;
            lastStepTime = double.NegativeInfinity;
        }

        /// <summary>시간 초과 검사. 끊겼으면 Timeout.</summary>
        public ComboBreak Tick(double time)
        {
            if (Step > 0 && time > deadline)
            {
                Reset();
                return ComboBreak.Timeout;
            }
            return ComboBreak.None;
        }

        /// <summary>명중 하나.</summary>
        public ComboOutcome OnHit(PilotSide side, PunchKind kind, double time)
        {
            var outcome = new ComboOutcome { Broken = Tick(time) };
            var onTurn = Step > 0 && side == next && time - lastStepTime >= settings.minComboGap;
            switch (kind)
            {
                case PunchKind.Jab:
                    if (Step == 1 && onTurn)
                    {
                        Step = 2;
                        next = first;
                    }
                    else
                    {
                        // 차례가 아니면 지금 연계는 끊기고, 이 잽이 새 연계의 첫 단계가 된다.
                        if (Step > 0) outcome.Broken = ComboBreak.WrongTurn;
                        first = side;
                        next = Other(side);
                        Step = 1;
                    }
                    outcome.Advanced = true;
                    lastStepTime = time;
                    deadline = time + settings.comboSeconds;
                    break;

                case PunchKind.Hook:
                    if (Step == 2 && onTurn)
                    {
                        outcome.Finished = true;
                        outcome.Advanced = true;
                        outcome.Step = 3;
                        Reset();
                        return outcome;
                    }
                    if (Step > 0)
                    {
                        outcome.Broken = ComboBreak.WrongTurn;
                        Reset();
                    }
                    break;

                case PunchKind.Uppercut:
                    if (Step > 0 && !settings.uppercutKeepsCombo)
                    {
                        outcome.Broken = ComboBreak.WrongTurn;
                        Reset();
                    }
                    break;
            }
            outcome.Step = Step;
            return outcome;
        }

        /// <summary>그립을 쥐고 친 공격이 빗나감.</summary>
        public ComboBreak OnMiss(double time)
        {
            var broken = Tick(time);
            if (broken != ComboBreak.None) return broken;
            if (Step == 0) return ComboBreak.None;
            Reset();
            return ComboBreak.Miss;
        }

        /// <summary>로봇이 직접 피해를 받음(M2 보스 공격).</summary>
        public ComboBreak OnDamaged(double time)
        {
            var broken = Tick(time);
            if (broken != ComboBreak.None) return broken;
            if (Step == 0) return ComboBreak.None;
            Reset();
            return ComboBreak.Damaged;
        }

        static PilotSide Other(PilotSide side) => side == PilotSide.Left ? PilotSide.Right : PilotSide.Left;
    }
}
