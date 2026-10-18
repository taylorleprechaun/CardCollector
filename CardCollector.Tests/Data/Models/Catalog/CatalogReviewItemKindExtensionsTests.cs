using CardCollector.Data.Models.Catalog;

namespace CardCollector.Tests.Data.Models.Catalog
{
    [TestClass]
    public sealed class CatalogReviewItemKindExtensionsTests
    {
        [TestMethod]
        [DataRow(CatalogReviewItemKind.LegacyDismissalUnmapped, DisplayName = "Legacy dismissal")]
        [DataRow(CatalogReviewItemKind.LegacyRowAutoMapped, DisplayName = "Auto-mapped row")]
        [DataRow(CatalogReviewItemKind.LegacyRowUnmapped, DisplayName = "Unmapped row")]
        public void IsActionable_LegacyKind_ReturnsFalse(CatalogReviewItemKind kind)
        {
            var result = kind.IsActionable();

            Assert.IsFalse(result);
        }

        [TestMethod]
        [DataRow(CatalogReviewItemKind.LegacyDismissalUnmapped, DisplayName = "Legacy dismissal")]
        [DataRow(CatalogReviewItemKind.LegacyRowAutoMapped, DisplayName = "Auto-mapped row")]
        [DataRow(CatalogReviewItemKind.LegacyRowUnmapped, DisplayName = "Unmapped row")]
        public void IsLegacy_LegacyKind_ReturnsTrue(CatalogReviewItemKind kind)
        {
            var result = kind.IsLegacy();

            Assert.IsTrue(result);
        }

        [TestMethod]
        [DataRow(CatalogReviewItemKind.CardAdded, DisplayName = "Informational sync kind")]
        [DataRow(CatalogReviewItemKind.UnmappedTCGProduct, DisplayName = "Actionable sync kind")]
        public void IsLegacy_SyncKind_ReturnsFalse(CatalogReviewItemKind kind)
        {
            var result = kind.IsLegacy();

            Assert.IsFalse(result);
        }
    }
}
