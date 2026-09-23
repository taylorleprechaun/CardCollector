using CardCollector.ViewModels;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class PagingTests
    {
        [TestMethod]
        [DataRow(-3, 1, DisplayName = "Negative")]
        [DataRow(0, 1, DisplayName = "Zero")]
        [DataRow(1, 1, DisplayName = "First page")]
        [DataRow(7, 7, DisplayName = "Later page")]
        public void ClampPage_Page_IsAtLeastOne(int page, int expected)
        {
            var result = Paging.ClampPage(page);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(0, 25, DisplayName = "Zero")]
        [DataRow(-5, 25, DisplayName = "Negative")]
        [DataRow(101, 25, DisplayName = "Over the maximum")]
        [DataRow(1, 1, DisplayName = "Minimum")]
        [DataRow(37, 37, DisplayName = "Size not offered in the UI")]
        [DataRow(100, 100, DisplayName = "Maximum")]
        public void ClampPageSize_PageSize_KeepsSizesFromOneToTheMaximum(int pageSize, int expected)
        {
            var result = Paging.ClampPageSize(pageSize);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(10, 10, DisplayName = "Offered size")]
        [DataRow(100, 100, DisplayName = "Largest offered size")]
        [DataRow(37, 25, DisplayName = "Size not offered")]
        [DataRow(0, 25, DisplayName = "Zero")]
        [DataRow(999, 25, DisplayName = "Oversized")]
        public void NormalizePageSize_PageSize_KeepsOnlyOfferedSizes(int pageSize, int expected)
        {
            var result = Paging.NormalizePageSize(pageSize);

            Assert.AreEqual(expected, result);
        }
    }
}
