using System.Globalization;
using System.Text;

namespace Anseong.Boss
{
    /// <summary>
    /// 한 판 기록(M2-04 7번): 페이즈별 걸린 시간·받은 피해·가드(회피 포함) 성공률. 한 판 길이(목표 보스전 4~7분) 조정용. 순수 클래스.
    /// </summary>
    public sealed class BossFightStats
    {
        public const string Header = "run,seed,result,total_s,p1_s,p2_s,p3_s,kneel_s,p1_damage,p2_damage,p3_damage,p1_guard,p2_guard,p3_guard,boss_attacks";
        const int Slots = 4; // 페이즈 1·2·3 + 무릎

        readonly double[] phaseStart = new double[Slots];
        readonly double[] phaseSeconds = new double[Slots];
        readonly float[] damage = new float[Slots];
        readonly int[] strikes = new int[Slots];
        readonly int[] defended = new int[Slots];
        int slot = -1;
        double start;

        public int Run { get; private set; }
        public bool Running => slot >= 0;
        public int Attacks { get; private set; }

        public void Begin(double now)
        {
            Run++;
            System.Array.Clear(phaseSeconds, 0, Slots);
            System.Array.Clear(damage, 0, Slots);
            System.Array.Clear(strikes, 0, Slots);
            System.Array.Clear(defended, 0, Slots);
            Attacks = 0;
            start = now;
            slot = 0;
            phaseStart[0] = now;
        }

        /// <summary>페이즈 2·3, 무릎은 4.</summary>
        public void EnterPhase(int phase, double now)
        {
            if (!Running) return;
            Close(now);
            slot = System.Math.Max(0, System.Math.Min(Slots - 1, phase - 1));
            phaseStart[slot] = now;
        }

        public void AttackStarted() => Attacks++;

        /// <summary>보스 명중 순간 하나와 그 결과(막음·피함이면 true).</summary>
        public void Strike(bool success)
        {
            if (!Running) return;
            strikes[slot]++;
            if (success) defended[slot]++;
        }

        public void PilotDamage(float amount)
        {
            if (Running) damage[slot] += amount;
        }

        /// <summary>판 끝(승리·재시작·종료). CSV 한 줄.</summary>
        public string Finish(double now, int seed, string result)
        {
            if (!Running) return null;
            Close(now);
            slot = -1;
            var c = CultureInfo.InvariantCulture;
            var b = new StringBuilder();
            b.Append(Run).Append(',').Append(seed).Append(',').Append(result).Append(',').Append((now - start).ToString("F1", c));
            for (var i = 0; i < Slots; i++) b.Append(',').Append(phaseSeconds[i].ToString("F1", c));
            for (var i = 0; i < 3; i++) b.Append(',').Append(damage[i].ToString("F1", c));
            for (var i = 0; i < 3; i++) b.Append(',').Append(strikes[i] > 0 ? ((float)defended[i] / strikes[i]).ToString("F2", c) : string.Empty);
            b.Append(',').Append(Attacks);
            return b.ToString();
        }

        void Close(double now)
        {
            if (slot >= 0) phaseSeconds[slot] += now - phaseStart[slot];
        }
    }
}
