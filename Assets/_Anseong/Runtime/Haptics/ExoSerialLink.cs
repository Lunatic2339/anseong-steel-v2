using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Anseong.Haptics
{
    public enum ExoLinkState
    {
        /// <summary>포트 이름이 비었거나 꺼짐.</summary>
        Off,
        /// <summary>포트가 아직 없거나 끊김. 2초마다 다시 찾는다.</summary>
        Searching,
        Connected,
        /// <summary>이 빌드에서 System.IO.Ports를 못 씀(Api Compatibility Level이 .NET Framework가 아님).</summary>
        Unsupported,
    }

    /// <summary>
    /// ESP32 한 대와 USB 시리얼로 이야기하는 별도 스레드(02 "Unity: 별도 스레드에서 포트 쓰기").
    /// 포트가 없거나 끊기면 조용히 다시 찾기만 하고 게임에는 영향이 없다. 끊긴 동안 쌓인 명령은 버린다(늦은 저항 방지).
    /// 살아 있음 신호 H는 이 스레드가 100ms마다 보내되, 메인 스레드가 0.3초 넘게 멈췄으면 보내지 않아 펌웨어 워치독(200ms)이 SAFE로 간다.
    /// </summary>
    public sealed class ExoSerialLink : IDisposable
    {
        const int HeartbeatMs = 100;
        const int MainAliveMs = 300;
        const int RetryMs = 2000;
        const int LoopSleepMs = 5;

        readonly string portName;
        readonly int baud;
        readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> received = new ConcurrentQueue<string>();
        readonly StringBuilder lineBuffer = new StringBuilder(64);
        Thread thread;
        volatile bool stopRequested;
        volatile int state;
        long aliveTimestamp;

        public ExoSerialLink(string portName, int baud = ExoProtocol.BaudRate)
        {
            this.portName = portName;
            this.baud = baud;
            MarkAlive();
        }

        public string PortName => portName;
        public ExoLinkState State => (ExoLinkState)state;

        public void Start()
        {
            if (thread != null) return;
            if (string.IsNullOrEmpty(portName))
            {
                state = (int)ExoLinkState.Off;
                return;
            }
#if NET_4_6
            state = (int)ExoLinkState.Searching;
            thread = new Thread(Run) { IsBackground = true, Name = "ExoSerial " + portName };
            thread.Start();
#else
            state = (int)ExoLinkState.Unsupported;
#endif
        }

        /// <summary>명령 한 줄(줄 끝 \n은 붙이지 않음). 연결이 없으면 버려진다.</summary>
        public void Send(string line)
        {
            if (string.IsNullOrEmpty(line) || State != ExoLinkState.Connected) return;
            outgoing.Enqueue(line);
        }

        /// <summary>메인 스레드가 매 프레임 부른다. 이것이 멈추면 살아 있음 신호도 멈춘다.</summary>
        public void MarkAlive() => Interlocked.Exchange(ref aliveTimestamp, Stopwatch.GetTimestamp());

        /// <summary>ESP32가 보낸 줄(S,... 등). 메인 스레드에서 꺼내 기록한다.</summary>
        public bool TryReceive(out string line) => received.TryDequeue(out line);

        /// <summary>X를 보내고 포트를 닫는다(최대 0.5초 기다림).</summary>
        public void Dispose()
        {
            stopRequested = true;
            if (thread != null && thread.IsAlive) thread.Join(500);
            thread = null;
            state = (int)ExoLinkState.Off;
        }

        static long ElapsedMs(long fromTimestamp)
        {
            return (Stopwatch.GetTimestamp() - fromTimestamp) * 1000L / Stopwatch.Frequency;
        }

#if NET_4_6
        System.IO.Ports.SerialPort port;

        void Run()
        {
            var nextOpen = 0L;
            var lastHeartbeat = 0L;
            var clock = Stopwatch.StartNew();
            while (!stopRequested)
            {
                var now = clock.ElapsedMilliseconds;
                if (port == null)
                {
                    while (outgoing.TryDequeue(out _)) { }
                    if (now >= nextOpen)
                    {
                        nextOpen = now + RetryMs;
                        TryOpen();
                    }
                    if (port == null)
                    {
                        Thread.Sleep(50);
                        continue;
                    }
                    lastHeartbeat = now;
                }

                try
                {
                    while (outgoing.TryDequeue(out var line)) port.Write(line + "\n");
                    if (now - lastHeartbeat >= HeartbeatMs && ElapsedMs(Interlocked.Read(ref aliveTimestamp)) <= MainAliveMs)
                    {
                        port.Write(ExoProtocol.Heartbeat + "\n");
                        lastHeartbeat = now;
                    }
                    if (port.BytesToRead > 0) ReadLines(port.ReadExisting());
                }
                catch (Exception)
                {
                    // 선이 빠짐·장치 재부팅 등. 조용히 닫고 다시 찾는다.
                    ClosePort(false);
                    nextOpen = now + RetryMs;
                }
                Thread.Sleep(LoopSleepMs);
            }
            ClosePort(true);
        }

        void TryOpen()
        {
            try
            {
                var found = false;
                foreach (var name in System.IO.Ports.SerialPort.GetPortNames())
                    if (string.Equals(name, portName, StringComparison.OrdinalIgnoreCase)) found = true;
                if (!found) return;

                var p = new System.IO.Ports.SerialPort(portName, baud)
                {
                    NewLine = "\n",
                    ReadTimeout = 10,
                    WriteTimeout = 50,
                };
                p.Open();
                p.Write(ExoProtocol.Release + "\n");
                port = p;
                lineBuffer.Length = 0;
                state = (int)ExoLinkState.Connected;
            }
            catch (Exception)
            {
                port = null;
            }
        }

        void ClosePort(bool sendRelease)
        {
            var p = port;
            port = null;
            if (!stopRequested) state = (int)ExoLinkState.Searching;
            if (p == null) return;
            try
            {
                if (sendRelease && p.IsOpen) p.Write(ExoProtocol.Release + "\n");
            }
            catch (Exception) { }
            try { p.Close(); }
            catch (Exception) { }
        }

        void ReadLines(string chunk)
        {
            foreach (var ch in chunk)
            {
                if (ch == '\n')
                {
                    var line = lineBuffer.ToString().Trim();
                    lineBuffer.Length = 0;
                    if (line.Length > 0 && received.Count < 256) received.Enqueue(line);
                }
                else if (lineBuffer.Length < 256)
                {
                    lineBuffer.Append(ch);
                }
            }
        }
#endif
    }
}
