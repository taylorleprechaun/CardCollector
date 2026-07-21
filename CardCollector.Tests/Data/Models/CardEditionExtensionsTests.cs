using CardCollector.Data.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Data.Models
{
    [TestClass]
    public sealed class CardEditionExtensionsTests
    {
        [TestMethod]
        [DataRow(CardEdition.FirstEdition, "1st Edition")]
        [DataRow(CardEdition.LimitedEdition, "Limited")]
        [DataRow(CardEdition.Unlimited, "Unlimited")]
        public void GetTCGAPIEditionName_KnownEdition_ReturnsApiName(CardEdition edition, string expected)
        {
            var result = edition.GetTCGAPIEditionName();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1st Edition", CardEdition.FirstEdition)]
        [DataRow("Limited", CardEdition.LimitedEdition)]
        [DataRow("Unlimited", CardEdition.Unlimited)]
        public void TryParseTCGAPIEditionName_KnownApiName_ReturnsTrueAndParsedEdition(string apiName, CardEdition expected)
        {
            var success = CardEditionExtensions.TryParseTCGAPIEditionName(apiName, out var edition);

            Assert.IsTrue(success);
            Assert.AreEqual(expected, edition);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null")]
        [DataRow("", DisplayName = "Empty")]
        [DataRow("Not A Real Edition", DisplayName = "Unmapped string")]
        public void TryParseTCGAPIEditionName_UnknownApiName_ReturnsFalse(string? apiName)
        {
            var success = CardEditionExtensions.TryParseTCGAPIEditionName(apiName, out _);

            Assert.IsFalse(success);
        }
    }
}
