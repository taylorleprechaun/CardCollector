using CardCollector.Data.Models;
using CardCollector.Models.Legacy;

namespace CardCollector.Tests.Models.Legacy
{
    [TestClass]
    public sealed class LegacyRowTests
    {
        [TestMethod]
        public void GetDateTime_EFTextDate_ParsesAsUTC()
        {
            var row = Row(("DateCreated", "2026-10-03 18:51:40.0265372"));

            var value = row.GetDateTime("DateCreated");

            Assert.AreEqual(new DateTime(2026, 10, 3, 18, 51, 40, DateTimeKind.Utc).AddTicks(265372), value);
            Assert.AreEqual(DateTimeKind.Utc, value.Kind);
        }

        [TestMethod]
        public void GetDecimal_RealValue_ConvertsAndNullStaysNull()
        {
            var row = Row(("Price", 5.36), ("Missing", null));

            Assert.AreEqual(5.36m, row.GetDecimal("Price"));
            Assert.IsNull(row.GetDecimal("Missing"));
        }

        [TestMethod]
        public void GetEnum_StoredName_ParsesAndUnknownIsNull()
        {
            var row = Row(("Edition", "FirstEdition"), ("Other", "NotAnEdition"));

            Assert.AreEqual(CardEdition.FirstEdition, row.GetEnum<CardEdition>("Edition"));
            Assert.IsNull(row.GetEnum<CardEdition>("Other"));
        }

        [TestMethod]
        public void GetInt_NullColumn_Throws()
        {
            var row = Row(("CardID", null));

            Assert.ThrowsExactly<InvalidOperationException>(() => row.GetInt("CardID"));
        }

        [TestMethod]
        public void GetNullableInt_MissingColumn_ReturnsNull()
        {
            var row = Row(("CardID", 11111111L));

            Assert.AreEqual(11111111, row.GetNullableInt("CardID"));
            Assert.IsNull(row.GetNullableInt("DesiredQuantity"));
        }

        private static LegacyRow Row(params (string Column, object? Value)[] values) =>
            new(values.ToDictionary(v => v.Column, v => v.Value, StringComparer.OrdinalIgnoreCase));
    }
}
