using CardCollector.DTO;
using CardCollector.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class PurchasePriorityAnalyzerTests
    {
        private static readonly DateTime AsOfUtc = new(2026, 7, 20);

        [TestMethod]
        public void Evaluate_CardIsNull_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                PurchasePriorityAnalyzer.Evaluate(null!, "LOB-EN001", "Secret Rare", _ => "2015-01-01", AsOfUtc));
        }

        [TestMethod]
        public void Evaluate_DebutDateWithinMinDebutAgeYears_ReturnsNull()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Secret Rare", _ => "2026-01-01", AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_FoilPoolExceedsMaxFoilCount_ReturnsNull()
        {
            var card = MakeCard(
                ("LOB-EN001", "Secret Rare"),
                ("LOB-EN002", "Ultra Rare"),
                ("LOB-EN003", "Ghost Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Secret Rare", _ => "2015-01-01", AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_FoilPoolIsEmpty_ReturnsNull()
        {
            var card = MakeCard(("LOB-EN001", "Common"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Common", _ => "2015-01-01", AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_NoSetHasAResolvableDate_ReturnsNull()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(card, "LOB-EN001", "Secret Rare", _ => null, AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_OnlySecondaryFoilPoolPresent_ReturnsCandidateViaFallback()
        {
            var card = MakeCard(("LOB-EN001", "Normal Parallel Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Normal Parallel Rare", _ => "2015-01-01", AsOfUtc);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result!.FoilCount);
        }

        [TestMethod]
        public void Evaluate_PreferredEntryRarityIsNotFoil_ReturnsNull()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"), ("LOB-EN001", "Common"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Common", _ => "2015-01-01", AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_PreferredPrintingNotYetStale_ReturnsNull()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Secret Rare", code => code == "LOB-EN001" ? "2024-01-01" : null, AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_PreferredRarityNameIsNull_MatchesAnyRarityForThatSetCode()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", null, _ => "2015-01-01", AsOfUtc);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Evaluate_PreferredSetCodeHasNoResolvableDate_ReturnsNull()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "DOES-NOT-EXIST", "Secret Rare", _ => "2015-01-01", AsOfUtc);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Evaluate_PreferredSetCodeIsNull_ThrowsArgumentNullException()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            Assert.ThrowsExactly<ArgumentNullException>(() =>
                PurchasePriorityAnalyzer.Evaluate(card, null!, "Secret Rare", _ => "2015-01-01", AsOfUtc));
        }

        [TestMethod]
        public void Evaluate_PrimaryFoilPoolPresent_ReturnsCandidate()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));
            card.ID = 42;
            card.Name = "Blue-Eyes White Dragon";

            var result = PurchasePriorityAnalyzer.Evaluate(
                card, "LOB-EN001", "Secret Rare", _ => "2015-01-01", AsOfUtc);

            Assert.IsNotNull(result);
            Assert.AreEqual(42, result!.CardID);
            Assert.AreEqual("Blue-Eyes White Dragon", result.CardName);
            Assert.AreEqual("2015-01-01", result.DebutDate);
            Assert.AreEqual("2015-01-01", result.PrintingDate);
            Assert.AreEqual(1, result.FoilCount);
        }

        [TestMethod]
        public void Evaluate_ResolveTcgDateIsNull_ThrowsArgumentNullException()
        {
            var card = MakeCard(("LOB-EN001", "Secret Rare"));

            Assert.ThrowsExactly<ArgumentNullException>(() =>
                PurchasePriorityAnalyzer.Evaluate(card, "LOB-EN001", "Secret Rare", null!, AsOfUtc));
        }
        private static Card MakeCard(params (string Code, string RarityName)[] sets) => new()
        {
            ID = 1,
            Name = "Test Card",
            CardSets = sets.Select(s => new Set { Code = s.Code, RarityName = s.RarityName }).ToList()
        };
    }
}
