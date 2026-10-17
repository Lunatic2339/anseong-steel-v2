using Anseong.Core;
using NUnit.Framework;

namespace Anseong.Tests
{
    public class MaterialSlotNamesTests
    {
        static readonly string[] Slots = { "ArmorMain", "ArmorAccent", "Metal", "Dark", "Glow", "Trim" };

        [TestCase("ArmorMain", 0)]
        [TestCase("armormain", 0)]
        [TestCase("Glow", 4)]
        [TestCase("Trim.001", 5)]
        [TestCase("Dark.12", 3)]
        [TestCase(" Metal ", 2)]
        public void IndexOf_MatchesSlot(string material, int expected)
        {
            Assert.AreEqual(expected, MaterialSlotNames.IndexOf(material, Slots));
        }

        [TestCase("Material_087")]
        [TestCase("ArmorMainOld")]
        [TestCase("Metal.v2")]
        [TestCase("")]
        [TestCase(null)]
        public void IndexOf_NoMatch_IsMinusOne(string material)
        {
            Assert.AreEqual(-1, MaterialSlotNames.IndexOf(material, Slots));
        }

        [Test]
        public void IndexOf_EmptyOrNullSlotsNeverMatch()
        {
            Assert.AreEqual(-1, MaterialSlotNames.IndexOf("Glow", new string[] { null, "" }));
            Assert.AreEqual(-1, MaterialSlotNames.IndexOf("Glow", null));
        }

        [TestCase("Trim.001", "Trim")]
        [TestCase("Trim.", "Trim.")]
        [TestCase(".001", ".001")]
        [TestCase("Robot.v026", "Robot.v026")]
        public void BaseName_StripsOnlyNumericSuffix(string material, string expected)
        {
            Assert.AreEqual(expected, MaterialSlotNames.BaseName(material));
        }
    }
}
