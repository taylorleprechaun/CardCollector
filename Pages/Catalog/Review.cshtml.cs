using CardCollector.Data.Models.Catalog;
using CardCollector.Extensions;
using CardCollector.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Catalog
{
    public sealed class ReviewModel : PageModel
    {
        private readonly ICatalogReviewService _catalogReviewService;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        public ReviewModel(ICatalogReviewService catalogReviewService, IRazorPartialRenderer razorPartialRenderer)
        {
            _catalogReviewService = catalogReviewService;
            _razorPartialRenderer = razorPartialRenderer;
        }

        [BindProperty(SupportsGet = true)]
        public CatalogReviewFilter Filter { get; set; } = CatalogReviewFilter.NeedsDecision;

        /// <summary>The kinds the kind filter offers under the current tab.</summary>
        public IReadOnlyList<CatalogReviewItemKind> FilterKinds =>
            Enum.GetValues<CatalogReviewItemKind>()
                .Where(k => Filter switch
                {
                    CatalogReviewFilter.NeedsDecision => k.IsActionable(),
                    CatalogReviewFilter.Informational => !k.IsActionable(),
                    _ => true
                })
                .ToList();

        [BindProperty(SupportsGet = true)]
        public CatalogReviewItemKind? Kind { get; set; }

        /// <summary>Open items per kind, for the tab counts and the kind filter.</summary>
        public IReadOnlyDictionary<CatalogReviewItemKind, int> OpenCountsByKind { get; private set; } = new Dictionary<CatalogReviewItemKind, int>();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = Paging.DEFAULT_PAGE_SIZE;

        public PagedResult<CatalogReviewItemViewModel> Results { get; private set; } = new();

        public int CountOpen(bool actionable) => OpenCountsByKind.Where(kv => kv.Key.IsActionable() == actionable).Sum(kv => kv.Value);

        /// <summary>The query string that keeps the current tab, kind, and page when an action posts back.</summary>
        public string GetFilterParams() => $"filter={Filter}&kind={Kind?.ToString() ?? string.Empty}&pageNumber={PageNumber}&pageSize={PageSize}";

        public IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>
            {
                ["filter"] = Filter.ToString(),
                ["kind"] = Kind?.ToString()
            };

        public async Task OnGetAsync()
        {
            PageNumber = Paging.ClampPage(PageNumber);
            PageSize = Paging.NormalizePageSize(PageSize);

            OpenCountsByKind = await _catalogReviewService.GetOpenCountsByKindAsync().ConfigureAwait(false);
            Results = await _catalogReviewService
                .SearchAsync(new CatalogReviewSearchCriteria { Filter = Filter, Kind = Kind, Page = PageNumber, PageSize = PageSize })
                .ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostAcceptAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.AcceptAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostCreatePrintingAsync(int id, int cardID) =>
            await RespondAsync(id, await _catalogReviewService.CreatePrintingAsync(id, cardID).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostDismissAllAsync()
        {
            var count = await _catalogReviewService.DismissAllInformationalAsync(Kind).ConfigureAwait(false);
            TempData["Success"] = count == 1 ? "Dismissed 1 informational item." : $"Dismissed {count} informational items.";
            return RedirectToPage(new { filter = Filter, kind = Kind });
        }

        public async Task<IActionResult> OnPostDismissAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.DismissAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostMapAsync(int id, int printingID) =>
            await RespondAsync(id, await _catalogReviewService.MapToPrintingAsync(id, printingID).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostRejectAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.RejectAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        /// <summary>
        /// After a decision: a full-page post redirects back to the same view; an AJAX post gets the item's row re-rendered
        /// (with the error, when the decision failed), or nothing when the item has left the current tab.
        /// </summary>
        private async Task<IActionResult> RespondAsync(int id, SaveResult result)
        {
            var error = result.Succeeded ? null : string.Join(" ", result.Errors);
            if (!Request.IsAjaxRequest())
            {
                if (error is not null)
                    TempData["Warning"] = error;
                return RedirectToPage(new { filter = Filter, kind = Kind, pageNumber = PageNumber, pageSize = PageSize });
            }

            Response.Headers["X-Review-Count"] = (await _catalogReviewService.CountNeedingDecisionAsync().ConfigureAwait(false)).ToString();
            var current = await _catalogReviewService
                .SearchAsync(new CatalogReviewSearchCriteria { Filter = Filter, Kind = Kind, Page = 1, PageSize = 1 })
                .ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = current.TotalCount.ToString();

            var item = await _catalogReviewService.GetItemAsync(id, error).ConfigureAwait(false);
            if (item is null)
                return Content(string.Empty, "text/html");

            var leftTab = Filter != CatalogReviewFilter.Resolved && item.Status != CatalogReviewItemStatus.Open;
            if (leftTab && error is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer
                .RenderPartialAsync(this, "_ReviewItemRow", new CatalogReviewRowViewModel { FilterParams = GetFilterParams(), Item = item })
                .ConfigureAwait(false);
            return Content(html, "text/html");
        }
    }
}
