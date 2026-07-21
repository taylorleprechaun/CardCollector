using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class PagedResultTests
    {
        [TestMethod]
        public void HasNextPage_PageIsLastPage_ReturnsFalse()
        {
            var result = new PagedResult<string> { Page = 3, PageSize = 10, TotalCount = 25 };

            Assert.IsFalse(result.HasNextPage);
        }

        [TestMethod]
        public void HasNextPage_PageLessThanTotalPages_ReturnsTrue()
        {
            var result = new PagedResult<string> { Page = 1, PageSize = 10, TotalCount = 25 };

            Assert.IsTrue(result.HasNextPage);
        }

        [TestMethod]
        public void HasPreviousPage_PageIsGreaterThanOne_ReturnsTrue()
        {
            var result = new PagedResult<string> { Page = 2, PageSize = 10, TotalCount = 25 };

            Assert.IsTrue(result.HasPreviousPage);
        }

        [TestMethod]
        public void HasPreviousPage_PageIsOne_ReturnsFalse()
        {
            var result = new PagedResult<string> { Page = 1, PageSize = 10, TotalCount = 25 };

            Assert.IsFalse(result.HasPreviousPage);
        }

        [TestMethod]
        [DataRow(1, 10, 25, 3, DisplayName = "25 items at 10 per page rounds up to 3 pages")]
        [DataRow(1, 10, 20, 2, DisplayName = "Exact multiple of page size")]
        [DataRow(1, 10, 0, 0, DisplayName = "No items")]
        public void TotalPages_ComputesCeilingOfCountOverPageSize(int page, int pageSize, int totalCount, int expectedTotalPages)
        {
            var result = new PagedResult<string> { Page = page, PageSize = pageSize, TotalCount = totalCount };

            Assert.AreEqual(expectedTotalPages, result.TotalPages);
        }

        [TestMethod]
        public void TotalPages_PageSizeIsZero_ReturnsZeroInsteadOfDividingByZero()
        {
            var result = new PagedResult<string> { Page = 1, PageSize = 0, TotalCount = 50 };

            Assert.AreEqual(0, result.TotalPages);
        }
    }
}
