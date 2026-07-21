using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class RazorPartialRendererTests
    {
        private Mock<ICompositeViewEngine> _viewEngineMock = null!;

        [TestMethod]
        public async Task RenderPartialAsync_ModelIsPassedThrough_ViewDataCarriesModel()
        {
            var model = new { Name = "Dark Magician" };
            object? capturedModel = null;
            var viewMock = new Mock<IView>();
            viewMock.Setup(v => v.RenderAsync(It.IsAny<ViewContext>()))
                .Returns((ViewContext ctx) =>
                {
                    capturedModel = ctx.ViewData.Model;
                    return Task.CompletedTask;
                });
            _viewEngineMock
                .Setup(v => v.FindView(It.IsAny<Microsoft.AspNetCore.Mvc.ActionContext>(), "_SomePartial", false))
                .Returns(ViewEngineResult.Found("_SomePartial", viewMock.Object));
            var renderer = new RazorPartialRenderer(_viewEngineMock.Object, Mock.Of<ITempDataProvider>());
            var page = CreatePage();

            await renderer.RenderPartialAsync(page, "_SomePartial", model);

            Assert.AreSame(model, capturedModel);
        }

        [TestMethod]
        public async Task RenderPartialAsync_ViewFound_ReturnsRenderedWriterContent()
        {
            var viewMock = new Mock<IView>();
            viewMock.Setup(v => v.RenderAsync(It.IsAny<ViewContext>()))
                .Returns((ViewContext ctx) =>
                {
                    ctx.Writer.Write("<div>rendered</div>");
                    return Task.CompletedTask;
                });
            _viewEngineMock
                .Setup(v => v.FindView(It.IsAny<Microsoft.AspNetCore.Mvc.ActionContext>(), "_SomePartial", false))
                .Returns(ViewEngineResult.Found("_SomePartial", viewMock.Object));
            var renderer = new RazorPartialRenderer(_viewEngineMock.Object, Mock.Of<ITempDataProvider>());
            var page = CreatePage();

            var result = await renderer.RenderPartialAsync(page, "_SomePartial", new object());

            Assert.AreEqual("<div>rendered</div>", result);
        }

        [TestMethod]
        public async Task RenderPartialAsync_ViewNotFound_ThrowsInvalidOperationException()
        {
            _viewEngineMock
                .Setup(v => v.FindView(It.IsAny<Microsoft.AspNetCore.Mvc.ActionContext>(), "_MissingPartial", false))
                .Returns(ViewEngineResult.NotFound("_MissingPartial", []));
            var renderer = new RazorPartialRenderer(_viewEngineMock.Object, Mock.Of<ITempDataProvider>());
            var page = CreatePage();

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => renderer.RenderPartialAsync(page, "_MissingPartial", new object()));
        }

        [TestInitialize]
        public void Setup()
        {
            _viewEngineMock = new Mock<ICompositeViewEngine>();
        }

        private static PageModel CreatePage()
        {
            var page = new TestPageModel();
            PageContextFactory.Attach(page);
            page.PageContext.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            return page;
        }

        private sealed class TestPageModel : PageModel;
    }
}
