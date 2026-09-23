using CardCollector.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class BanlistCollectionTests
    {
        [TestMethod]
        public void Build_DuplicateEffectiveDateAfterOverride_KeepsListWithLaterSourceDate()
        {
            var list1 = BuildList(new DateOnly(2024, 1, 1));
            var list2 = BuildList(new DateOnly(2024, 3, 1), konamiID: 1, limit: BanlistLimit.Limited);
            var list3 = BuildList(new DateOnly(2024, 5, 1), konamiID: 1, limit: BanlistLimit.Forbidden);
            var list4 = BuildList(new DateOnly(2024, 7, 1));
            var overrides = new Dictionary<string, string>
            {
                ["2024-03-01"] = "2024-04-01",
                ["2024-05-01"] = "2024-04-01"
            };

            var collection = BanlistCollection.Build([list1, list2, list3, list4], current: null, overrides);

            var merged = collection.GetByDate(new DateOnly(2024, 4, 1));
            Assert.IsNotNull(merged);
            Assert.AreEqual(BanlistLimit.Forbidden, merged!.GetLimit(1));
        }

        [TestMethod]
        public void Build_NoOverridesConfigured_UsesSourceDates()
        {
            var lists = new List<Banlist> { BuildList(new DateOnly(2024, 4, 22)) };

            var collection = BanlistCollection.Build(lists, current: null, effectiveDateOverrides: null);

            Assert.IsNotNull(collection.GetByDate(new DateOnly(2024, 4, 22)));
        }

        [TestMethod]
        public void Build_NullSourceDatedLists_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => BanlistCollection.Build(null!, current: null, effectiveDateOverrides: null));
        }

        [TestMethod]
        public void Build_OverrideDateInvalid_IgnoredWithSourceDateKept()
        {
            var lists = new List<Banlist> { BuildList(new DateOnly(2024, 4, 22)) };
            var overrides = new Dictionary<string, string> { ["2024-04-22"] = "not-a-date" };
            var logger = new Mock<ILogger>();

            var collection = BanlistCollection.Build(lists, current: null, overrides, logger.Object);

            Assert.IsNotNull(collection.GetByDate(new DateOnly(2024, 4, 22)));
            logger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
        }

        [TestMethod]
        public void Build_OverrideDateOutOfOrder_IgnoredWithSourceDateKept()
        {
            var lists = new List<Banlist>
            {
                BuildList(new DateOnly(2024, 1, 1)),
                BuildList(new DateOnly(2024, 4, 22)),
                BuildList(new DateOnly(2024, 9, 2))
            };
            // 2023-12-01 is before the previous list's date (2024-01-01), so it's out of order.
            var overrides = new Dictionary<string, string> { ["2024-04-22"] = "2023-12-01" };

            var collection = BanlistCollection.Build(lists, current: null, overrides, new Mock<ILogger>().Object);

            Assert.IsNotNull(collection.GetByDate(new DateOnly(2024, 4, 22)));
        }

        [TestMethod]
        public void Build_OverrideDateValid_UsesOverrideAsEffectiveDate()
        {
            var lists = new List<Banlist>
            {
                BuildList(new DateOnly(2024, 1, 1)),
                BuildList(new DateOnly(2024, 4, 22)),
                BuildList(new DateOnly(2024, 9, 2))
            };
            var overrides = new Dictionary<string, string> { ["2024-04-22"] = "2024-04-15" };

            var collection = BanlistCollection.Build(lists, current: null, overrides);

            Assert.IsNotNull(collection.GetByDate(new DateOnly(2024, 4, 15)));
            Assert.IsNull(collection.GetByDate(new DateOnly(2024, 4, 22)));
        }

        [TestMethod]
        public void Build_OverrideOnFirstList_HasNoPreviousNeighbourToViolate()
        {
            var lists = new List<Banlist>
            {
                BuildList(new DateOnly(2024, 4, 22)),
                BuildList(new DateOnly(2024, 9, 2))
            };
            var overrides = new Dictionary<string, string> { ["2024-04-22"] = "2024-04-15" };

            var collection = BanlistCollection.Build(lists, current: null, overrides);

            Assert.IsNotNull(collection.GetByDate(new DateOnly(2024, 4, 15)));
        }

        [TestMethod]
        public void Build_OverrideOnLastList_HasNoNextNeighbourToViolate()
        {
            var lists = new List<Banlist>
            {
                BuildList(new DateOnly(2024, 1, 1)),
                BuildList(new DateOnly(2024, 4, 22))
            };
            var overrides = new Dictionary<string, string> { ["2024-04-22"] = "2024-04-15" };

            var collection = BanlistCollection.Build(lists, current: null, overrides);

            Assert.IsNotNull(collection.GetByDate(new DateOnly(2024, 4, 15)));
        }

        [TestMethod]
        public void Dates_MultipleLists_NewestFirst()
        {
            var lists = new List<Banlist>
            {
                BuildList(new DateOnly(2024, 1, 1)),
                BuildList(new DateOnly(2024, 6, 1)),
                BuildList(new DateOnly(2024, 3, 1))
            };

            var collection = BanlistCollection.Build(lists, current: null, effectiveDateOverrides: null);

            CollectionAssert.AreEqual(
                new[] { new DateOnly(2024, 6, 1), new DateOnly(2024, 3, 1), new DateOnly(2024, 1, 1) },
                collection.Dates.ToArray());
        }

        [TestMethod]
        public void GetByDate_ExactMatch_ReturnsList()
        {
            var list = BuildList(new DateOnly(2024, 1, 1));
            var collection = BanlistCollection.Build([list], current: null, effectiveDateOverrides: null);

            var result = collection.GetByDate(new DateOnly(2024, 1, 1));

            Assert.AreSame(list, result);
        }

        [TestMethod]
        public void GetByDate_NoMatch_ReturnsNull()
        {
            var collection = BanlistCollection.Build([BuildList(new DateOnly(2024, 1, 1))], current: null, effectiveDateOverrides: null);

            var result = collection.GetByDate(new DateOnly(2024, 2, 1));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetForDate_AfterLastList_ReturnsLatestList()
        {
            var earlier = BuildList(new DateOnly(2024, 1, 1));
            var later = BuildList(new DateOnly(2024, 6, 1));
            var collection = BanlistCollection.Build([earlier, later], current: null, effectiveDateOverrides: null);

            var result = collection.GetForDate(new DateOnly(2024, 12, 1));

            Assert.AreSame(later, result);
        }

        [TestMethod]
        public void GetForDate_BeforeFirstList_ReturnsNull()
        {
            var collection = BanlistCollection.Build([BuildList(new DateOnly(2024, 6, 1))], current: null, effectiveDateOverrides: null);

            var result = collection.GetForDate(new DateOnly(2024, 1, 1));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetForDate_BetweenLists_ReturnsEarlierList()
        {
            var earlier = BuildList(new DateOnly(2024, 1, 1));
            var later = BuildList(new DateOnly(2024, 6, 1));
            var collection = BanlistCollection.Build([earlier, later], current: null, effectiveDateOverrides: null);

            var result = collection.GetForDate(new DateOnly(2024, 3, 1));

            Assert.AreSame(earlier, result);
        }

        [TestMethod]
        public void GetForDate_SingleList_ReturnsThatListOnOrAfterItsDate()
        {
            var only = BuildList(new DateOnly(2024, 1, 1));
            var collection = BanlistCollection.Build([only], current: null, effectiveDateOverrides: null);

            var result = collection.GetForDate(new DateOnly(2024, 1, 1));

            Assert.AreSame(only, result);
        }

        private static Banlist BuildList(DateOnly date, int? konamiID = null, BanlistLimit limit = BanlistLimit.Limited) =>
            new()
            {
                EffectiveDate = date,
                LimitsByKonamiID = konamiID is { } id
                    ? new Dictionary<int, BanlistLimit> { [id] = limit }
                    : new Dictionary<int, BanlistLimit>()
            };
    }
}
