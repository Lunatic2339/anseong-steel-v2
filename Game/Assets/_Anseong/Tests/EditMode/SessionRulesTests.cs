using Anseong.Net;
using Anseong.Pilot;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class SessionRulesTests
    {
        const ulong Server = 0;

        [Test]
        public void Host_ApprovesOneRemotePilot_RejectsThird()
        {
            var v = SessionRules.ProtocolVersion;
            Assert.IsTrue(SessionRules.Approve(SessionMode.Host, true, 0, -1).Approved, "호스트 자신");
            Assert.IsTrue(SessionRules.Approve(SessionMode.Host, false, 0, v).Approved, "PC-B");
            var third = SessionRules.Approve(SessionMode.Host, false, 1, v);
            Assert.IsFalse(third.Approved, "3번째");
            StringAssert.Contains("2명", third.Reason);
        }

        [Test]
        public void Solo_RejectsAnyRemote()
        {
            Assert.IsTrue(SessionRules.Approve(SessionMode.SoloBothArms, true, 0, -1).Approved);
            Assert.IsFalse(SessionRules.Approve(SessionMode.SoloBothArms, false, 0, SessionRules.ProtocolVersion).Approved);
        }

        [Test]
        public void DifferentBuild_IsRejected()
        {
            var r = SessionRules.Approve(SessionMode.Host, false, 0, SessionRules.ProtocolVersion + 1);
            Assert.IsFalse(r.Approved);
            Assert.IsFalse(SessionRules.Approve(SessionMode.Host, false, 0, -1).Approved, "약속 번호 없음");
        }

        [Test]
        public void Payload_RoundTrips()
        {
            Assert.AreEqual(SessionRules.ProtocolVersion, SessionRules.ReadPayload(SessionRules.ConnectionPayload()));
            Assert.AreEqual(-1, SessionRules.ReadPayload(null));
            Assert.AreEqual(-1, SessionRules.ReadPayload(new byte[2]));
        }

        [Test]
        public void Owners_SoloBothServer_HostLeftOnly()
        {
            var solo = SessionRules.InitialOwners(SessionMode.SoloBothArms, Server);
            Assert.IsTrue(SessionRules.IsOwner(solo, PilotSide.Left, Server));
            Assert.IsTrue(SessionRules.IsOwner(solo, PilotSide.Right, Server));

            var host = SessionRules.InitialOwners(SessionMode.Host, Server);
            Assert.IsTrue(SessionRules.IsOwner(host, PilotSide.Left, Server));
            Assert.IsFalse(SessionRules.IsOwner(host, PilotSide.Right, Server), "오른팔은 PC-B 것");
        }

        [Test]
        public void Joiner_GetsRightArm_LeaverFreesIt()
        {
            var owners = SessionRules.InitialOwners(SessionMode.Host, Server);
            Assert.IsTrue(SessionRules.AssignJoiner(owners, 5));
            Assert.IsTrue(SessionRules.IsOwner(owners, PilotSide.Right, 5));
            Assert.IsFalse(SessionRules.IsOwner(owners, PilotSide.Left, 5), "PC-B는 왼팔 보고 못 함");
            Assert.IsFalse(SessionRules.AssignJoiner(owners, 6), "오른팔이 이미 있음");

            Assert.IsTrue(SessionRules.ReleaseLeaver(owners, 5));
            Assert.IsFalse(SessionRules.IsOwner(owners, PilotSide.Right, 5));
            Assert.IsFalse(SessionRules.IsOwner(owners, PilotSide.Right, SessionRules.NoOwner), "빈 팔은 누구 것도 아님");
            Assert.IsTrue(SessionRules.AssignJoiner(owners, 7), "다시 접속");
            Assert.IsTrue(SessionRules.IsOwner(owners, PilotSide.Right, 7));
        }

        [Test]
        public void DrivesArm_ByMode()
        {
            Assert.IsTrue(SessionRules.DrivesArm(SessionMode.SoloBothArms, PilotSide.Left));
            Assert.IsTrue(SessionRules.DrivesArm(SessionMode.SoloBothArms, PilotSide.Right));
            Assert.IsTrue(SessionRules.DrivesArm(SessionMode.Host, PilotSide.Left));
            Assert.IsFalse(SessionRules.DrivesArm(SessionMode.Host, PilotSide.Right));
            Assert.IsFalse(SessionRules.DrivesArm(SessionMode.Client, PilotSide.Left));
            Assert.AreEqual(PilotSide.Right, SessionRules.ArmSide(SessionMode.Client));
            Assert.AreEqual(PilotSide.Left, SessionRules.ArmSide(SessionMode.Host));
        }

        [TestCase("192.168.0.10", "192.168.0.10")]
        [TestCase("  10.0.0.2 ", "10.0.0.2")]
        [TestCase("127.0.0.1", "127.0.0.1")]
        public void Address_Accepts(string text, string expected)
        {
            Assert.IsTrue(SessionRules.TryParseAddress(text, out var address));
            Assert.AreEqual(expected, address);
        }

        [TestCase("")]
        [TestCase("192.168.0")]
        [TestCase("192.168.0.256")]
        [TestCase("192.168.0.1.5")]
        [TestCase("abc.def.ghi.jkl")]
        [TestCase("192.168.-1.1")]
        [TestCase("pc-a")]
        public void Address_Rejects(string text)
        {
            Assert.IsFalse(SessionRules.TryParseAddress(text, out _));
        }

        [Test]
        public void ModeFromArgs()
        {
            Assert.IsTrue(SessionRules.TryModeFromArgs(new[] { "game.exe", "-mode", "client", "-ip", "192.168.0.10" }, out var m));
            Assert.AreEqual(SessionMode.Client, m);
            Assert.AreEqual("192.168.0.10", SessionRules.ArgValue(new[] { "game.exe", "-mode", "client", "-ip", "192.168.0.10" }, "-ip"));
            Assert.IsTrue(SessionRules.TryModeFromArgs(new[] { "-MODE", "Host" }, out m));
            Assert.AreEqual(SessionMode.Host, m);
            Assert.IsFalse(SessionRules.TryModeFromArgs(new[] { "-mode", "server" }, out _));
            Assert.IsFalse(SessionRules.TryModeFromArgs(new[] { "game.exe" }, out _));
        }

        [Test]
        public void SceneFingerprint_SameSet_AnyOrder_Matches_DifferentRejected()
        {
            var a = SessionRules.SceneFingerprint(new uint[] { 3883444392u, 1288582861u });
            var b = SessionRules.SceneFingerprint(new uint[] { 1288582861u, 3883444392u });
            var c = SessionRules.SceneFingerprint(new uint[] { 3872961797u, 1288582861u });
            Assert.AreEqual(a, b, "순서 무관");
            Assert.AreNotEqual(a, c);
            var v = SessionRules.ProtocolVersion;
            Assert.IsTrue(SessionRules.Approve(SessionMode.Host, false, 0, v, a, b).Approved);
            var r = SessionRules.Approve(SessionMode.Host, false, 0, v, c, a);
            Assert.IsFalse(r.Approved);
            StringAssert.Contains("씬이 다릅니다", r.Reason);
            Assert.IsTrue(SessionRules.Approve(SessionMode.Host, true, 0, -1, 0, a).Approved, "호스트 자신");
        }

        [Test]
        public void Payload_CarriesSceneFingerprint()
        {
            var p = SessionRules.ConnectionPayload(0xDEADBEEFu);
            Assert.AreEqual(SessionRules.ProtocolVersion, SessionRules.ReadPayload(p));
            Assert.AreEqual(0xDEADBEEFu, SessionRules.ReadSceneFingerprint(p));
            Assert.AreEqual(0u, SessionRules.ReadSceneFingerprint(new byte[4]));
        }

        [Test]
        public void Beacon_RoundTrips_AndIgnoresOthers()
        {
            Assert.IsTrue(SessionRules.TryReadBeacon(SessionRules.BeaconPayload(7777), out var v, out var p));
            Assert.AreEqual(SessionRules.ProtocolVersion, v);
            Assert.AreEqual(7777, p);
            Assert.IsFalse(SessionRules.TryReadBeacon(System.Text.Encoding.ASCII.GetBytes("MINECRAFT|1|7777"), out _, out _));
            Assert.IsFalse(SessionRules.TryReadBeacon(null, out _, out _));
            Assert.AreEqual("192.168.0.255", SessionRules.SubnetBroadcast("192.168.0.5"));
            Assert.IsNull(SessionRules.SubnetBroadcast("abc"));
        }
    }
}
