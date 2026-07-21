using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CheckedOutRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_NullEntry_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(null!));
        }

        [TestMethod]
        public async Task AddAsync_ThenGetAsync_RoundTrips()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 2 });

            var result = await repository.GetAsync(10, "LOB-EN001", "Ultra Rare");

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result!.Quantity);
        }

        [TestMethod]
        public async Task GetAllAsync_OrdersByCheckedOutDateDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 1, ImageID = 10, SetCode = "OLD-EN001", RarityName = "A", CheckedOutDate = new DateTime(2020, 1, 1) });
            await repository.AddAsync(new CheckedOutCard { CardID = 2, ImageID = 20, SetCode = "NEW-EN001", RarityName = "B", CheckedOutDate = new DateTime(2026, 1, 1) });

            var result = await repository.GetAllAsync();

            Assert.AreEqual("NEW-EN001", result[0].SetCode);
        }

        [TestMethod]
        public async Task GetCheckedOutLookupAsync_KeyedByImageSetRarity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            var date = new DateTime(2026, 1, 1);
            await repository.AddAsync(new CheckedOutCard { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", CheckedOutDate = date, Quantity = 2 });

            var result = await repository.GetCheckedOutLookupAsync();

            Assert.AreEqual((date, 2), result[(10, "LOB-EN001", "Ultra Rare")]);
        }

        [TestMethod]
        public async Task RemoveAsync_ExistingRecord_RemovesAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare" });

            var result = await repository.RemoveAsync(10, "LOB-EN001", "Ultra Rare");

            Assert.IsTrue(result);
            Assert.IsNull(await repository.GetAsync(10, "LOB-EN001", "Ultra Rare"));
        }

        [TestMethod]
        public async Task RemoveAsync_NoSuchRecord_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);

            var result = await repository.RemoveAsync(999, "LOB-EN001", "Ultra Rare");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingRecord_UpdatesQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 1, ImageID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 1 });

            await repository.UpdateAsync(10, "LOB-EN001", "Ultra Rare", 5);

            var result = await repository.GetAsync(10, "LOB-EN001", "Ultra Rare");
            Assert.AreEqual(5, result!.Quantity);
        }

        [TestMethod]
        public async Task UpdateAsync_NoSuchRecord_DoesNotThrow()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);

            await repository.UpdateAsync(999, "LOB-EN001", "Ultra Rare", 1);
        }
    }
}
