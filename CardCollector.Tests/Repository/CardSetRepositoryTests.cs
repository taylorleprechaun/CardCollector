using CardCollector.DTO;
using CardCollector.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Repository
{
    [TestClass]
    public sealed class CardSetRepositoryTests
    {
        [TestMethod]
        public void BuildDateIndex_CodeLookupIsCaseInsensitive()
        {
            var sets = new List<CardSetData> { new() { Code = "lob", TCGDate = "2002-03-08" } };

            var result = CardSetRepository.BuildDateIndex(sets);

            Assert.IsTrue(result.ContainsKey("LOB"));
        }

        [TestMethod]
        public void BuildDateIndex_DuplicateCode_KeepsEarliestDate()
        {
            var sets = new List<CardSetData>
            {
                new() { Code = "LOB", TCGDate = "2020-06-01" },
                new() { Code = "LOB", TCGDate = "2002-03-08" }
            };

            var result = CardSetRepository.BuildDateIndex(sets);

            Assert.AreEqual("2002-03-08", result["LOB"]);
        }

        [TestMethod]
        public void BuildDateIndex_SetWithNullOrEmptyCodeOrDate_IsSkipped()
        {
            var sets = new List<CardSetData>
            {
                new() { Code = "", TCGDate = "2020-01-01" },
                new() { Code = "LOB", TCGDate = null }
            };

            var result = CardSetRepository.BuildDateIndex(sets);

            Assert.AreEqual(0, result.Count);
        }
    }
}
