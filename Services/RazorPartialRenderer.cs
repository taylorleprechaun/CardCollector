using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CardCollector.Services
{
    public sealed class RazorPartialRenderer : IRazorPartialRenderer
    {
        private readonly ITempDataProvider _tempDataProvider;
        private readonly ICompositeViewEngine _viewEngine;

        public RazorPartialRenderer(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider)
        {
            _tempDataProvider = tempDataProvider;
            _viewEngine = viewEngine;
        }

        public async Task<string> RenderPartialAsync<TModel>(PageModel page, string partialViewName, TModel model)
        {
            var actionContext = page.PageContext;

            var viewResult = _viewEngine.FindView(actionContext, partialViewName, isMainPage: false);
            if (!viewResult.Success)
                throw new InvalidOperationException($"Partial view '{partialViewName}' could not be found.");

            await using var writer = new StringWriter();

            var viewData = new ViewDataDictionary<TModel>(page.ViewData, model);
            var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

            var viewContext = new ViewContext(actionContext, viewResult.View, viewData, tempData, writer, new HtmlHelperOptions());

            await viewResult.View.RenderAsync(viewContext).ConfigureAwait(false);

            return writer.ToString();
        }
    }
}
