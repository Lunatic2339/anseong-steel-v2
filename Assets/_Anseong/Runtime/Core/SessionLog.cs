#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Anseong.Core
{
    /// <summary>
    /// Play 한 번 동안의 Console 줄을 logs/session_<날짜시각>.txt에 남긴다(개발용, 선우 10/28: 기록을 바로바로 살펴볼 수 있게).
    /// 우리 컴포넌트 줄([이름]으로 시작)과 경고·오류만 쓴다. 보정 결과·명중·헛침 거리가 여기 다 남는다.
    /// </summary>
    public sealed class SessionLog : IDisposable
    {
        readonly string path;
        readonly object fileLock = new object();
        readonly DateTime start = DateTime.Now;

        public SessionLog(string root, DateTime now)
        {
            path = System.IO.Path.Combine(root, "logs", "session_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
            try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            Application.logMessageReceivedThreaded += OnLog;
        }

        public string Path => path;

        public void Dispose() => Application.logMessageReceivedThreaded -= OnLog;

        void OnLog(string message, string stackTrace, LogType type)
        {
            var ours = message.StartsWith("[", StringComparison.Ordinal);
            if (!ours && type == LogType.Log) return;
            var line = new StringBuilder()
                .Append((DateTime.Now - start).TotalSeconds.ToString("F2", CultureInfo.InvariantCulture))
                .Append(type == LogType.Log ? " " : " [" + type + "] ")
                .Append(message.Replace('\n', ' '));
            if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace)) line.Append(" | ").Append(stackTrace.Replace('\n', ' '));
            line.Append('\n');
            lock (fileLock)
            {
                try { File.AppendAllText(path, line.ToString(), new UTF8Encoding(false)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
#endif
