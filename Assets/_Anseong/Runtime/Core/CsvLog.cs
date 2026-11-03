#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Anseong.Core
{
    /// <summary>
    /// 개발용 CSV 기록. 줄을 모았다가 백그라운드에서 쓰므로 메인 스레드를 막지 않는다. 기록 실패는 게임을 멈추지 않는다.
    /// </summary>
    public sealed class CsvLog : IDisposable
    {
        readonly ConcurrentQueue<string> pending = new ConcurrentQueue<string>();
        readonly string header;
        readonly object fileLock = new object();
        Task writing = Task.CompletedTask;
        bool headerWritten;

        public CsvLog(string path, string header)
        {
            Path = path;
            this.header = header;
        }

        public string Path { get; }

        /// <summary>logs/&lt;name&gt;_yyyyMMdd_HHmmss.csv</summary>
        public static string DefaultPath(string root, string name, DateTime now)
        {
            return System.IO.Path.Combine(root, "logs", name + "_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
        }

        public static string Number(float value, string format = "F3")
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? string.Empty : value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Add(string line) => pending.Enqueue(line);

        public void Pump()
        {
            if (pending.IsEmpty || !writing.IsCompleted) return;
            writing = Task.Run(new Action(Drain));
        }

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
                if (!headerWritten) text.Append(header).Append('\n');
                string line;
                while (pending.TryDequeue(out line)) text.Append(line).Append('\n');
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    File.AppendAllText(Path, text.ToString(), new UTF8Encoding(false));
                    headerWritten = true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
#endif
