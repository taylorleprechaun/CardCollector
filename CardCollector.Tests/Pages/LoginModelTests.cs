using CardCollector.Pages;
using CardCollector.Tests.TestHelpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class LoginModelTests
    {
        private static readonly string ValidHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        [TestMethod]
        public async Task OnPostAsync_NoPasswordHashConfigured_SetsErrorMessage()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var page = CreatePage(config);
            page.Username = "admin";
            page.Password = "anything";

            var result = await page.OnPostAsync(null);

            Assert.IsInstanceOfType<PageResult>(result);
        }

        [TestMethod]
        public async Task OnPostAsync_ValidCredentialsNoReturnUrl_RedirectsToIndex()
        {
            var page = CreatePage(BuildConfig());
            page.Username = "admin";
            page.Password = "correct-password";

            var result = await page.OnPostAsync(null) as RedirectToPageResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Index", result!.PageName);
        }

        [TestMethod]
        public async Task OnPostAsync_ValidCredentialsWithLocalReturnUrl_RedirectsLocally()
        {
            var page = CreatePage(BuildConfig(), isLocalUrl: true);
            page.Username = "admin";
            page.Password = "correct-password";

            var result = await page.OnPostAsync("/Collection") as LocalRedirectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("/Collection", result!.Url);
        }

        [TestMethod]
        public async Task OnPostAsync_WrongPassword_SetsErrorMessageAndReturnsPage()
        {
            var page = CreatePage(BuildConfig());
            page.Username = "admin";
            page.Password = "wrong-password";

            var result = await page.OnPostAsync(null);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreEqual("Invalid username or password.", page.ErrorMessage);
        }

        [TestMethod]
        public async Task OnPostAsync_WrongUsername_SetsErrorMessageAndReturnsPage()
        {
            var page = CreatePage(BuildConfig());
            page.Username = "not-admin";
            page.Password = "correct-password";

            var result = await page.OnPostAsync(null);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreEqual("Invalid username or password.", page.ErrorMessage);
        }

        private static IConfiguration BuildConfig() =>
                                                    new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Username"] = "admin",
                    ["Auth:PasswordHash"] = ValidHash
                })
                .Build();

        private static LoginModel CreatePage(IConfiguration config, bool isLocalUrl = false)
        {
            var page = new LoginModel(config);
            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(s => s.SignInAsync(It.IsAny<HttpContext>(), "CardCollectorCookie", It.IsAny<System.Security.Claims.ClaimsPrincipal>(), null))
                .Returns(Task.CompletedTask);
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IAuthenticationService))).Returns(authServiceMock.Object);

            PageContextFactory.Attach(page, httpContext => httpContext.RequestServices = services.Object);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(isLocalUrl);
            page.Url = urlHelperMock.Object;

            return page;
        }
    }
}
