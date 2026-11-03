using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Anseong.Pilot;

namespace Anseong.Net
{
    /// <summary>접속 요청 하나에 대한 답.</summary>
    public struct ApprovalResult
    {
        public bool Approved;
        /// <summary>거절 이유(접속하려던 PC 화면에 뜬다).</summary>
        public string Reason;

        public static ApprovalResult Yes => new ApprovalResult { Approved = true, Reason = string.Empty };
        public static ApprovalResult No(string reason) => new ApprovalResult { Approved = false, Reason = reason };
    }

    /// <summary>
    /// 접속 규칙(M3-01, 순수). CLAUDE.md: PC-A = 호스트 + 왼쪽 조종사, PC-B = 클라이언트 + 오른쪽 조종사, 직결 7777, 2명까지만 승인.
    /// 1인 양팔은 한 PC가 두 팔을 맡고 다른 PC를 받지 않는다.
    /// </summary>
    public static class SessionRules
    {
        /// <summary>호스트 포함 최대 조종사 수.</summary>
        public const int MaxPilots = 2;
        /// <summary>접속 요청에 실어 보내는 약속 번호. 네트워크로 주고받는 형식이 바뀌면 올린다(다른 빌드끼리 붙지 않게).</summary>
        public const int ProtocolVersion = 1;
        /// <summary>팔 주인이 아무도 아님(오른쪽 조종사가 아직 안 들어옴·나감).</summary>
        public const ulong NoOwner = ulong.MaxValue;

        /// <summary>이 PC가 직접 움직이는 팔. 1인 양팔은 두 팔.</summary>
        public static bool DrivesArm(SessionMode mode, PilotSide side)
        {
            switch (mode)
            {
                case SessionMode.Host: return side == PilotSide.Left;
                case SessionMode.Client: return side == PilotSide.Right;
                default: return true;
            }
        }

        /// <summary>담당 팔(1인 양팔이면 의미 없음, 왼쪽을 돌려줌).</summary>
        public static PilotSide ArmSide(SessionMode mode) => mode == SessionMode.Client ? PilotSide.Right : PilotSide.Left;

        /// <summary>
        /// 서버가 받는 접속 요청 판정. <paramref name="remotePilots"/> = 이미 승인한 다른 PC 수(호스트 자신 제외).
        /// <paramref name="theirVersion"/>는 접속 요청에 실린 약속 번호(못 읽으면 -1).
        /// </summary>
        public static ApprovalResult Approve(SessionMode mode, bool isSelf, int remotePilots, int theirVersion)
        {
            if (isSelf) return ApprovalResult.Yes;
            if (mode != SessionMode.Host) return ApprovalResult.No("상대 PC가 1인 양팔 모드로 실행 중입니다. 상대 PC를 '호스트'로 시작하세요.");
            if (theirVersion != ProtocolVersion)
                return ApprovalResult.No($"두 PC의 게임 빌드가 다릅니다(약속 번호 {theirVersion} ≠ {ProtocolVersion}). 같은 빌드로 맞추세요.");
            if (remotePilots >= MaxPilots - 1) return ApprovalResult.No("이미 조종사 2명이 접속해 있습니다.");
            return ApprovalResult.Yes;
        }

        /// <summary>새로 들어온 PC의 담당 팔. 오른쪽이 비어 있으면 오른쪽, 아니면 없음.</summary>
        public static bool AssignJoiner(ulong[] owners, ulong clientId)
        {
            if (owners == null || owners.Length < 2) return false;
            if (owners[(int)PilotSide.Right] != NoOwner) return false;
            owners[(int)PilotSide.Right] = clientId;
            return true;
        }

        /// <summary>나간 PC가 맡던 팔을 비운다. 비운 팔이 있으면 true.</summary>
        public static bool ReleaseLeaver(ulong[] owners, ulong clientId)
        {
            var changed = false;
            for (var i = 0; owners != null && i < owners.Length; i++)
            {
                if (owners[i] != clientId) continue;
                owners[i] = NoOwner;
                changed = true;
            }
            return changed;
        }

        /// <summary>세션 시작 때 팔 주인. 1인 양팔은 두 팔 모두 호스트, 호스트 모드는 왼팔만 호스트(오른팔은 접속을 기다림).</summary>
        public static ulong[] InitialOwners(SessionMode mode, ulong serverId)
        {
            return mode == SessionMode.Host ? new[] { serverId, NoOwner } : new[] { serverId, serverId };
        }

        /// <summary>보고를 보낸 PC가 그 팔의 주인인지(남의 팔 명중·그립 보고는 버린다).</summary>
        public static bool IsOwner(ulong[] owners, PilotSide side, ulong sender)
        {
            return owners != null && (int)side < owners.Length && owners[(int)side] != NoOwner && owners[(int)side] == sender;
        }

        /// <summary>운영자가 친 IPv4 주소를 읽는다(앞뒤 공백 무시). "192.168.0.10" 형식만.</summary>
        public static bool TryParseAddress(string text, out string address)
        {
            address = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var trimmed = text.Trim();
            var parts = trimmed.Split('.');
            if (parts.Length != 4) return false;
            foreach (var part in parts)
            {
                if (part.Length == 0 || part.Length > 3) return false;
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n > 255) return false;
            }
            if (!IPAddress.TryParse(trimmed, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            address = ip.ToString();
            return true;
        }

        /// <summary>실행 인자 "-mode solo|host|client". 없거나 모르는 값이면 false.</summary>
        public static bool TryModeFromArgs(string[] args, out SessionMode mode)
        {
            mode = SessionMode.SoloBothArms;
            var value = ArgValue(args, "-mode");
            if (value == null) return false;
            switch (value.Trim().ToLowerInvariant())
            {
                case "solo": mode = SessionMode.SoloBothArms; return true;
                case "host": mode = SessionMode.Host; return true;
                case "client": mode = SessionMode.Client; return true;
                default: return false;
            }
        }

        /// <summary>"-ip 192.168.0.10"처럼 키 다음 값. 없으면 null.</summary>
        public static string ArgValue(string[] args, string key)
        {
            if (args == null) return null;
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        /// <summary>접속 요청에 싣는 바이트(약속 번호 4바이트).</summary>
        public static byte[] ConnectionPayload() => BitConverter.GetBytes(ProtocolVersion);

        public static int ReadPayload(byte[] payload)
        {
            return payload != null && payload.Length >= 4 ? BitConverter.ToInt32(payload, 0) : -1;
        }

        public static string ModeLabel(SessionMode mode)
        {
            switch (mode)
            {
                case SessionMode.Host: return "호스트(PC-A, 왼팔)";
                case SessionMode.Client: return "클라이언트(PC-B, 오른팔)";
                default: return "1인 양팔";
            }
        }
    }
}
