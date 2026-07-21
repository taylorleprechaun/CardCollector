using CardCollector.Data.Models;
using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class EditionAuditGroupViewModelTests
    {
        [TestMethod]
        public void From_MixOfCategories_FlaggedCategoryIsTheLowestEnumValue()
        {
            var entries = new List<EditionAuditEntryViewModel>
            {
                MakeEntry(EditionAuditCategory.Unverifiable),
                MakeEntry(EditionAuditCategory.EditionMismatch)
            };

            var result = EditionAuditGroupViewModel.From(new CardPrinting(), entries);

            Assert.AreEqual(EditionAuditCategory.EditionMismatch, result.FlaggedCategory);
        }

        [TestMethod]
        public void From_MixOfFlaggedAndUnflaggedEntries_CountsOnlyFlagged()
        {
            var entries = new List<EditionAuditEntryViewModel>
            {
                MakeEntry(EditionAuditCategory.Unverifiable),
                MakeEntry(null),
                MakeEntry(EditionAuditCategory.EditionMismatch)
            };

            var result = EditionAuditGroupViewModel.From(new CardPrinting(), entries);

            Assert.AreEqual(2, result.FlaggedCount);
        }
        [TestMethod]
        public void From_OnlyUnverifiableEntries_FlaggedCategoryIsUnverifiable()
        {
            var entries = new List<EditionAuditEntryViewModel> { MakeEntry(EditionAuditCategory.Unverifiable) };

            var result = EditionAuditGroupViewModel.From(new CardPrinting(), entries);

            Assert.AreEqual(EditionAuditCategory.Unverifiable, result.FlaggedCategory);
        }

        private static EditionAuditEntryViewModel MakeEntry(EditionAuditCategory? category) =>
            EditionAuditEntryViewModel.From(new OrderEntryViewModel(), category, availableEditions: []);
    }
}
