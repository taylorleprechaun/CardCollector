using CardCollector.Extensions;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class HttpClientExtensionsTests
    {
        [TestMethod]
        public void ApplyAppDefaults_Client_SetsTimeoutAndUserAgent()
        {
            using var client = new HttpClient();

            client.ApplyAppDefaults(TimeSpan.FromSeconds(45));

            Assert.AreEqual(TimeSpan.FromSeconds(45), client.Timeout);
            Assert.AreEqual(HttpClientExtensions.USER_AGENT, client.DefaultRequestHeaders.UserAgent.ToString());
        }

        [TestMethod]
        public void ApplyAppDefaults_NullClient_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => ((HttpClient)null!).ApplyAppDefaults(TimeSpan.FromSeconds(1)));
        }
    }
}
