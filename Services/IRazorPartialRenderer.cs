using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Services
{
    /// <summary>
    /// Renders a Razor partial view to an HTML string outside of a normal page render,
    /// so AJAX handlers can return updated markup for a single DOM fragment.
    /// </summary>
    public interface IRazorPartialRenderer
    {
        Task<string> RenderPartialAsync<TModel>(PageModel page, string partialViewName, TModel model);
    }
}
