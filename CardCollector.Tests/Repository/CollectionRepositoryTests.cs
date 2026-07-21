using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CollectionRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_ThenGetByIDAsync_RoundTripsEntry()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, Quantity = 2 };

            await repository.AddAsync(entry);
            var result = await repository.GetByIDAsync(entry.ID);

            Assert.IsNotNull(result);
            Assert.AreEqual("LOB-EN001", result!.SetCode);
        }

        [TestMethod]
        public async Task DeleteAsync_ExistingEntry_RemovesItAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned };
            await repository.AddAsync(entry);

            var result = await repository.DeleteAsync(entry.ID);

            Assert.IsTrue(result);
            Assert.IsNull(await repository.GetByIDAsync(entry.ID));
        }

        [TestMethod]
        public async Task DeleteAsync_NoSuchEntry_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.DeleteAsync(999);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetByCardIDAsync_ReturnsOnlyEntriesForThatCard()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned });

            var result = (await repository.GetByCardIDAsync(1)).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(10, result[0].ImageID);
        }

        [TestMethod]
        public async Task GetByStatusAsync_OrdersByDateCreatedDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "OLD-EN001", Status = CollectionStatus.Owned, DateCreated = new DateTime(2020, 1, 1) });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "NEW-EN001", Status = CollectionStatus.Owned, DateCreated = new DateTime(2026, 1, 1) });

            var result = (await repository.GetByStatusAsync(CollectionStatus.Owned)).ToList();

            Assert.AreEqual("NEW-EN001", result[0].SetCode);
            Assert.AreEqual("OLD-EN001", result[1].SetCode);
        }

        [TestMethod]
        public async Task GetCardIDsByStatusAsync_ReturnsDistinctCardIDsForStatus()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 11, SetCode = "LOB-EN002", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN003", Status = CollectionStatus.Ordered });

            var result = await repository.GetCardIDsByStatusAsync(CollectionStatus.Owned);

            CollectionAssert.AreEquivalent(new[] { 1 }, result.ToArray());
        }

        [TestMethod]
        public async Task GetCollectedPairsAsync_IncludesEntriesRegardlessOfStatus()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Ordered });

            var result = await repository.GetCollectedPairsAsync();

            Assert.IsTrue(result.Contains((10, "LOB-EN001")));
            Assert.IsTrue(result.Contains((20, "LOB-EN002")));
        }

        [TestMethod]
        public async Task GetCompletionStatusByImageIDsAsync_CaseInsensitiveSetCodeMatch_StillRollsUp()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "lob-en001", RarityName = "ultra rare", Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByImageIDsAsync([10]);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result[10]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByImageIDsAsync_EmptyImageIDs_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.GetCompletionStatusByImageIDsAsync([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetCompletionStatusByImageIDsAsync_NoPreferredVersionMatch_ReturnsPlaceholder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "DIFFERENT-EN001" });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByImageIDsAsync([10]);

            Assert.AreEqual(CollectionCompletionStatus.Placeholder, result[10]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByImageIDsAsync_PreferredQuantityAtThreshold_ReturnsComplete()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByImageIDsAsync([10]);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result[10]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByImageIDsAsync_PreferredQuantityBelowThreshold_ReturnsIncomplete()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 1 });
            context.PreferredVersions.Add(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByImageIDsAsync([10]);

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, result[10]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByImageIDsAsync_PreferredRarityNameIsNull_MatchesAnyRarity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = null });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByImageIDsAsync([10]);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result[10]);
        }

        [TestMethod]
        public async Task GetDistinctAcquisitionMethodsAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, AcquisitionMethod = AcquisitionMethod.Traded });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned, AcquisitionMethod = AcquisitionMethod.Purchased });
            await repository.AddAsync(new CollectionEntry { CardID = 3, ImageID = 30, SetCode = "LOB-EN003", Status = CollectionStatus.Owned, AcquisitionMethod = null });
            await repository.AddAsync(new CollectionEntry { CardID = 4, ImageID = 40, SetCode = "LOB-EN004", Status = CollectionStatus.Ordered, AcquisitionMethod = AcquisitionMethod.Pulled });

            var result = await repository.GetDistinctAcquisitionMethodsAsync();

            CollectionAssert.AreEqual(new[] { AcquisitionMethod.Purchased, AcquisitionMethod.Traded }, result.ToArray());
        }

        [TestMethod]
        public async Task GetDistinctConditionsAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, Condition = CardCondition.NearMint });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned, Condition = CardCondition.Damaged });
            await repository.AddAsync(new CollectionEntry { CardID = 3, ImageID = 30, SetCode = "LOB-EN003", Status = CollectionStatus.Owned, Condition = null });
            await repository.AddAsync(new CollectionEntry { CardID = 4, ImageID = 40, SetCode = "LOB-EN004", Status = CollectionStatus.Ordered, Condition = CardCondition.LightlyPlayed });

            var result = await repository.GetDistinctConditionsAsync();

            CollectionAssert.AreEqual(new[] { CardCondition.Damaged, CardCondition.NearMint }, result.ToArray());
        }

        [TestMethod]
        public async Task GetDistinctEditionsAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, Edition = CardEdition.Unlimited });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned, Edition = CardEdition.FirstEdition });
            await repository.AddAsync(new CollectionEntry { CardID = 3, ImageID = 30, SetCode = "LOB-EN003", Status = CollectionStatus.Owned, Edition = null });
            await repository.AddAsync(new CollectionEntry { CardID = 4, ImageID = 40, SetCode = "LOB-EN004", Status = CollectionStatus.Ordered, Edition = CardEdition.LimitedEdition });

            var result = await repository.GetDistinctEditionsAsync();

            CollectionAssert.AreEqual(new[] { CardEdition.FirstEdition, CardEdition.Unlimited }, result.ToArray());
        }

        [TestMethod]
        public async Task GetDistinctRarityNamesAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, RarityName = "Ultra Rare" });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned, RarityName = "Common" });
            await repository.AddAsync(new CollectionEntry { CardID = 3, ImageID = 30, SetCode = "LOB-EN003", Status = CollectionStatus.Owned, RarityName = null });
            await repository.AddAsync(new CollectionEntry { CardID = 4, ImageID = 40, SetCode = "LOB-EN004", Status = CollectionStatus.Ordered, RarityName = "Secret Rare" });

            var result = await repository.GetDistinctRarityNamesAsync();

            CollectionAssert.AreEqual(new[] { "Common", "Ultra Rare" }, result.ToArray());
        }

        [TestMethod]
        public async Task GetDistinctSetCodesAsync_OwnedOnlyOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "ZZZ-EN001", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "AAA-EN001", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 3, ImageID = 30, SetCode = "MMM-EN001", Status = CollectionStatus.Ordered });

            var result = await repository.GetDistinctSetCodesAsync();

            CollectionAssert.AreEqual(new[] { "AAA-EN001", "ZZZ-EN001" }, result.ToArray());
        }

        [TestMethod]
        public async Task GetOrderedQuantitiesAsync_SumsQuantityByKey_ExcludesOwnedEntries()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = null, Status = CollectionStatus.Ordered, Quantity = 2 });
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = null, Status = CollectionStatus.Ordered, Quantity = 1 });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned, Quantity = 5 });

            var result = await repository.GetOrderedQuantitiesAsync();

            Assert.AreEqual(3, result[(10, "LOB-EN001", "")]);
            Assert.IsFalse(result.ContainsKey((20, "LOB-EN002", "")));
        }

        [TestMethod]
        public async Task GetOwnedPairsAsync_ExcludesOrderedEntries()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Ordered });

            var result = await repository.GetOwnedPairsAsync();

            Assert.IsTrue(result.Contains((10, "LOB-EN001")));
            Assert.IsFalse(result.Contains((20, "LOB-EN002")));
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesByCardIDsForSetPrefixAsync_EmptyCardIDs_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.GetOwnedQuantitiesByCardIDsForSetPrefixAsync([], "MP25");

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesByCardIDsForSetPrefixAsync_MatchesPrefixBoundary()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "MP25-EN001", Status = CollectionStatus.Owned, Quantity = 2 });
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 11, SetCode = "MP25X-EN001", Status = CollectionStatus.Owned, Quantity = 5 });

            var result = await repository.GetOwnedQuantitiesByCardIDsForSetPrefixAsync([1], "MP25");

            Assert.AreEqual(2, result[1]);
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesForPairsAsync_ExactCaseMatch_ReturnsQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Status = CollectionStatus.Owned, Quantity = 4 });

            var result = await repository.GetOwnedQuantitiesForPairsAsync([(10, "LOB-EN001", "Ultra Rare")]);

            Assert.AreEqual(4, result[(10, "LOB-EN001", "Ultra Rare")]);
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesForPreferredVersionsAsync_CaseInsensitiveMatch_SumsQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "lob-en001", RarityName = "ultra rare", Status = CollectionStatus.Owned, Quantity = 2 });

            var result = await repository.GetOwnedQuantitiesForPreferredVersionsAsync([(10, "LOB-EN001", "Ultra Rare")]);

            Assert.AreEqual(2, result[(10, "LOB-EN001")]);
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesForPreferredVersionsAsync_NoMatchingEntries_OmitsPair()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.GetOwnedQuantitiesForPreferredVersionsAsync([(10, "LOB-EN001", "Ultra Rare")]);

            Assert.IsFalse(result.ContainsKey((10, "LOB-EN001")));
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesForPreferredVersionsAsync_NullRarityNameMatchesAnyRarity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Common", Status = CollectionStatus.Owned, Quantity = 1 });

            var result = await repository.GetOwnedQuantitiesForPreferredVersionsAsync([(10, "LOB-EN001", null)]);

            Assert.AreEqual(1, result[(10, "LOB-EN001")]);
        }

        [TestMethod]
        public async Task GetOwnedStatsAsync_MixOfEntriesWithAndWithoutPrices_SumsOnlyPricedOnes()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, Quantity = 2, PurchasePrice = 5m });
            await repository.AddAsync(new CollectionEntry { CardID = 2, ImageID = 20, SetCode = "LOB-EN002", Status = CollectionStatus.Owned, Quantity = 1, PurchasePrice = null });

            var stats = await repository.GetOwnedStatsAsync();

            Assert.AreEqual(3, stats.TotalQuantity);
            Assert.AreEqual(10m, stats.TotalSpent);
        }

        [TestMethod]
        public async Task GetOwnedStatsAsync_NoEntriesHavePurchasePrice_TotalSpentIsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, Quantity = 1, PurchasePrice = null });

            var stats = await repository.GetOwnedStatsAsync();

            Assert.IsNull(stats.TotalSpent);
        }

        [TestMethod]
        public async Task GetStatusByCardIDsAsync_EmptyCardIDs_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.GetStatusByCardIDsAsync([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetStatusByCardIDsAsync_OnlyOrderedEntries_ReturnsOrdered()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Ordered });

            var result = await repository.GetStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionStatus.Ordered, result[1]);
        }

        [TestMethod]
        public async Task GetStatusByCardIDsAsync_OwnedAndOrderedEntries_OwnedTakesPriority()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Ordered });
            await repository.AddAsync(new CollectionEntry { CardID = 1, ImageID = 11, SetCode = "LOB-EN002", Status = CollectionStatus.Owned });

            var result = await repository.GetStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionStatus.Owned, result[1]);
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingEntry_UpdatesMutableFields()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Owned, Quantity = 1 };
            await repository.AddAsync(entry);

            var result = await repository.UpdateAsync(new CollectionEntry { ID = entry.ID, Quantity = 5, RarityName = "Ultra Rare" });

            Assert.IsTrue(result);
            var updated = await repository.GetByIDAsync(entry.ID);
            Assert.AreEqual(5, updated!.Quantity);
            Assert.AreEqual("Ultra Rare", updated.RarityName);
        }

        [TestMethod]
        public async Task UpdateAsync_NoSuchEntry_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.UpdateAsync(new CollectionEntry { ID = 999 });

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateStatusAsync_ExistingEntryWithQuantity_UpdatesBothFields()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", Status = CollectionStatus.Ordered, Quantity = 1 };
            await repository.AddAsync(entry);

            var result = await repository.UpdateStatusAsync(entry.ID, CollectionStatus.Owned, 3);

            Assert.IsTrue(result);
            var updated = await repository.GetByIDAsync(entry.ID);
            Assert.AreEqual(CollectionStatus.Owned, updated!.Status);
            Assert.AreEqual(3, updated.Quantity);
        }

        [TestMethod]
        public async Task UpdateStatusAsync_NoSuchEntry_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.UpdateStatusAsync(999, CollectionStatus.Owned);

            Assert.IsFalse(result);
        }
    }
}
