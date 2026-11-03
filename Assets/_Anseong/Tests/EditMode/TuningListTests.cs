using Anseong.Core;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class TuningListTests
    {
        sealed class Box { public float A = 0.3f, B = 0.065f; public bool C; public int D; }

        static TuningList Make(Box box, Box second = null)
        {
            var list = new TuningList();
            list.AddFloat("묵직함", "a", "A", () => box.A, v => box.A = v, 0f, 0.6f, 0.05f);
            if (second != null) list.AddFloat("묵직함", "a", "A", () => second.A, v => second.A = v, 0f, 0.6f, 0.05f);
            list.AddFloat("펀치 인식", "b", "B", () => box.B, v => box.B = v, 0.01f, 0.3f, 0.005f, 3);
            list.AddToggle("몸 이동", "c", "C", () => box.C, v => box.C = v);
            list.AddChoice("손목", "d", "D", () => box.D, v => box.D = v, new[] { "항상", "그립" });
            return list;
        }

        [Test]
        public void Adjust_StepsAndClamps()
        {
            var box = new Box();
            var list = Make(box);
            Assert.IsTrue(list.Adjust(1));
            Assert.AreEqual(0.35f, box.A, 1e-5f);
            list.Adjust(100);
            Assert.AreEqual(0.6f, box.A, 1e-5f);
            Assert.IsFalse(list.Adjust(1));
            list.Adjust(-100);
            Assert.AreEqual(0f, box.A, 1e-5f);
        }

        [Test]
        public void SameKey_ChangesBoth()
        {
            Box left = new Box(), right = new Box();
            var list = Make(left, right);
            Assert.AreEqual(4, list.Items.Count);
            list.Adjust(2);
            Assert.AreEqual(0.4f, left.A, 1e-5f);
            Assert.AreEqual(0.4f, right.A, 1e-5f);
        }

        [Test]
        public void ToggleChoiceAndSelection_StopAtEnds()
        {
            var box = new Box();
            var list = Make(box);
            list.Move(2);
            Assert.IsTrue(list.Adjust(1));
            Assert.IsTrue(box.C);
            Assert.IsFalse(list.Adjust(1), "켬에서 더 밀어도 그대로");
            Assert.IsTrue(box.C);
            list.Adjust(-1);
            Assert.IsFalse(box.C);
            list.Move(1);
            Assert.IsFalse(list.Adjust(-1), "첫 선택지에서 왼쪽은 그대로");
            Assert.AreEqual(0, box.D);
            list.Move(1);
            Assert.AreEqual(3, list.Selected, "마지막에서 아래로 가도 그대로");
            list.Move(-10);
            Assert.AreEqual(0, list.Selected, "처음에서 위로 가도 그대로");
        }

        [Test]
        public void OffGridDefault_StepsFromDefault_AndReset()
        {
            var box = new Box();
            var list = Make(box);
            list.Move(1);
            list.Adjust(1);
            Assert.AreEqual(0.07f, box.B, 1e-5f);
            Assert.IsTrue(list.ResetCurrent());
            Assert.AreEqual(0.065f, box.B, 1e-6f);
            Assert.IsFalse(list.ResetCurrent());
        }

        [Test]
        public void SerializeOnlyChanged_ThenApplyRestores()
        {
            var box = new Box();
            var list = Make(box);
            Assert.AreEqual(string.Empty, list.Serialize());
            list.Adjust(3);
            list.Move(2);
            list.Adjust(1);
            var saved = list.Serialize();
            StringAssert.Contains("a=0.45", saved);
            StringAssert.Contains("c=1", saved);
            StringAssert.DoesNotContain("b=", saved);

            var fresh = new Box();
            var restored = Make(fresh);
            Assert.AreEqual(2, restored.Apply(saved + "\nunknown=3\nbroken line\na=abc"));
            Assert.AreEqual(0.45f, fresh.A, 1e-5f);
            Assert.IsTrue(fresh.C);
        }

        [Test]
        public void Apply_ClampsToRange()
        {
            var box = new Box();
            var list = Make(box);
            list.Apply("a=9");
            Assert.AreEqual(0.6f, box.A, 1e-5f);
        }

        [Test]
        public void SortByGroup_FollowsOrder_UnknownLast()
        {
            var list = Make(new Box());
            list.SortByGroup(new[] { "몸 이동", "손목", "펀치 인식" });
            Assert.AreEqual("c", list.Items[0].Key);
            Assert.AreEqual("d", list.Items[1].Key);
            Assert.AreEqual("b", list.Items[2].Key);
            Assert.AreEqual("a", list.Items[3].Key);
        }
    
        [Test]
        public void Checks_CountAndSurviveSaveLoad()
        {
            var list = new TuningList();
            list.Describe("펀치 인식", "할 것: 잽 20번");
            list.Describe("펀치 인식", "나중 안내는 무시");
            var jab = list.AddCheck("펀치 인식", "punch.jab", "잽 18번 이상");
            list.AddCheck("펀치 인식", "punch.hook", "훅 18번 이상");
            list.AddCheck("묵직함", "arm.heavy", "값 정함");
            Assert.AreEqual("할 것: 잽 20번", list.Hint("펀치 인식"));
            Assert.IsNull(list.Hint("묵직함"));
            Assert.IsTrue(jab.IsCheck);
            Assert.AreEqual("아직", jab.ValueText());

            list.Adjust(1); // 잽 → OK
            list.Move(1);
            list.Adjust(2); // 훅 → 문제
            list.CountChecks(out var total, out var ok, out var problem);
            Assert.AreEqual(3, total);
            Assert.AreEqual(1, ok);
            Assert.AreEqual(1, problem);

            var saved = list.Serialize();
            StringAssert.Contains("check.punch.jab=1", saved);
            StringAssert.DoesNotContain("arm.heavy", saved);
            var fresh = new TuningList();
            var restored = fresh.AddCheck("펀치 인식", "punch.jab", "잽 18번 이상");
            fresh.AddCheck("펀치 인식", "punch.hook", "훅 18번 이상");
            Assert.AreEqual(2, fresh.Apply(saved));
            Assert.AreEqual("OK", restored.ValueText());
        }
}
}
