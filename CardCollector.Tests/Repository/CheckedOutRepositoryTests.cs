using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;

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
            await repository.AddAsync(new CheckedOutCard { CardID = 10, CatalogPrintingID = 101, Quantity = 2 });

            var result = await repository.GetAsync(101);

            Assert.AreEqual(2, result!.Quantity);
        }

        [TestMethod]
        public async Task GetAllAsync_OrdersByCheckedOutDateDescending()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 1, CatalogPrintingID = 101, CheckedOutDate = new DateTime(2020, 1, 1) });
            await repository.AddAsync(new CheckedOutCard { CardID = 2, CatalogPrintingID = 201, CheckedOutDate = new DateTime(2026, 1, 1) });

            var result = await repository.GetAllAsync();

            Assert.AreEqual(201, result[0].CatalogPrintingID);
        }

        [TestMethod]
        public async Task GetAsync_OtherPrintingOfSameCard_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 10, CatalogPrintingID = 101, Quantity = 1 });

            var result = await repository.GetAsync(102);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetCheckedOutLookupAsync_KeyedByPrintingID()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            var date = new DateTime(2026, 1, 1);
            await repository.AddAsync(new CheckedOutCard { CardID = 10, CatalogPrintingID = 101, CheckedOutDate = date, Quantity = 2 });

            var result = await repository.GetCheckedOutLookupAsync();

            Assert.AreEqual((date, 2), result[101]);
        }

        [TestMethod]
        public async Task GetCheckedOutLookupAsync_RowWithoutPrintingID_IsLeftOut()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 10, SetCode = "LOB-EN001", RarityName = "Ultra Rare", Quantity = 2 });

            var result = await repository.GetCheckedOutLookupAsync();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task RemoveAsync_ExistingRecord_RemovesAndReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 10, CatalogPrintingID = 101 });

            var result = await repository.RemoveAsync(101);

            Assert.IsTrue(result);
            Assert.IsNull(await repository.GetAsync(101));
        }

        [TestMethod]
        public async Task RemoveAsync_NoSuchRecord_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);

            var result = await repository.RemoveAsync(999);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingRecord_UpdatesQuantity()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);
            await repository.AddAsync(new CheckedOutCard { CardID = 10, CatalogPrintingID = 101, Quantity = 1 });

            await repository.UpdateAsync(101, 5);

            var result = await repository.GetAsync(101);
            Assert.AreEqual(5, result!.Quantity);
        }

        [TestMethod]
        public async Task UpdateAsync_NoSuchRecord_DoesNotThrow()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new CheckedOutRepository(context);

            await repository.UpdateAsync(999, 1);
        }
    }
}
