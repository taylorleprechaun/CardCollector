using CardCollector.Data.Models.Catalog;
using CardCollector.Rules.Catalog;
using CardCollector.ViewModels;
using static CardCollector.Tests.TestHelpers.CatalogTestData;

namespace CardCollector.Tests.Rules.Catalog
{
    [TestClass]
    public sealed class CatalogCardEditorTests
    {
        [TestMethod]
        public void Apply_NameAndPasscodeChanged_LocksBothAndStampsModified()
        {
            var card = Card(1, "Synthetic Theory", passcode: 48739627);

            var changed = CatalogCardEditor.Apply(card, new CatalogCardEditInput { Name = " Synthetic Theorealize ", Passcode = 74733322 }, NowUTC);

            CollectionAssert.AreEqual(new[] { CatalogCardField.NAME, CatalogCardField.PASSCODE }, changed.ToArray());
            Assert.AreEqual("Synthetic Theorealize", card.Name);
            Assert.AreEqual("Name,Passcode", card.LockedFields);
            Assert.AreEqual(NowUTC, card.DateModified);
        }

        [TestMethod]
        public void Apply_TextareaLineBreaksOnly_ChangesNothing()
        {
            var card = Card(1, "Synthetic Theory");
            card.Description = "First line.\nSecond line.";

            var changed = CatalogCardEditor.Apply(card, new CatalogCardEditInput { Description = "First line.\r\nSecond line.\r\n", Name = "Synthetic Theory" }, NowUTC);

            Assert.AreEqual(0, changed.Count);
            Assert.IsNull(card.LockedFields);
        }

        [TestMethod]
        public void Apply_ValueCleared_StoresNullAndLocksIt()
        {
            var card = Card(1, "Synthetic Theory");
            card.Archetype = "Synthetic";

            var changed = CatalogCardEditor.Apply(card, new CatalogCardEditInput { Archetype = "  ", Name = "Synthetic Theory" }, NowUTC);

            Assert.AreEqual(CatalogCardField.ARCHETYPE, changed.Single());
            Assert.IsNull(card.Archetype);
        }

        [TestMethod]
        [DataRow(null, null, null, DisplayName = "Missing name")]
        [DataRow("Synthetic Theory", 0, null, DisplayName = "Zero passcode")]
        [DataRow("Synthetic Theory", null, -1, DisplayName = "Negative ATK")]
        public void Validate_InvalidInput_ReturnsError(string? name, int? passcode, int? atk)
        {
            var errors = CatalogCardEditor.Validate(new CatalogCardEditInput { ATK = atk, Name = name, Passcode = passcode });

            Assert.AreEqual(1, errors.Count);
        }

        [TestMethod]
        public void Validate_ValidInput_ReturnsNoErrors()
        {
            var errors = CatalogCardEditor.Validate(new CatalogCardEditInput { ATK = 0, Name = "Synthetic Theory", Passcode = 74733322 });

            Assert.AreEqual(0, errors.Count);
        }
    }
}
