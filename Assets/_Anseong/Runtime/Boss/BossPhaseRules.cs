using System;

namespace Anseong.Boss
{
    public enum PhaseStep
    {
        None,
        /// <summary>경계에 닿음: 진행 중인 공격이 끝나기를 기다린다.</summary>
        Pending,
        /// <summary>새 페이즈 시작(포효).</summary>
        PhaseChanged,
        /// <summary>마지막 경계: 무릎.</summary>
        Kneel,
    }

    /// <summary>
    /// 페이즈 전환(05 "페이즈 전환", "끝" 1단계). 순수 클래스.
    /// 보스 HP가 경계(60·25·10%)에 닿으면 그 아래로 안 내려가게 바닥을 두고(<see cref="FloorPercent"/>), 공격이 끝나면 다음 페이즈(포효 무적)로,
    /// 마지막 경계면 무릎. 무릎 뒤에도 바닥은 10%로 남아 필살기만 보스를 쓰러뜨린다(M2-05에서 <see cref="ReleaseFloor"/>).
    /// </summary>
    public sealed class BossPhaseRules
    {
        readonly float[] thresholds;
        readonly float roarSeconds;
        double roarEnd = double.NegativeInfinity;
        bool floorReleased;

        public BossPhaseRules(float[] thresholds = null, float roarSeconds = 2f)
        {
            this.thresholds = thresholds != null && thresholds.Length > 0 ? (float[])thresholds.Clone() : new[] { 60f, 25f, 10f };
            Array.Sort(this.thresholds);
            Array.Reverse(this.thresholds);
            this.roarSeconds = roarSeconds;
        }

        /// <summary>1부터. 경계 수 + 1이 되면 무릎.</summary>
        public int Phase { get; private set; } = 1;
        public int PhaseCount => thresholds.Length;
        public bool Kneeling { get; private set; }
        public bool Pending { get; private set; }

        /// <summary>지금 HP 바닥(%). 무릎 뒤 <see cref="ReleaseFloor"/> 전까지는 마지막 경계.</summary>
        public float FloorPercent
        {
            get
            {
                if (floorReleased) return 0f;
                var i = Math.Min(Phase - 1, thresholds.Length - 1);
                return thresholds[i];
            }
        }

        /// <summary>포효 중(무적).</summary>
        public bool IsRoaring(double now) => now < roarEnd;

        /// <summary>
        /// 서버 프레임마다. hpPercent = 보스 HP(%), busy = 보스가 공격 중(예고·명중·회복). 무엇이 일어났는지 돌려준다.
        /// </summary>
        public PhaseStep Update(double now, float hpPercent, bool busy)
        {
            if (Kneeling) return PhaseStep.None;
            if (!Pending)
            {
                if (hpPercent > FloorPercent + 1e-3f) return PhaseStep.None;
                Pending = true;
            }
            if (busy) return PhaseStep.Pending;
            Pending = false;
            if (Phase >= thresholds.Length)
            {
                Kneeling = true;
                return PhaseStep.Kneel;
            }
            Phase++;
            roarEnd = now + roarSeconds;
            return PhaseStep.PhaseChanged;
        }

        /// <summary>필살기로 끝낼 때 바닥을 없앤다(M2-05).</summary>
        public void ReleaseFloor() => floorReleased = true;

        public void Reset()
        {
            Phase = 1;
            Kneeling = Pending = floorReleased = false;
            roarEnd = double.NegativeInfinity;
        }
    }
}
