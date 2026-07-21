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

        [TestMethod]
        public void Deserialize_MalformedJson_ReturnsEmptyList()
        {
            const string json = "{ this is not valid json";

            var result = CardSetRepository.Deserialize(json);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Deserialize_ValidJson_ReturnsDeserializedSets()
        {
            const string json = /*lang=json,strict*/ """[{"set_code":"LOB","set_name":"Legend of Blue Eyes White Dragon"}]""";

            var result = CardSetRepository.Deserialize(json);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("LOB", result[0].Code);
        }

        [TestMethod]
        public void GetTCGDateBySetCode_KnownPrefix_ReturnsDate()
        {
            var dateByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["LOB"] = "2002-03-08" };

            var result = CardSetRepository.GetTCGDateBySetCode(dateByCode, "LOB-EN001");

            Assert.AreEqual("2002-03-08", result);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null")]
        [DataRow("", DisplayName = "Empty")]
        public void GetTCGDateBySetCode_NullOrEmptyFullSetCode_ReturnsNull(string? fullSetCode)
        {
            var dateByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["LOB"] = "2002-03-08" };

            var result = CardSetRepository.GetTCGDateBySetCode(dateByCode, fullSetCode!);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetTCGDateBySetCode_UnknownPrefix_ReturnsNull()
        {
            var dateByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["LOB"] = "2002-03-08" };

            var result = CardSetRepository.GetTCGDateBySetCode(dateByCode, "MRD-EN001");

            Assert.IsNull(result);
        }
    }
}
