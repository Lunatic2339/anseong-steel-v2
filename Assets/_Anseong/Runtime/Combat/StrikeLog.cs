#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Anseong.Pilot;

namespace Anseong.Combat
{
    /// <summary>
    /// 펀치 동작 기록(개발용). 동작마다 CSV 한 줄. 쓰기는 모았다가 백그라운드에서 하므로 메인 스레드를 막지 않는다.
    /// 명중 열: 1 = 명중, 0 = 공격이었지만 빗나감, 빈칸 = 공격 아님(M1-05).
    /// </summary>
    public sealed class StrikeLog : IDisposable
    {
        public const string Header = "time_s,arm,kind,reason,side_ratio,straightness,outset_m,inward_sweep_m,arc_ratio,turn_deg,rise_m,head_forward_m,grip,tracked,strike_id,hit";

        readonly ConcurrentQueue<string> pending = new ConcurrentQueue<string>();
        readonly string path;
        readonly object fileLock = new object();
        Task writing = Task.CompletedTask;
        bool headerWritten;

        public StrikeLog(string path)
        {
            this.path = path;
        }

        public string Path => path;

        /// <summary>logs/strikes_yyyyMMdd_HHmmss.csv</summary>
        public static string DefaultPath(string root, DateTime now)
        {
            return System.IO.Path.Combine(root, "logs", "strikes_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
        }

        public static string FormatRow(PilotSide arm, PunchResult r, string hit = "")
        {
            var m = r.Measure;
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                r.EndTime.ToString("F3", c),
                arm.ToString(),
                r.Kind.ToString(),
                r.Reason.ToString(),
                Number(m.SideRatio),
                Number(m.Straightness),
                Number(m.Outset),
                Number(m.InwardSweep),
                Number(m.ArcRatio),
                Number(m.TurnDegrees),
                Number(m.Rise),
                Number(r.HeadForward),
                r.GripHeld ? "1" : "0",
                r.Tracked ? "1" : "0",
                r.StrikeId.ToString(c),
                hit ?? string.Empty);
        }

        static string Number(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? string.Empty : value.ToString("F3", CultureInfo.InvariantCulture);
        }

        public void Add(PilotSide arm, PunchResult result, string hit = "")
        {
            pending.Enqueue(FormatRow(arm, result, hit));
        }

        /// <summary>쌓인 줄을 백그라운드에서 쓴다. 이미 쓰는 중이면 다음에.</summary>
        public void Pump()
        {
            if (pending.IsEmpty || !writing.IsCompleted) return;
            writing = Task.Run(new Action(Drain));
        }

        /// <summary>남은 줄을 모두 쓰고 끝낸다.</summary>
        public void Dispose()
        {
            try { writing.Wait(); } catch (AggregateException) { }
            Drain();
        }

        void Drain()
        {
            if (pending.IsEmpty) return;
            lock (fileLock)
            {
                var text = new StringBuilder();
                if (!headerWritten) text.Append(Header).Append('\n');
                string line;
                while (pending.TryDequeue(out line)) text.Append(line).Append('\n');
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    File.AppendAllText(path, text.ToString(), new UTF8Encoding(false));
                    headerWritten = true;
                }
                catch (IOException)
                {
                    // 기록 실패는 게임을 멈추지 않는다. 다음 Pump에서 다시 쓰지 않고 버린다.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
#endif
