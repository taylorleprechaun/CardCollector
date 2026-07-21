using CardCollector.Pages;
using CardCollector.Tests.TestHelpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class LogoutModelTests
    {
        [TestMethod]
        public async Task OnPostAsync_SignsOutAndRedirectsToLogin()
        {
            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(s => s.SignOutAsync(It.IsAny<HttpContext>(), "CardCollectorCookie", null))
                .Returns(Task.CompletedTask);
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IAuthenticationService))).Returns(authServiceMock.Object);

            var page = new LogoutModel();
            PageContextFactory.Attach(page, httpContext => httpContext.RequestServices = services.Object);

            var result = await page.OnPostAsync() as RedirectToPageResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Login", result!.PageName);
            authServiceMock.Verify(s => s.SignOutAsync(It.IsAny<HttpContext>(), "CardCollectorCookie", null), Times.Once);
        }
    }
}
