using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.DTO
{
    [TestClass]
    public sealed class RarityExtensionsTests
    {
        [TestMethod]
        [DataRow("Common", "(C)")]
        [DataRow("Ultra Rare", "(UR)")]
        [DataRow("Quarter Century Secret Rare", "(QCSCR)")]
        public void GetRarityCode_KnownRarityName_ReturnsCode(string rarityName, string expected)
        {
            var result = RarityExtensions.GetRarityCode(rarityName);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null")]
        [DataRow("Not A Real Rarity", DisplayName = "Unmapped string")]
        public void GetRarityCode_UnmappedOrNullValue_ReturnsNull(string? rarityName)
        {
            var result = RarityExtensions.GetRarityCode(rarityName);

            Assert.IsNull(result);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null")]
        [DataRow("", DisplayName = "Empty")]
        [DataRow("   ", DisplayName = "Whitespace")]
        [DataRow("2", DisplayName = "Garbage numeric string")]
        [DataRow("Not A Real Rarity", DisplayName = "Unmapped string")]
        public void ParseRarity_InvalidOrUnmappedValue_ReturnsError(string? value)
        {
            var result = RarityExtensions.ParseRarity(value);

            Assert.AreEqual(Rarity.Error, result);
        }

        [TestMethod]
        [DataRow("Common", Rarity.Common, DisplayName = "Common")]
        [DataRow("Secret Rare", Rarity.SecretRare, DisplayName = "Secret Rare")]
        [DataRow("common", Rarity.Common, DisplayName = "Case-insensitive match")]
        public void ParseRarity_KnownValue_ReturnsMappedRarity(string value, Rarity expected)
        {
            var result = RarityExtensions.ParseRarity(value);

            Assert.AreEqual(expected, result);
        }
    }
}
