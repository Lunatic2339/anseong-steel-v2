using System;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>결과판 숫자(10_game_scenario.md 장면 10). 네트워크로 복제한다.</summary>
    [Serializable]
    public struct MatchResult : IEquatable<MatchResult>
    {
        public bool Won;
        /// <summary>출격(장면 2)부터 끝까지 걸린 시간(초).</summary>
        public float Seconds;
        /// <summary>막기 성공률(%): (막기 + 같이 피하기) / (그것 + 맞은 수). 보스 공격이 없었으면 -1.</summary>
        public sbyte BlockPercent;
        public byte Drones;
        /// <summary>최고 연계 단계(3 = 번갈아 세 번 완성).</summary>
        public byte BestCombo;
        public byte Reboots;

        public bool Equals(MatchResult o) =>
            Won == o.Won && Seconds.Equals(o.Seconds) && BlockPercent == o.BlockPercent && Drones == o.Drones && BestCombo == o.BestCombo && Reboots == o.Reboots;
        public override bool Equals(object obj) => obj is MatchResult o && Equals(o);
        public override int GetHashCode() => (Won ? 1 : 0) ^ Seconds.GetHashCode() ^ (BlockPercent << 8) ^ (Drones << 16) ^ (BestCombo << 24);
    }

    /// <summary>M6 테스트 기록: 판마다 결과 한 줄(Logs/Anseong/logs/matches_*.csv, 호스트 PC). 외부인 10쌍 완주 기록·수치 조정에 쓴다.</summary>
    public static class MatchLogRow
    {
        public const string Header = "time,won,seconds,block_percent,drones,best_combo,reboots";

        public static string Format(MatchResult r, DateTime now)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join(",",
                now.ToString("yyyy-MM-dd HH:mm:ss", c),
                r.Won ? "1" : "0",
                r.Seconds.ToString("F1", c),
                r.BlockPercent < 0 ? string.Empty : r.BlockPercent.ToString(c),
                r.Drones.ToString(c),
                r.BestCombo.ToString(c),
                r.Reboots.ToString(c));
        }
    }

    /// <summary>한 판 동안 결과판 숫자를 모은다(M4-01). 서버만. 순수 클래스.</summary>
    public sealed class MatchStats
    {
        double start = double.NaN;
        int blocked, hit, drones, bestCombo;

        public bool Running => !double.IsNaN(start);

        public void Reset()
        {
            start = double.NaN;
            blocked = hit = drones = bestCombo = 0;
        }

        /// <summary>출격 순간부터 잰다. 이미 재고 있으면 그대로(재기동해도 시간은 이어진다).</summary>
        public void Begin(double now)
        {
            if (!Running) start = now;
        }

        // 출격 전(기체 점검 연습)은 세지 않는다.
        public void Blocked() { if (Running) blocked++; }
        public void Hit() { if (Running) hit++; }
        public void DroneDown() { if (Running) drones++; }
        public void Combo(int step) { if (Running) bestCombo = Mathf.Max(bestCombo, step); }

        public MatchResult Finish(double now, bool won, int reboots)
        {
            var total = blocked + hit;
            return new MatchResult
            {
                Won = won,
                Seconds = Running ? (float)Math.Max(0d, now - start) : 0f,
                BlockPercent = total > 0 ? (sbyte)Mathf.RoundToInt(blocked * 100f / total) : (sbyte)-1,
                Drones = (byte)Mathf.Clamp(drones, 0, 255),
                BestCombo = (byte)Mathf.Clamp(bestCombo, 0, 255),
                Reboots = (byte)Mathf.Clamp(reboots, 0, 255),
            };
        }
    }
}
