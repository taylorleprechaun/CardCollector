using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class FormatServiceTests
    {
        [TestMethod]
        public async Task AddAsync_CallerSuppliesID_IgnoresItAndInserts()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.AddAsync(Build(42, "Alpha Era", "2024-01-01", null));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, (await service.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task AddAsync_InvalidFormat_ReturnsErrorsAndPersistsNothing()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.AddAsync(Build(0, "   ", "2024-01-01", null));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors.ToArray(), "Name is required.");
            Assert.AreEqual(0, (await service.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task AddAsync_NullFormat_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.AddAsync(null!));
        }

        [TestMethod]
        public async Task AddAsync_OverlapsExistingFormat_ReturnsOverlapError()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            await service.AddAsync(Build(0, "Existing", "2024-01-01", "2024-03-31"));

            var result = await service.AddAsync(Build(0, "Clashing", "2024-03-01", "2024-05-31"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Errors.Single(), "\"Existing\"");
        }

        [TestMethod]
        public async Task AddAsync_ValidFormat_PersistsNormalizedStrategiesInOrder()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.AddAsync(Build(0, "  Alpha Era ", "2024-01-01", null, " First ", "", "first", "Second"));

            var saved = (await service.GetAllAsync()).Single();
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("Alpha Era", saved.Name);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, saved.Strategies.Select(s => s.Name).ToArray());
        }
        [TestMethod]
        public async Task DeleteAsync_ExistingFormat_ReturnsTrue()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            await service.AddAsync(Build(0, "Alpha Era", "2024-01-01", null));
            var id = (await service.GetAllAsync()).Single().ID;

            var deleted = await service.DeleteAsync(id);

            Assert.IsTrue(deleted);
            Assert.AreEqual(0, (await service.GetAllAsync()).Count);
        }

        [TestMethod]
        public async Task UpdateAsync_MissingFormat_ReturnsNotFound()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            var result = await service.UpdateAsync(Build(99, "Ghost", "2024-01-01", null));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Format not found.", result.Errors.Single());
        }

        [TestMethod]
        public async Task UpdateAsync_NullFormat_ThrowsArgumentNullException()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.UpdateAsync(null!));
        }

        [TestMethod]
        public async Task UpdateAsync_OverlapsAnotherFormat_ReturnsErrorAndKeepsOriginal()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            await service.AddAsync(Build(0, "Early", "2024-01-01", "2024-03-31"));
            await service.AddAsync(Build(0, "Late", "2024-04-01", "2024-06-30"));
            var late = (await service.GetAllAsync()).Single(f => f.Name == "Late");

            var result = await service.UpdateAsync(Build(late.ID, "Late", "2024-03-01", "2024-06-30"));

            var stored = (await service.GetAllAsync()).Single(f => f.Name == "Late");
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(new DateOnly(2024, 4, 1), stored.StartDate);
        }

        [TestMethod]
        public async Task UpdateAsync_ValidEditKeepingOwnRange_SucceedsAndReplacesStrategies()
        {
            using var context = InMemoryDbContextFactory.Create();
            var service = CreateService(context);
            await service.AddAsync(Build(0, "Alpha Era", "2024-01-01", "2024-03-31", "Old"));
            var id = (await service.GetAllAsync()).Single().ID;

            var result = await service.UpdateAsync(Build(id, "Alpha Era II", "2024-01-01", "2024-03-31", "New A", "New B"));

            var saved = (await service.GetAllAsync()).Single();
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("Alpha Era II", saved.Name);
            CollectionAssert.AreEqual(new[] { "New A", "New B" }, saved.Strategies.Select(s => s.Name).ToArray());
        }

        private static Format Build(int id, string name, string start, string? end, params string[] strategies) =>
            new()
            {
                EndDate = end is null ? null : DateOnly.Parse(end),
                ID = id,
                Name = name,
                StartDate = DateOnly.Parse(start),
                Strategies = strategies.Select(s => new FormatStrategy { Name = s }).ToList()
            };

        private static FormatService CreateService(CardCollector.Data.AppDBContext context) =>
            new(new FormatRepository(context));
    }
}
