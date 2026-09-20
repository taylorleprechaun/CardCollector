using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class FormatRepositoryTests
    {
        [TestMethod]
        public async Task AddAsync_FormatWithStrategies_PersistsStrategiesInOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);

            var id = await repository.AddAsync(BuildFormat("Alpha Era", new DateOnly(2024, 1, 1), null, "First", "Second", "Third"));

            var saved = await repository.GetAsync(id);
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, saved!.Strategies.Select(s => s.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, saved.Strategies.Select(s => s.Position).ToArray());
        }

        [TestMethod]
        public async Task AddAsync_NewFormat_SetsAuditDates()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);

            var id = await repository.AddAsync(BuildFormat("Alpha Era", new DateOnly(2024, 1, 1), null));

            var saved = await repository.GetAsync(id);
            Assert.AreNotEqual(default, saved!.DateCreated);
            Assert.AreEqual(saved.DateCreated, saved.DateModified);
        }

        [TestMethod]
        public async Task AddAsync_NullFormat_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.AddAsync(null!));
        }
        [TestMethod]
        public async Task DeleteAsync_FormatExists_RemovesFormatAndStrategies()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = connection;
            using var __ = context;
            var repository = new FormatRepository(context);
            var id = await repository.AddAsync(BuildFormat("Alpha Era", new DateOnly(2024, 1, 1), null, "First", "Second"));

            var deleted = await repository.DeleteAsync(id);

            Assert.IsTrue(deleted);
            Assert.AreEqual(0, await context.Formats.CountAsync());
            Assert.AreEqual(0, await context.FormatStrategies.CountAsync());
        }

        [TestMethod]
        public async Task DeleteAsync_FormatMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);

            var deleted = await repository.DeleteAsync(99);

            Assert.IsFalse(deleted);
        }

        [TestMethod]
        public async Task GetAllAsync_MultipleFormats_OrdersNewestStartDateFirst()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);
            await repository.AddAsync(BuildFormat("Oldest", new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31)));
            await repository.AddAsync(BuildFormat("Newest", new DateOnly(2022, 1, 1), null));
            await repository.AddAsync(BuildFormat("Middle", new DateOnly(2021, 1, 1), new DateOnly(2021, 12, 31)));

            var formats = await repository.GetAllAsync();

            CollectionAssert.AreEqual(new[] { "Newest", "Middle", "Oldest" }, formats.Select(f => f.Name).ToArray());
        }

        [TestMethod]
        public async Task GetAllAsync_StrategiesSavedOutOfPositionOrder_ReturnsThemByPosition()
        {
            using var context = InMemoryDbContextFactory.Create();
            context.Formats.Add(new Format
            {
                Name = "Alpha Era",
                StartDate = new DateOnly(2024, 1, 1),
                Strategies =
                [
                    new FormatStrategy { Name = "Third", Position = 2 },
                    new FormatStrategy { Name = "First", Position = 0 },
                    new FormatStrategy { Name = "Second", Position = 1 }
                ]
            });
            await context.SaveChangesAsync();
            var repository = new FormatRepository(context);

            var formats = await repository.GetAllAsync();

            CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, formats[0].Strategies.Select(s => s.Name).ToArray());
        }

        [TestMethod]
        public async Task GetAsync_FormatMissing_ReturnsNull()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);

            var result = await repository.GetAsync(99);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task UpdateAsync_FormatExists_AdvancesDateModifiedOnly()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);
            var id = await repository.AddAsync(BuildFormat("Alpha Era", new DateOnly(2024, 1, 1), null));
            var before = await repository.GetAsync(id);
            await Task.Delay(10);
            var edited = BuildFormat("Alpha Era 2", new DateOnly(2024, 1, 1), null);
            edited.ID = id;

            await repository.UpdateAsync(edited);

            var after = await repository.GetAsync(id);
            Assert.AreEqual(before!.DateCreated, after!.DateCreated);
            Assert.IsTrue(after.DateModified > before.DateModified);
        }

        [TestMethod]
        public async Task UpdateAsync_FormatExists_ReplacesFieldsAndStrategies()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);
            var id = await repository.AddAsync(BuildFormat("Alpha Era", new DateOnly(2024, 1, 1), null, "Old One", "Old Two"));
            var edited = BuildFormat("Beta Era", new DateOnly(2024, 2, 1), new DateOnly(2024, 6, 1), "New One");
            edited.ID = id;
            edited.Notes = "Edited";

            var updated = await repository.UpdateAsync(edited);

            var saved = await repository.GetAsync(id);
            Assert.IsTrue(updated);
            Assert.AreEqual("Beta Era", saved!.Name);
            Assert.AreEqual(new DateOnly(2024, 2, 1), saved.StartDate);
            Assert.AreEqual(new DateOnly(2024, 6, 1), saved.EndDate);
            Assert.AreEqual("Edited", saved.Notes);
            CollectionAssert.AreEqual(new[] { "New One" }, saved.Strategies.Select(s => s.Name).ToArray());
            Assert.AreEqual(1, await context.FormatStrategies.CountAsync());
        }

        [TestMethod]
        public async Task UpdateAsync_FormatMissing_ReturnsFalse()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);
            var missing = BuildFormat("Ghost", new DateOnly(2024, 1, 1), null);
            missing.ID = 99;

            var updated = await repository.UpdateAsync(missing);

            Assert.IsFalse(updated);
        }

        [TestMethod]
        public async Task UpdateAsync_NullFormat_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var repository = new FormatRepository(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repository.UpdateAsync(null!));
        }
        private static Format BuildFormat(string name, DateOnly start, DateOnly? end, params string[] strategies) =>
            new()
            {
                EndDate = end,
                Name = name,
                StartDate = start,
                Strategies = strategies.Select(s => new FormatStrategy { Name = s }).ToList()
            };
    }
}
