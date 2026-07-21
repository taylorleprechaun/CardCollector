using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace CardCollector.Tests.TestHelpers
{
    // Builds the minimal PageContext/TempData plumbing a bare PageModel needs so extension
    // methods and handlers that touch TempData/RedirectToPage/Request/Response/User can run outside a real request.
    internal static class PageContextFactory
    {
        public static void Attach(PageModel page, Action<DefaultHttpContext>? configureHttpContext = null)
        {
            var httpContext = new DefaultHttpContext();
            configureHttpContext?.Invoke(httpContext);

            var modelState = new ModelStateDictionary();
            var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
            var pageContext = new PageContext(actionContext);

            page.PageContext = pageContext;
            page.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            page.Url = Mock.Of<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        }

        public static TPage Create<TPage>(Action<DefaultHttpContext>? configureHttpContext = null) where TPage : PageModel, new()
        {
            var page = new TPage();
            Attach(page, configureHttpContext);
            return page;
        }
    }
}
