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
    /// 가드 판정 기록(개발용, M1-06). 정해진 간격(기본 10Hz)마다 팔별 한 줄. 사람 기준·로봇 기준 전완 각도를 같이 남겨
    /// 화면 자세와 판정이 어긋나는지 본다. 쓰기는 백그라운드(StrikeLog와 같은 방식).
    /// </summary>
    public sealed class GuardLog : IDisposable
    {
        public const string Header = "time_s,arm,guard,reason,offset_x_m,offset_y_m,offset_z_m,front_of_face_m,human_tilt_deg,robot_tilt_deg";

        readonly ConcurrentQueue<string> pending = new ConcurrentQueue<string>();
        readonly string path;
        readonly object fileLock = new object();
        Task writing = Task.CompletedTask;
        bool headerWritten;

        public GuardLog(string path)
        {
            this.path = path;
        }

        public string Path => path;

        /// <summary>logs/guard_yyyyMMdd_HHmmss.csv</summary>
        public static string DefaultPath(string root, DateTime now)
        {
            return System.IO.Path.Combine(root, "logs", "guard_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
        }

        public static string FormatRow(double time, PilotSide arm, GuardResult r)
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                time.ToString("F3", c),
                arm.ToString(),
                r.IsGuarding ? "1" : "0",
                r.Reason.ToString(),
                Number(r.Offset.x),
                Number(r.Offset.y),
                Number(r.Offset.z),
                Number(r.FrontOfFace),
                Number(r.HumanTilt),
                Number(r.RobotTilt));
        }

        static string Number(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? string.Empty : value.ToString("F3", CultureInfo.InvariantCulture);
        }

        public void Add(double time, PilotSide arm, GuardResult result)
        {
            pending.Enqueue(FormatRow(time, arm, result));
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
                    // 기록 실패는 게임을 멈추지 않는다.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
#endif
