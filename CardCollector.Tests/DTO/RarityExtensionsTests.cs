using CardCollector.DTO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.DTO
{
    [TestClass]
    public sealed class RarityExtensionsTests
    {
        [TestMethod]
        [DataRow("Common", "(C)")]
        [DataRow("Rare", "(R)")]
        [DataRow("Super Rare", "(SR)")]
        [DataRow("Ultra Rare", "(UR)")]
        [DataRow("Secret Rare", "(ScR)")]
        [DataRow("Ultimate Rare", "(UtR)")]
        [DataRow("Gold Rare", "(GUR)")]
        [DataRow("Ghost Rare", "(GHR)")]
        [DataRow("Ghost/Gold Rare", "(GGR)")]
        [DataRow("Starlight Rare", "(StR)")]
        [DataRow("Collector's Rare", "(CR)")]
        [DataRow("Prismatic Secret Rare", "(PScR)")]
        [DataRow("Quarter Century Secret Rare", "(QCSCR)")]
        [DataRow("Platinum Secret Rare", "(PlScR)")]
        [DataRow("Platinum Rare", "(PR)")]
        [DataRow("Short Print", "(SP)")]
        [DataRow("Super Short Print", "(SSP)")]
        [DataRow("Normal Parallel Rare", "(NPR)")]
        [DataRow("Super Parallel Rare", "(SPR)")]
        [DataRow("Ultra Parallel Rare", "(UPR)")]
        [DataRow("10000 Secret Rare", "(10000ScR)")]
        [DataRow("Extra Secret Rare", "(EScR)")]
        [DataRow("Gold Secret Rare", "(GScR)")]
        [DataRow("Mosaic Rare", "(MSR)")]
        [DataRow("Premium Gold Rare", "(PGR)")]
        [DataRow("Shatterfoil Rare", "(SHR)")]
        [DataRow("Starfoil Rare", "(SFR)")]
        [DataRow("Ultra Secret Rare", "(UScR)")]
        [DataRow("Secret Rare Pharaoh's Rare", "(SCR-PhaR)")]
        [DataRow("Ultra Rare Pharaoh's Rare", "(UR-PhaR)")]
        [DataRow("Duel Terminal Normal Parallel Rare", "(DTNPR)")]
        [DataRow("Duel Terminal Normal Rare Parallel Rare", "(DTNRPR)")]
        [DataRow("Duel Terminal Rare Parallel Rare", "(DTRPR)")]
        [DataRow("Duel Terminal Super Parallel Rare", "(DTSPR)")]
        [DataRow("Duel Terminal Ultra Parallel Rare", "(DTUPR)")]
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
