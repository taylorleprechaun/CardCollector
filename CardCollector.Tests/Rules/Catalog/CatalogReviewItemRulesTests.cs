using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Rules.Catalog;
using Newtonsoft.Json;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogReviewItemRulesTests
    {
        private static readonly IReadOnlySet<CatalogSource> AllSucceeded =
            new HashSet<CatalogSource> { CatalogSource.TCGPlayer, CatalogSource.YamlYugi, CatalogSource.YGOProDeck };

        [TestMethod]
        public void Reconcile_ActionableItemNoLongerDetected_ResolvesBySource()
        {
            var stored = Stored(CatalogReviewItemKind.PasscodeChanged, "card:1", new { Current = 1, Reported = 2 });

            var result = CatalogReviewItemRules.Reconcile([stored], [], AllSucceeded, 2, NowUTC);

            Assert.AreEqual(CatalogReviewItemStatus.ResolvedBySource, stored.Status);
            Assert.AreEqual(1, result.AutoResolved);
        }

        [TestMethod]
        public void Reconcile_ClosedItemSamePayload_StaysClosed()
        {
            var payload = new { Current = 1, Reported = 2 };
            var stored = Stored(CatalogReviewItemKind.PasscodeChanged, "card:1", payload, CatalogReviewItemStatus.Rejected);

            var result = CatalogReviewItemRules.Reconcile([stored], [Detected(CatalogReviewItemKind.PasscodeChanged, "card:1", payload)], AllSucceeded, 2, NowUTC);

            Assert.AreEqual(CatalogReviewItemStatus.Rejected, stored.Status);
            Assert.AreEqual(2, stored.DetectionCount);
            Assert.AreEqual(0, result.ActionableOpened);
        }

        [TestMethod]
        public void Reconcile_ClosedItemWithNewPayload_Reopens()
        {
            var stored = Stored(CatalogReviewItemKind.PasscodeChanged, "card:1", new { Current = 1, Reported = 2 }, CatalogReviewItemStatus.Rejected);

            var result = CatalogReviewItemRules.Reconcile(
                [stored], [Detected(CatalogReviewItemKind.PasscodeChanged, "card:1", new { Current = 1, Reported = 3 })], AllSucceeded, 2, NowUTC);

            Assert.AreEqual(CatalogReviewItemStatus.Open, stored.Status);
            Assert.AreEqual(1, result.ActionableOpened);
        }

        [TestMethod]
        public void Reconcile_InfoItemNoLongerDetected_StaysOpen()
        {
            var stored = Stored(CatalogReviewItemKind.CardAdded, "card:1", new { Name = "Synthetic" });

            CatalogReviewItemRules.Reconcile([stored], [], AllSucceeded, 2, NowUTC);

            Assert.AreEqual(CatalogReviewItemStatus.Open, stored.Status);
        }

        [TestMethod]
        public void Reconcile_NewDetection_CreatesOpenItem()
        {
            var result = CatalogReviewItemRules.Reconcile([], [Detected(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", new { ProductID = 500 })], AllSucceeded, 7, NowUTC);

            var item = result.NewItems.Single();
            Assert.AreEqual(CatalogReviewItemStatus.Open, item.Status);
            Assert.AreEqual("tcg:500", item.SubjectKey);
            Assert.AreEqual(7, item.LastSyncRunID);
            Assert.AreEqual(1, result.OpenedByKind[CatalogReviewItemKind.UnmappedTCGProduct]);
        }

        [TestMethod]
        public void Reconcile_OpenItemDetectedAgain_AccumulatesInsteadOfDuplicating()
        {
            var payload = new { ProductID = 500 };
            var stored = Stored(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", payload);

            var result = CatalogReviewItemRules.Reconcile([stored], [Detected(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", payload)], AllSucceeded, 2, NowUTC);

            Assert.AreEqual(0, result.NewItems.Count);
            Assert.AreEqual(2, stored.DetectionCount);
            Assert.AreEqual(NowUTC, stored.LastDetectedUTC);
            Assert.AreEqual(0, result.ActionableOpened);
        }

        [TestMethod]
        public void Reconcile_RequiredSourceFailed_DoesNotAutoResolve()
        {
            var stored = Stored(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", new { ProductID = 500 });

            CatalogReviewItemRules.Reconcile([stored], [], new HashSet<CatalogSource> { CatalogSource.YamlYugi }, 2, NowUTC);

            Assert.AreEqual(CatalogReviewItemStatus.Open, stored.Status);
        }

        [TestMethod]
        public void Reconcile_ResolvedBySourceItemDetectedAgain_Reopens()
        {
            var payload = new { ProductID = 500 };
            var stored = Stored(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", payload, CatalogReviewItemStatus.ResolvedBySource);

            CatalogReviewItemRules.Reconcile([stored], [Detected(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", payload)], AllSucceeded, 2, NowUTC);

            Assert.AreEqual(CatalogReviewItemStatus.Open, stored.Status);
            Assert.IsNull(stored.ResolvedUTC);
        }

        [TestMethod]
        public void Reconcile_SameSubjectTwiceInOneRun_KeepsLastReport()
        {
            var result = CatalogReviewItemRules.Reconcile(
                [],
                [
                    Detected(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", new { Reason = "first" }),
                    Detected(CatalogReviewItemKind.UnmappedTCGProduct, "tcg:500", new { Reason = "second" })
                ],
                AllSucceeded,
                2,
                NowUTC);

            StringAssert.Contains(result.NewItems.Single().PayloadJson, "second");
        }

        private static DetectedReviewItem Detected(CatalogReviewItemKind kind, string subject, object payload) =>
            new() { ExternalSubject = subject, Kind = kind, Payload = payload, Summary = "Synthetic summary" };

        private static CatalogReviewItem Stored(
            CatalogReviewItemKind kind,
            string subject,
            object payload,
            CatalogReviewItemStatus status = CatalogReviewItemStatus.Open)
        {
            var json = JsonConvert.SerializeObject(payload);
            return new CatalogReviewItem
            {
                DetectionCount = 1,
                FirstDetectedUTC = EarlierUTC,
                Kind = kind,
                LastDetectedUTC = EarlierUTC,
                PayloadHash = CatalogReviewItemRules.ComputeHash(json),
                PayloadJson = json,
                ResolvedUTC = status == CatalogReviewItemStatus.Open ? null : EarlierUTC,
                Status = status,
                SubjectKey = subject
            };
        }
    }
}
