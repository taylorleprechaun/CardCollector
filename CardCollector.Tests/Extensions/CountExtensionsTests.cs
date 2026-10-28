using CardCollector.Extensions;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class CountExtensionsTests
    {
        [TestMethod]
        [DataRow(0, "0 cards", DisplayName = "Zero")]
        [DataRow(1, "1 card", DisplayName = "One")]
        [DataRow(3, "3 cards", DisplayName = "Several")]
        public void ToCountLabel_Count_PluralizesUnlessOne(int count, string expected)
        {
            var result = count.ToCountLabel("card");

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToCountLabel_NullNoun_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => 1.ToCountLabel(null!));
        }
    }
}
