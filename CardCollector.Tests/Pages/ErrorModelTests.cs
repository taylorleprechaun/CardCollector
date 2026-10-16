using System.Diagnostics;
using CardCollector.Pages;
using CardCollector.Tests.TestHelpers;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class ErrorModelTests
    {
        [TestMethod]
        public void OnGet_InsideAnActivity_ShowsTheActivityID()
        {
            using var activity = new Activity("Synthetic request").Start();
            var page = PageContextFactory.Create<ErrorModel>(httpContext => httpContext.TraceIdentifier = "trace-1");

            page.OnGet();

            Assert.AreEqual(activity.Id, page.RequestID);
        }

        [TestMethod]
        public void OnGet_NoActivity_ShowsTheTraceIdentifier()
        {
            Activity.Current = null;
            var page = PageContextFactory.Create<ErrorModel>(httpContext => httpContext.TraceIdentifier = "trace-1");

            page.OnGet();

            Assert.AreEqual("trace-1", page.RequestID);
        }
    }
}
