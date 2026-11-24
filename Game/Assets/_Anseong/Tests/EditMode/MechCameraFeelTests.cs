using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>P1 카메라의 걸음·충격 반응과, 메카의 출발·정지 가속.</summary>
    public class MechCameraFeelTests
    {
        const float Dt = 1f / 90f;

        static SkirmishSession Running(out SkirmishSettings s)
        {
            s = SkirmishSettings.Scene1();
            var session = new SkirmishSession(s);
            session.SetReady(true, true);
            return session;
        }

        [Test]
        public void 출발하면_속도가_서서히_올라_정해진_시간에_전속력이_된다()
        {
            var session = Running(out var s);
            session.SetMech(new MechInput { move = Vector3.right });
            session.Tick(0.02f);
            Assert.Less(session.MechVelocity.magnitude, s.mechSpeed * 0.1f, "첫 틱에 전속력이 되면 안 된다");
            for (int i = 0; i < 9; i++) session.Tick(0.02f);
            Assert.AreEqual(s.mechSpeed * 0.5f, session.MechVelocity.magnitude, 0.01f, "0.2초에 절반");
            for (int i = 0; i < 10; i++) session.Tick(0.02f);
            Assert.AreEqual(s.mechSpeed, session.MechVelocity.magnitude, 0.01f, "0.4초에 전속력");
        }

        [Test]
        public void 스틱을_놓으면_정해진_시간에_멈춘다()
        {
            var session = Running(out var s);
            session.SetMech(new MechInput { move = Vector3.right });
            for (int i = 0; i < 30; i++) session.Tick(0.02f);
            session.SetMech(default);
            session.Tick(0.02f);
            Assert.Greater(session.MechVelocity.magnitude, s.mechSpeed * 0.8f, "바로 멈추면 안 된다");
            for (int i = 0; i < 14; i++) session.Tick(0.02f);
            Assert.AreEqual(0f, session.MechVelocity.magnitude, 0.01f, "0.3초에 정지");
        }

        [Test]
        public void 벽에_닿으면_그_방향_속도를_버린다()
        {
            var session = Running(out var s);
            session.SetMech(new MechInput { move = Vector3.right });
            for (int i = 0; i < 300; i++) session.Tick(0.02f);
            Assert.AreEqual(s.mechArea.z, session.State.mech.x, 0.001f);
            Assert.AreEqual(0f, session.MechVelocity.x);
        }

        [Test]
        public void 시간을_0으로_두면_예전처럼_바로_움직인다()
        {
            var session = Running(out var s);
            s.mechAccelSeconds = s.mechStopSeconds = 0f;
            session.SetMech(new MechInput { move = Vector3.forward });
            session.Tick(0.02f);
            Assert.AreEqual(s.mechSpeed, session.MechVelocity.magnitude, 0.001f);
        }

        [Test]
        public void 서_있으면_카메라는_움직이지_않는다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings();
            for (int i = 0; i < 200; i++) feel.Tick(new Vector3(3f, 0f, 5f), Dt, s);
            Assert.AreEqual(Vector3.zero, feel.Offset);
            Assert.IsFalse(feel.Stepped);
        }

        [Test]
        public void 달리면_위아래로만_정해진_깊이_안에서_움직이고_걸음마다_한_번_알린다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings();
            var p = Vector3.zero;
            int steps = 0;
            float deepest = 0f;
            for (int i = 0; i < 90 * 9; i++)     // 10m/s로 9초 = 90m = 10걸음
            {
                p += Vector3.forward * (10f * Dt);
                feel.Tick(p, Dt, s);
                if (feel.Stepped) steps++;
                Assert.AreEqual(0f, feel.Offset.x, 1e-6f);
                Assert.AreEqual(0f, feel.Offset.z, 1e-6f);
                Assert.LessOrEqual(feel.Offset.y, 0f);
                deepest = Mathf.Min(deepest, feel.Offset.y);
            }
            Assert.AreEqual(10, steps, 1);
            Assert.AreEqual(-s.stepDip, deepest, 0.002f);
        }

        [Test]
        public void 멈추면_걸음_반응이_사라진다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings();
            var p = Vector3.zero;
            for (int i = 0; i < 180; i++) { p += Vector3.forward * (10f * Dt); feel.Tick(p, Dt, s); }
            for (int i = 0; i < 90; i++) feel.Tick(p, Dt, s);
            Assert.AreEqual(0f, feel.Offset.magnitude, 1e-5f);
        }

        [Test]
        public void 맞으면_맞은_방향으로_짧게_밀렸다가_돌아온다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings();
            feel.Tick(Vector3.zero, Dt, s);
            feel.Impact(new Vector3(0f, -3f, -1f), s.hitPush);
            float farthest = 0f, farthestAt = 0f;
            for (int i = 1; i <= 90; i++)
            {
                feel.Tick(Vector3.zero, Dt, s);
                Assert.AreEqual(0f, feel.Offset.y, 1e-6f, "충격은 수평으로만");
                Assert.LessOrEqual(feel.Offset.z, 0f);
                if (-feel.Offset.z > farthest) { farthest = -feel.Offset.z; farthestAt = i * Dt; }
            }
            Assert.AreEqual(s.hitPush, farthest, 0.01f);
            Assert.Less(farthestAt, 0.08f, "가장 많이 밀리는 때는 맞은 직후");
            Assert.AreEqual(0f, feel.Offset.magnitude, 1e-4f, "1초 뒤에는 제자리");
        }

        [Test]
        public void 걸음과_충격을_합쳐도_정해진_거리를_넘지_않는다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings { hitPush = 0.2f, stepDip = 0.08f, maxOffset = 0.1f };
            var p = Vector3.zero;
            for (int i = 0; i < 300; i++)
            {
                if (i % 20 == 0) feel.Impact(Vector3.back, s.hitPush);
                p += Vector3.forward * (10f * Dt);
                feel.Tick(p, Dt, s);
                Assert.LessOrEqual(feel.Offset.magnitude, s.maxOffset + 1e-5f);
            }
        }

        [Test]
        public void 재시작으로_순간_이동해도_걸음으로_치지_않는다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings();
            feel.Tick(Vector3.zero, Dt, s);
            feel.Tick(new Vector3(0f, 0f, 60f), Dt, s);
            Assert.IsFalse(feel.Stepped);
            Assert.AreEqual(0f, feel.Offset.magnitude, 1e-6f);
        }

        [Test]
        public void 끄면_아무것도_하지_않는다()
        {
            var feel = new MechCameraFeel();
            var s = new CameraFeelSettings { steps = false, impacts = false };
            var p = Vector3.zero;
            feel.Impact(Vector3.back, 0.1f);
            for (int i = 0; i < 200; i++) { p += Vector3.forward * (10f * Dt); feel.Tick(p, Dt, s); Assert.AreEqual(Vector3.zero, feel.Offset); }
        }
    }
}
