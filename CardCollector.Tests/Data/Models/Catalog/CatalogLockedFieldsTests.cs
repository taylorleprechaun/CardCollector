using CardCollector.Data.Models.Catalog;

namespace CardCollector.Tests.Data.Models.Catalog
{
    [TestClass]
    public sealed class CatalogLockedFieldsTests
    {
        [TestMethod]
        public void Add_FieldAlreadyLocked_LeavesListUnchanged()
        {
            var result = CatalogLockedFields.Add("Name,Passcode", "passcode");

            Assert.AreEqual("Name,Passcode", result);
        }

        [TestMethod]
        public void Add_NothingLocked_ReturnsFieldName()
        {
            var result = CatalogLockedFields.Add(null, CatalogCardField.NAME);

            Assert.AreEqual("Name", result);
        }

        [TestMethod]
        [DataRow(null, "Name", false, DisplayName = "Nothing locked")]
        [DataRow("Name, Passcode", "Passcode", true, DisplayName = "Spaces around entries")]
        [DataRow("Name", "name", true, DisplayName = "Case-insensitive")]
        [DataRow("NameExtra", "Name", false, DisplayName = "Whole entries only")]
        public void Contains_ReturnsExpected(string? lockedFields, string fieldName, bool expected)
        {
            var result = CatalogLockedFields.Contains(lockedFields, fieldName);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Remove_LastLockedField_ReturnsNull()
        {
            var result = CatalogLockedFields.Remove("Name", "name");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Remove_OneOfSeveral_KeepsTheOthersInOrder()
        {
            var result = CatalogLockedFields.Remove("Name, Passcode,ATK", CatalogCardField.PASSCODE);

            Assert.AreEqual("Name,ATK", result);
        }
    }
}
