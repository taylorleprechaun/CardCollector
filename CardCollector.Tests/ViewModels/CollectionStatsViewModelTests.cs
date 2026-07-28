using CardCollector.ViewModels;
using System.Text.Json;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class CollectionStatsViewModelTests
    {
        [TestMethod]
        public void SetCountsJson_MoreThanTwentySets_TruncatesToFirstTwenty()
        {
            var breakdown = Enumerable.Range(1, 25).Select(i => (Label: $"Set{i}", Count: i)).ToList();
            var stats = new CollectionStatsViewModel { SetBreakdown = breakdown };

            var counts = JsonSerializer.Deserialize<int[]>(stats.SetCountsJson);

            Assert.AreEqual(20, counts!.Length);
            Assert.AreEqual(20, counts[19]);
        }

        [TestMethod]
        public void SetLabelsJson_MoreThanTwentySets_TruncatesToFirstTwenty()
        {
            var breakdown = Enumerable.Range(1, 25).Select(i => (Label: $"Set{i}", Count: i)).ToList();
            var stats = new CollectionStatsViewModel { SetBreakdown = breakdown };

            var labels = JsonSerializer.Deserialize<string[]>(stats.SetLabelsJson);

            Assert.AreEqual(20, labels!.Length);
            Assert.AreEqual("Set1", labels[0]);
            Assert.AreEqual("Set20", labels[19]);
        }
        [TestMethod]
        public void SetValueLabelsJson_SerializesAllEntriesWithoutTruncation()
        {
            var breakdown = new List<(string Label, decimal Value)> { ("Set A", 10.5m), ("Set B", 20.25m) };
            var stats = new CollectionStatsViewModel { SetValueBreakdown = breakdown };

            var labels = JsonSerializer.Deserialize<string[]>(stats.SetValueLabelsJson);

            CollectionAssert.AreEqual(new[] { "Set A", "Set B" }, labels);
        }

        [TestMethod]
        public void ValueHistoryDatesJson_SerializesSnapshotDates()
        {
            var stats = new CollectionStatsViewModel
            {
                ValueHistory = [new ValueSnapshotPoint("2026-01-01", 100m, 5)]
            };

            var dates = JsonSerializer.Deserialize<string[]>(stats.ValueHistoryDatesJson);

            CollectionAssert.AreEqual(new[] { "2026-01-01" }, dates);
        }

        [TestMethod]
        public void WishlistValueHistoryCountsJson_SerializesCounts()
        {
            var stats = new CollectionStatsViewModel
            {
                WishlistValueHistory = [new ValueSnapshotPoint("2026-01-01", 50m, 7)]
            };

            var counts = JsonSerializer.Deserialize<int[]>(stats.WishlistValueHistoryCountsJson);

            CollectionAssert.AreEqual(new[] { 7 }, counts);
        }

        [TestMethod]
        public void WishlistValueHistoryDatesJson_SerializesSnapshotDates()
        {
            var stats = new CollectionStatsViewModel
            {
                WishlistValueHistory = [new ValueSnapshotPoint("2026-01-01", 50m, 7)]
            };

            var dates = JsonSerializer.Deserialize<string[]>(stats.WishlistValueHistoryDatesJson);

            CollectionAssert.AreEqual(new[] { "2026-01-01" }, dates);
        }

        [TestMethod]
        public void WishlistValueHistoryValuesJson_SerializesTotalValues()
        {
            var stats = new CollectionStatsViewModel
            {
                WishlistValueHistory = [new ValueSnapshotPoint("2026-01-01", 50m, 7)]
            };

            var values = JsonSerializer.Deserialize<decimal[]>(stats.WishlistValueHistoryValuesJson);

            CollectionAssert.AreEqual(new[] { 50m }, values);
        }
    }
}
