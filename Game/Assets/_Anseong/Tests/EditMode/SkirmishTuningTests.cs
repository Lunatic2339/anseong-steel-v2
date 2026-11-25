using System.Linq;
using Anseong.Core;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    /// <summary>교전 시험 장면의 값과 헤드셋 확인 항목이 조절 메뉴에 올라가는지.</summary>
    public class SkirmishTuningTests
    {
        GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        TuningList Collect(out SkirmishSettings settings)
        {
            go = new GameObject("tuning test");
            settings = SkirmishSettings.Scene1();
            var list = new TuningList();
            go.AddComponent<SkirmishLocalInput>().AddTuning(list);
            go.AddComponent<SkirmishMechArms>().AddTuning(list);
            go.AddComponent<MechHoloHud>().AddTuning(list);
            settings.AddTuning(list);
            return list;
        }

        static void Select(TuningList list, string key)
        {
            list.Move(-list.Items.Count);
            for (int i = 0; i < list.Items.Count && list.Current.Key != key; i++) list.Move(1);
            Assert.AreEqual(key, list.Current.Key, "항목이 없음: " + key);
        }

        [Test]
        public void 키가_겹치지_않고_묶음마다_확인_항목이_있다()
        {
            var list = Collect(out _);
            var keys = list.Items.Select(i => i.Key).ToList();
            Assert.AreEqual(keys.Count, keys.Distinct().Count(), "같은 키가 두 번 등록되면 한 항목이 두 값을 같이 바꾼다");
            foreach (var group in list.Items.Select(i => i.Group).Distinct())
                Assert.IsTrue(list.Items.Any(i => i.Group == group && i.IsCheck), group + "에 확인 항목이 없음");
            list.CountChecks(out int total, out int ok, out int problem);
            Assert.GreaterOrEqual(total, 15);
            Assert.AreEqual(0, ok + problem, "처음에는 전부 '아직'");
        }

        [Test]
        public void 메뉴에서_바꾼_규칙_값이_교전_설정에_바로_들어간다()
        {
            var list = Collect(out var settings);
            Select(list, "sk.hitStop");
            Assert.IsTrue(list.Adjust(2));
            Assert.AreEqual(0.08f, settings.grunt.hitStopSeconds, 0.0001f);

            Select(list, "sk.slowSeconds");
            Assert.IsTrue(list.Adjust(-100));
            Assert.AreEqual(0f, settings.perfectSlowSeconds, "0까지 내리면 느려짐이 꺼진다");

            // 바꾼 설정으로 규칙을 돌리면 그대로 적용된다.
            var session = new SkirmishSession(settings);
            Assert.AreEqual(0.08f, session.Settings.grunt.hitStopSeconds, 0.0001f);
        }

        [Test]
        public void 바꾼_값과_확인_결과가_저장되고_다시_읽힌다()
        {
            var list = Collect(out _);
            Select(list, "sk.arm.reach");
            list.Adjust(5);
            Select(list, "check.sk.armFollow");
            list.Adjust(TuningList.CheckProblem);
            string saved = list.Serialize();
            StringAssert.Contains("sk.arm.reach=0.65", saved);
            StringAssert.Contains("check.sk.armFollow=2", saved);
            Object.DestroyImmediate(go);

            var again = Collect(out _);
            Assert.AreEqual(2, again.Apply(saved));
            Select(again, "sk.arm.reach");
            Assert.AreEqual(0.65f, again.Current.Value, 0.0001f);
            again.CountChecks(out _, out _, out int problem);
            Assert.AreEqual(1, problem);
        }
    }
}
