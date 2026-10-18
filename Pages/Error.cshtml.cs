using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace CardCollector.Pages
{
    /// <summary>
    /// Shown by the exception handler (outside Development) when a request fails, instead of a bare 500. Anonymous, and
    /// without the shared layout, so it still renders when the failure came from the database or the layout itself.
    /// </summary>
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public sealed class ErrorModel : PageModel
    {
        /// <summary>The failed request's ID, which the server log shows next to the exception.</summary>
        public string RequestID { get; private set; } = string.Empty;

        public void OnGet() => RequestID = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
