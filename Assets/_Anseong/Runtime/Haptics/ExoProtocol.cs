using System.Globalization;

namespace Anseong.Haptics
{
    /// <summary>ESP32가 10Hz로 보내는 상태(02_architecture "외골격 시리얼 프로토콜"). 기록용.</summary>
    public struct ExoStatus
    {
        /// <summary>IDLE·PRE·RESIST·RELEASE·SAFE.</summary>
        public string State;
        public int Output;
        public int Error;
    }

    /// <summary>
    /// 외골격 시리얼 명령 글자(순수). 115200bps, 한 줄 ASCII, 줄 끝 \n.
    /// PC → ESP32: P,&lt;ms&gt; 준비 / R,&lt;세기 0-255&gt;,&lt;지속ms&gt; 저항 / X 즉시 해제 / H 살아 있음(100ms마다).
    /// ESP32 → PC: S,&lt;상태&gt;,&lt;출력&gt;,&lt;오류코드&gt;.
    /// </summary>
    public static class ExoProtocol
    {
        public const int BaudRate = 115200;
        /// <summary>펌웨어가 저항 지속을 이 값으로 자른다. PC 쪽도 같은 값으로 미리 자른다.</summary>
        public const int MaxResistMs = 500;
        /// <summary>준비 명령 최대 대기. 보스 예고(최대 2.0초) + 여유.</summary>
        public const int MaxPrepareMs = 3000;
        public const int MaxStrength = 255;

        public const string Release = "X";
        public const string Heartbeat = "H";

        public static string Prepare(int ms)
        {
            return "P," + Clamp(ms, 1, MaxPrepareMs).ToString(CultureInfo.InvariantCulture);
        }

        public static string Resist(int strength, int ms)
        {
            var c = CultureInfo.InvariantCulture;
            return "R," + Clamp(strength, 0, MaxStrength).ToString(c) + "," + Clamp(ms, 1, MaxResistMs).ToString(c);
        }

        /// <summary>"S,RESIST,180,0" 같은 줄을 읽는다. 앞뒤 공백·\r은 무시. 형식이 다르면 false.</summary>
        public static bool TryParseStatus(string line, out ExoStatus status)
        {
            status = default;
            if (string.IsNullOrEmpty(line)) return false;
            var parts = line.Trim().Split(',');
            if (parts.Length != 4 || parts[0] != "S" || parts[1].Length == 0) return false;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var output)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var error)) return false;
            status = new ExoStatus { State = parts[1], Output = output, Error = error };
            return true;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
