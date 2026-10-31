using Anseong.Combat;
using Anseong.Pilot;
using Unity.Netcode;

namespace Anseong.Net
{
    /// <summary>
    /// 전투 구조체를 네트워크로 보내는 포장(M2-01). 전투 구조체 자체는 네트워크를 모르게 두려고(EditMode 테스트) 여기서만 직렬화한다.
    /// 칸을 더하면 Serialize에도 더한다.
    /// </summary>
    public struct NetHitReport : INetworkSerializable
    {
        public HitReport Value;

        public NetHitReport(HitReport value) => Value = value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var side = (byte)Value.Side;
            var kind = (byte)Value.Kind;
            var quality = (byte)Value.Quality;
            var zone = (byte)Value.Zone;
            s.SerializeValue(ref side);
            s.SerializeValue(ref kind);
            s.SerializeValue(ref quality);
            s.SerializeValue(ref zone);
            s.SerializeValue(ref Value.StrikeId);
            s.SerializeValue(ref Value.ClientTime);
            s.SerializeValue(ref Value.Point);
            if (!s.IsReader) return;
            Value.Side = (PilotSide)side;
            Value.Kind = (PunchKind)kind;
            Value.Quality = (HitQuality)quality;
            Value.Zone = (BossZone)zone;
        }
    }

    public struct NetCombatEvent : INetworkSerializable
    {
        public CombatEvent Value;

        public NetCombatEvent(CombatEvent value) => Value = value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var cue = (int)Value.Cue;
            var side = (byte)Value.Side;
            var kind = (byte)Value.Kind;
            var broken = (byte)Value.Break;
            var quality = (byte)Value.Quality;
            var zone = (byte)Value.Zone;
            s.SerializeValue(ref cue);
            s.SerializeValue(ref side);
            s.SerializeValue(ref kind);
            s.SerializeValue(ref broken);
            s.SerializeValue(ref quality);
            s.SerializeValue(ref zone);
            s.SerializeValue(ref Value.Damage);
            s.SerializeValue(ref Value.ComboStep);
            s.SerializeValue(ref Value.BossHealth);
            s.SerializeValue(ref Value.Point);
            s.SerializeValue(ref Value.Time);
            s.SerializeValue(ref Value.Code);
            s.SerializeValue(ref Value.Amount);
            if (!s.IsReader) return;
            Value.Cue = (CombatCue)cue;
            Value.Side = (PilotSide)side;
            Value.Kind = (PunchKind)kind;
            Value.Break = (ComboBreak)broken;
            Value.Quality = (HitQuality)quality;
            Value.Zone = (BossZone)zone;
        }
    }

    public struct NetCombatSnapshot : INetworkSerializable, System.IEquatable<NetCombatSnapshot>
    {
        public CombatSnapshot Value;

        public NetCombatSnapshot(CombatSnapshot value) => Value = value;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            var next = (byte)Value.ComboNextSide;
            var boostSide = (byte)Value.BoostSide;
            s.SerializeValue(ref Value.BossHealth);
            s.SerializeValue(ref Value.BossMax);
            s.SerializeValue(ref Value.ComboStep);
            s.SerializeValue(ref next);
            s.SerializeValue(ref Value.BoostOpen);
            s.SerializeValue(ref boostSide);
            if (!s.IsReader) return;
            Value.ComboNextSide = (PilotSide)next;
            Value.BoostSide = (PilotSide)boostSide;
        }

        public bool Equals(NetCombatSnapshot other)
        {
            var a = Value;
            var b = other.Value;
            return a.BossHealth == b.BossHealth && a.BossMax == b.BossMax && a.ComboStep == b.ComboStep && a.ComboNextSide == b.ComboNextSide
                   && a.BoostOpen == b.BoostOpen && a.BoostSide == b.BoostSide;
        }

        public override bool Equals(object obj) => obj is NetCombatSnapshot other && Equals(other);
        public override int GetHashCode() => Value.BossHealth.GetHashCode() ^ (Value.ComboStep << 8);
    }
}
