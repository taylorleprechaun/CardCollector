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
        [DataRow("Grand Master Rare", "(GMR)")]
        [DataRow("Starlight Rare", "(StR)")]
        [DataRow("Collector's Rare", "(CR)")]
        [DataRow("Prismatic Collector's Rare", "(PCR)")]
        [DataRow("Prismatic Secret Rare", "(PScR)")]
        [DataRow("Prismatic Ultimate Rare", "(PUR)")]
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
        [DataRow("Duel Terminal Technology Common", "(DTTC)")]
        [DataRow("Duel Terminal Technology Ultra Rare", "(DTTUR)")]
        [DataRow("Emblazoned Secret Rare", "(EmScR)")]
        [DataRow("Emblazoned Ultra Rare", "(EmUR)")]
        [DataRow("Secret Pharaoh’s Rare", "(SCR-PhaR)")]
        [DataRow("Ultra Pharaoh’s Rare", "(UR-PhaR)")]
        [DataRow("Parallel Rare", "(ParR)")]
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
        [DataRow("Common", DisplayName = "Already Common")]
        [DataRow("Ultra Rare", DisplayName = "Unrelated rarity")]
        public void NormalizeRarityName_NotAShortPrintVariant_ReturnsUnchanged(string? rarityName)
        {
            var result = RarityExtensions.NormalizeRarityName(rarityName);

            Assert.AreEqual(rarityName, result);
        }
        
        [TestMethod]
        [DataRow("Short Print", DisplayName = "Short Print")]
        [DataRow("Super Short Print", DisplayName = "Super Short Print")]
        [DataRow("short print", DisplayName = "Case-insensitive match")]
        public void NormalizeRarityName_ShortPrintVariant_ReturnsCommon(string rarityName)
        {
            var result = RarityExtensions.NormalizeRarityName(rarityName);

            Assert.AreEqual("Common", result);
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
        [DataRow("Duel Terminal Technology Common", Rarity.DuelTerminalTechnologyCommon, DisplayName = "Duel Terminal Technology Common")]
        [DataRow("Emblazoned Ultra Rare", Rarity.EmblazonedUltraRare, DisplayName = "Emblazoned Ultra Rare")]
        [DataRow("Secret Pharaoh’s Rare", Rarity.SecretRarePharaohsRare, DisplayName = "tcgcsv curly-apostrophe Secret Pharaoh's Rare alias")]
        [DataRow("Ultra Pharaoh’s Rare", Rarity.UltraRarePharaohsRare, DisplayName = "tcgcsv curly-apostrophe Ultra Pharaoh's Rare alias")]
        [DataRow("Cr", Rarity.CollectorsRare, DisplayName = "Alias sharing a value with its canonical member (regression: BuildMap previously dropped every same-valued alias)")]
        [DataRow("Secret Rare (Pharaoh's Rare)", Rarity.SecretRarePharaohsRare, DisplayName = "Another same-valued alias")]
        [DataRow("Parallel Rare", Rarity.ParallelRare, DisplayName = "Parallel Rare")]
        public void ParseRarity_KnownValue_ReturnsMappedRarity(string value, Rarity expected)
        {
            var result = RarityExtensions.ParseRarity(value);

            Assert.AreEqual(expected, result);
        }
    }
}
