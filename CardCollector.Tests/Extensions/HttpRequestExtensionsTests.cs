using CardCollector.Extensions;
using Microsoft.AspNetCore.Http;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class HttpRequestExtensionsTests
    {
        [TestMethod]
        public void IsAjaxRequest_FetchHeader_ReturnsTrue()
        {
            var request = new DefaultHttpContext().Request;
            request.Headers["X-Requested-With"] = "XMLHttpRequest";

            Assert.IsTrue(request.IsAjaxRequest());
        }

        [TestMethod]
        [DataRow(null, DisplayName = "No header")]
        [DataRow("fetch", DisplayName = "Other value")]
        public void IsAjaxRequest_NoFetchHeader_ReturnsFalse(string? headerValue)
        {
            var request = new DefaultHttpContext().Request;
            if (headerValue is not null)
                request.Headers["X-Requested-With"] = headerValue;

            Assert.IsFalse(request.IsAjaxRequest());
        }

        [TestMethod]
        public void IsAjaxRequest_NullRequest_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => ((HttpRequest)null!).IsAjaxRequest());
        }
    }
}
