using System;
using Anseong.Combat;
using Anseong.Core;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Haptics
{
    /// <summary>
    /// 팔 하나의 외골격 ESP32 연결(M3-04). 전투 이벤트를 듣고 <see cref="ExoCueRules"/>대로 명령을 보낸다.
    /// 1인 양팔은 이것 두 개(왼팔·오른팔), 2인 모드는 각 PC가 자기 팔 것만 포트를 채운다.
    /// 포트 이름이 비었거나 장치가 없으면 아무것도 하지 않는다(외골격이 없어도 게임은 끝까지 돈다).
    /// 빌드에서는 실행 인자 -exoLeft COM3 / -exoRight COM4 로 포트를 덮어쓴다.
    /// </summary>
    public class ExoArmLink : MonoBehaviour, ITunable
    {
        [SerializeField, Tooltip("이 장치가 맡는 팔.")]
        PilotSide arm = PilotSide.Left;

        [SerializeField, Tooltip("ESP32 포트 이름(예: COM3, 장치 관리자 → 포트). 비우면 끔. 빌드는 실행 인자 -exoLeft/-exoRight가 우선.")]
        string portName = "";

        [SerializeField, Tooltip("외골격 명령을 보낸다. 끄면 연결은 유지하고 X만 한 번 보낸다.")]
        bool sendCommands = true;

        [SerializeField, Tooltip("전투 판정. 비어 있으면 시작할 때 한 번 찾는다.")]
        CombatDirector director;

        [SerializeField] ExoCueSettings settings = new ExoCueSettings();

        [SerializeField, Tooltip("보낸 명령·받은 상태를 CSV로 남긴다(에디터·개발 빌드, logs/exo_<날짜시각>.csv). 고건 지연 측정·세기 맞추기용.")]
        bool writeCsv = true;

        [SerializeField, Tooltip("연결·끊김을 Console에 한 줄.")]
        bool logToConsole = true;

        ExoSerialLink link;
        ExoLinkState lastState = ExoLinkState.Off;
        string lastStatus;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CsvLog csv;
#endif

        public PilotSide Arm => arm;
        public ExoLinkState State => link != null ? link.State : ExoLinkState.Off;
        /// <summary>마지막으로 받은 상태 줄(S,...). 안내판·조절 메뉴 표시용.</summary>
        public string LastStatus => lastStatus;
        public ExoCueSettings Settings => settings;

        void OnEnable()
        {
            var port = PortFromCommandLine(Environment.GetCommandLineArgs(), arm == PilotSide.Left ? "-exoLeft" : "-exoRight") ?? portName;
            link = new ExoSerialLink(port != null ? port.Trim() : null);
            link.Start();
            lastState = link.State;
            if (logToConsole && lastState == ExoLinkState.Unsupported)
                Debug.LogWarning($"[Exo] {arm}: 이 빌드는 시리얼 포트를 못 씀(Api Compatibility Level = .NET Framework 필요). 외골격 없이 진행.", this);
        }

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CombatDirector>();
            if (director != null) director.Cue += OnCue;
        }

        void OnDisable()
        {
            link?.Dispose();
            link = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            csv?.Dispose();
            csv = null;
#endif
        }

        void OnDestroy()
        {
            if (director != null) director.Cue -= OnCue;
        }

        void Update()
        {
            if (link == null) return;
            link.MarkAlive();

            var state = link.State;
            if (state != lastState)
            {
                if (logToConsole)
                {
                    if (state == ExoLinkState.Connected) Debug.Log($"[Exo] {arm} 외골격 연결됨({link.PortName})", this);
                    else if (lastState == ExoLinkState.Connected) Debug.Log($"[Exo] {arm} 외골격 끊김({link.PortName}). 다시 찾는 중, 게임은 계속.", this);
                }
                Record("state", state.ToString());
                lastState = state;
            }

            while (link.TryReceive(out var line))
            {
                if (ExoProtocol.TryParseStatus(line, out _)) lastStatus = line;
                Record("in", line);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            csv?.Pump();
#endif
        }

        void OnCue(CombatEvent e)
        {
            if (link == null || !sendCommands) return;
            Send(ExoCueRules.CommandFor(e, arm, settings));
        }

        /// <summary>다른 컴포넌트(보스 예고·가드 등)가 직접 보낼 때. 연결이 없으면 버려진다.</summary>
        public void Send(string command)
        {
            if (string.IsNullOrEmpty(command) || link == null || link.State != ExoLinkState.Connected) return;
            link.Send(command);
            Record("out", command);
        }

        void SetSendCommands(bool on)
        {
            if (sendCommands && !on && link != null) link.Send(ExoProtocol.Release);
            sendCommands = on;
        }

        /// <summary>"-exoLeft COM3"에서 COM3. 없으면 null.</summary>
        public static string PortFromCommandLine(string[] args, string key)
        {
            if (args == null) return null;
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        void Record(string direction, string line)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!writeCsv) return;
            if (csv == null)
            {
                if (State == ExoLinkState.Off) return;
                csv = new CsvLog(CsvLog.DefaultPath(LogFolder.Root, "exo_" + arm.ToString().ToLowerInvariant(), DateTime.Now), "time_s,arm,dir,line");
            }
            csv.Add(string.Join(",", Time.realtimeSinceStartupAsDouble.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), arm.ToString(), direction, line));
#endif
        }

        public void AddTuning(TuningList list)
        {
            const string g = "외골격";
            list.Describe(g, "할 것: 외골격 차고 잽·훅 명중, 헛치기, 부스트\n볼 것: 명중 때만 짧게 버팀, 헛치면·부스트면 바로 풀림");
            list.AddToggle(g, "exo.send", "외골격 명령 보내기", () => sendCommands, SetSendCommands);
            list.AddFloat(g, "exo.hit.strength", "명중 세기(0-255)", () => settings.hitStrength, v => settings.hitStrength = Mathf.RoundToInt(v), 0f, 255f, 5f, 0);
            list.AddFloat(g, "exo.hit.ms", "명중 버티는 시간(ms)", () => settings.hitMs, v => settings.hitMs = Mathf.RoundToInt(v), 20f, 500f, 10f, 0);
            list.AddToggle(g, "exo.prepare", "펀치 발사 때 미리 감기(P)", () => settings.prepareOnStrike, v => settings.prepareOnStrike = v);
            list.AddCheck(g, "exo.hit", "명중 때 팔 펴는 쪽이 짧게 막힘");
            list.AddCheck(g, "exo.free", "평소엔 팔이 자유로움");
            list.AddCheck(g, "exo.release", "헛치기·부스트 때 바로 풀림");
        }
    }
}
