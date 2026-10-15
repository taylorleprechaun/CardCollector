using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CollectionRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_ThenGetByIDAsync_RoundTripsEntry()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 2 };

            await repository.AddAsync(entry);
            var result = await repository.GetByIDAsync(entry.ID);

            Assert.IsNotNull(result);
            Assert.AreEqual(101, result!.CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetCompletionStatusByCardIDsAsync_NoTrackedPrintingOwned_ReturnsPlaceholder()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 102 });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionCompletionStatus.Placeholder, result[1]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByCardIDsAsync_TrackedQuantityAtThreshold_ReturnsComplete()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 101 });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result[1]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByCardIDsAsync_TrackedQuantityBelowThreshold_ReturnsIncomplete()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 1 });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 101 });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionCompletionStatus.Incomplete, result[1]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByCardIDsAsync_TwoTrackedPrintingsForSameCard_RollsUpToCompleteIfEitherIsMet()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 1 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Owned, Quantity = 1 });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 101, DesiredQuantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 102, DesiredQuantity = 1 });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionCompletionStatus.Complete, result[1]);
        }

        [TestMethod]
        public async Task GetCompletionStatusByCardIDsAsync_VariantOfTrackedPrinting_DoesNotCountTowardIt()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Owned, Quantity = 3 });
            context.PreferredVersions.Add(new PreferredVersion { CatalogPrintingID = 101 });
            await context.SaveChangesAsync();

            var result = await repository.GetCompletionStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionCompletionStatus.Placeholder, result[1]);
        }

        [TestMethod]
        public async Task GetOrderedQuantitiesAsync_SumsOrderedQuantityPerPrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered, Quantity = 2 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered, Quantity = 1 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Ordered, Quantity = 4 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, Quantity = 5 });

            var result = await repository.GetOrderedQuantitiesAsync();

            Assert.AreEqual(3, result[101]);
            Assert.AreEqual(4, result[102]);
            Assert.IsFalse(result.ContainsKey(201));
        }

        [TestMethod]
        public async Task GetOwnedCardPrintingsAsync_ExcludesOrderedEntriesAndReturnsDistinctPrintings()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Ordered });

            var result = await repository.GetOwnedCardPrintingsAsync();

            Assert.AreEqual((1, 101), result.Single());
        }

        [TestMethod]
        public async Task GetOwnedPrintingIDsAsync_ExcludesOrderedEntries()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Ordered });

            var result = await repository.GetOwnedPrintingIDsAsync();

            CollectionAssert.AreEquivalent(new[] { 101 }, result.ToArray());
        }

        [TestMethod]
        public async Task GetOwnedQuantitiesByPrintingAsync_SumsOwnedEntriesOfEachRequestedPrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 2 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 1 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered, Quantity = 5 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Owned, Quantity = 4 });

            var result = await repository.GetOwnedQuantitiesByPrintingAsync([101]);

            Assert.AreEqual(3, result[101]);
            Assert.IsFalse(result.ContainsKey(102));
        }

        [TestMethod]
        public async Task GetQuantitiesByPrintingAsync_EmptyCardIDs_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.GetQuantitiesByPrintingAsync([], CollectionStatus.Owned);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetQuantitiesByPrintingAsync_SumsRequestedCardsEntriesWithTheStatusPerPrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 2 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 1 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Ordered, Quantity = 4 });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, Quantity = 5 });

            var result = await repository.GetQuantitiesByPrintingAsync([1], CollectionStatus.Owned);

            Assert.AreEqual(3, result[101]);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingEntry_UpdatesMutableFieldsAndKeepsPrinting()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 1 };
            await repository.AddAsync(entry);

            var result = await repository.UpdateAsync(new CollectionEntry { ID = entry.ID, Quantity = 5 });

            Assert.IsTrue(result);
            var updated = await repository.GetByIDAsync(entry.ID);
            Assert.AreEqual(5, updated!.Quantity);
            Assert.AreEqual(101, updated.CatalogPrintingID);
        }

        [TestMethod]
        public async Task DeleteAsync_ExistingEntry_RemovesItAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned };
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
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned });

            var result = (await repository.GetByCardIDAsync(1)).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(101, result[0].CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetByStatusAsync_OrdersByDateCreatedDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, DateCreated = new DateTime(2020, 1, 1) });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, DateCreated = new DateTime(2026, 1, 1) });

            var result = (await repository.GetByStatusAsync(CollectionStatus.Owned)).ToList();

            Assert.AreEqual(201, result[0].CatalogPrintingID);
            Assert.AreEqual(101, result[1].CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetCardIDsByStatusAsync_ReturnsDistinctCardIDsForStatus()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Owned });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Ordered });

            var result = await repository.GetCardIDsByStatusAsync(CollectionStatus.Owned);

            CollectionAssert.AreEquivalent(new[] { 1 }, result.ToArray());
        }

        [TestMethod]
        public async Task GetCompletionStatusByCardIDsAsync_EmptyCardIDs_ReturnsEmpty()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CollectionRepository(context);

            var result = await repository.GetCompletionStatusByCardIDsAsync([]);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetDistinctAcquisitionMethodsAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2), (301, 3), (401, 4));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, AcquisitionMethod = AcquisitionMethod.Traded });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, AcquisitionMethod = AcquisitionMethod.Purchased });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 301, Status = CollectionStatus.Owned, AcquisitionMethod = null });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 401, Status = CollectionStatus.Ordered, AcquisitionMethod = AcquisitionMethod.Pulled });

            var result = await repository.GetDistinctAcquisitionMethodsAsync();

            CollectionAssert.AreEqual(new[] { AcquisitionMethod.Purchased, AcquisitionMethod.Traded }, result.ToArray());
        }

        [TestMethod]
        public async Task GetDistinctConditionsAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2), (301, 3), (401, 4));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Condition = CardCondition.NearMint });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, Condition = CardCondition.Damaged });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 301, Status = CollectionStatus.Owned, Condition = null });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 401, Status = CollectionStatus.Ordered, Condition = CardCondition.LightlyPlayed });

            var result = await repository.GetDistinctConditionsAsync();

            CollectionAssert.AreEqual(new[] { CardCondition.Damaged, CardCondition.NearMint }, result.ToArray());
        }

        [TestMethod]
        public async Task GetDistinctEditionsAsync_OwnedOnlyExcludesNullAndOrdersValues()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2), (301, 3), (401, 4));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Edition = CardEdition.Unlimited });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, Edition = CardEdition.FirstEdition });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 301, Status = CollectionStatus.Owned, Edition = null });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 401, Status = CollectionStatus.Ordered, Edition = CardEdition.LimitedEdition });

            var result = await repository.GetDistinctEditionsAsync();

            CollectionAssert.AreEqual(new[] { CardEdition.FirstEdition, CardEdition.Unlimited }, result.ToArray());
        }

        [TestMethod]
        public async Task GetOwnedStatsAsync_MixOfEntriesWithAndWithoutPrices_SumsOnlyPricedOnes()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (201, 2));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 2, PurchasePrice = 5m });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 201, Status = CollectionStatus.Owned, Quantity = 1, PurchasePrice = null });

            var stats = await repository.GetOwnedStatsAsync();

            Assert.AreEqual(3, stats.TotalQuantity);
            Assert.AreEqual(10m, stats.TotalSpent);
        }

        [TestMethod]
        public async Task GetOwnedStatsAsync_NoEntriesHavePurchasePrice_TotalSpentIsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Owned, Quantity = 1, PurchasePrice = null });

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
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered });

            var result = await repository.GetStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionStatus.Ordered, result[1]);
        }

        [TestMethod]
        public async Task GetStatusByCardIDsAsync_OwnedAndOrderedEntries_OwnedTakesPriority()
        {
            using var context = InMemoryDbContextFactory.Create();
            CatalogTestData.SeedPrintings(context, (101, 1), (102, 1));
            var repository = new CollectionRepository(context);
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered });
            await repository.AddAsync(new CollectionEntry { CatalogPrintingID = 102, Status = CollectionStatus.Owned });

            var result = await repository.GetStatusByCardIDsAsync([1]);

            Assert.AreEqual(CollectionStatus.Owned, result[1]);
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
            CatalogTestData.SeedPrintings(context, (101, 1));
            var repository = new CollectionRepository(context);
            var entry = new CollectionEntry { CatalogPrintingID = 101, Status = CollectionStatus.Ordered, Quantity = 1 };
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
