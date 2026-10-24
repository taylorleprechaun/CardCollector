using CardCollector.Data.Models.Catalog;
using CardCollector.Extensions;
using CardCollector.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace CardCollector.Pages.Catalog
{
    public sealed class ReviewModel : PageModel
    {
        private static readonly JsonSerializerOptions CountsJsonOptions = new(JsonSerializerDefaults.Web);

        private readonly ICatalogReviewService _catalogReviewService;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

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

        /// <summary>Text the listed items' summary, resolution, or card name must contain.</summary>
        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }

        public PagedResult<CatalogReviewItemViewModel> Results { get; private set; } = new();

        public ReviewModel(ICatalogReviewService catalogReviewService, IRazorPartialRenderer razorPartialRenderer)
        {
            _catalogReviewService = catalogReviewService;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public int CountOpen(bool actionable) => OpenCountsByKind.Where(kv => kv.Key.IsActionable() == actionable).Sum(kv => kv.Value);

        /// <summary>
        /// What "Dismiss all" covers, e.g. "all 248 open Legacy Row Auto-Mapped items; other kinds stay open", so the button
        /// and its confirmation say exactly what will be dismissed.
        /// </summary>
        public string DescribeDismissAll()
        {
            var count = Results.TotalCount;
            var scope = Kind is CatalogReviewItemKind kind ? kind.GetDisplayName() : "informational";
            var matching = Query.TrimToNull() is string text ? $" matching “{text}”" : string.Empty;
            var rest = (Kind, matching.Length > 0) switch
            {
                (null, false) => ", of every kind",
                (null, true) => "; other items stay open",
                _ => "; other kinds stay open"
            };
            return $"all {count} open {scope} item{(count == 1 ? string.Empty : "s")}{matching}{rest}";
        }

        /// <summary>The empty-list message, naming the kind and search when they are what emptied the list.</summary>
        public string DescribeEmpty()
        {
            var text = Query.TrimToNull();
            if (Kind is null && text is null)
                return Filter switch
                {
                    CatalogReviewFilter.NeedsDecision => "Nothing needs a decision.",
                    CatalogReviewFilter.Informational => "No informational items.",
                    _ => "Nothing has been resolved yet."
                };

            var state = Filter == CatalogReviewFilter.Resolved ? "resolved" : "open";
            var scope = Kind is CatalogReviewItemKind kind ? kind.GetDisplayName() : Filter == CatalogReviewFilter.Informational ? "informational" : string.Empty;
            var matching = text is null ? string.Empty : $" matching “{text}”";
            return $"No {state} {scope}{(scope.Length > 0 ? " " : string.Empty)}items{matching}.";
        }

        /// <summary>The query string that keeps the current tab, kind, search, and page when an action posts back.</summary>
        public string GetFilterParams()
        {
            var search = Query.TrimToNull() is string text ? $"&q={Uri.EscapeDataString(text)}" : string.Empty;
            return $"filter={Filter}&kind={Kind?.ToString() ?? string.Empty}{search}&pageNumber={PageNumber}&pageSize={PageSize}";
        }

        public IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>
            {
                ["filter"] = Filter.ToString(),
                ["kind"] = Kind?.ToString(),
                ["q"] = Query.TrimToNull()
            };

        public async Task<IActionResult> OnGetAsync()
        {
            PageNumber = Paging.ClampPage(PageNumber);
            PageSize = Paging.NormalizePageSize(PageSize);

            OpenCountsByKind = await _catalogReviewService.GetOpenCountsByKindAsync().ConfigureAwait(false);
            Results = await _catalogReviewService.SearchAsync(Criteria(PageNumber, PageSize)).ConfigureAwait(false);

            // Deciding every item on the last page leaves it empty; show the page the remaining items now end on instead.
            if (Results.Items.Count == 0 && PageNumber > 1 && Results.TotalCount > 0)
                return RedirectToPage(new { filter = Filter, kind = Kind, q = Query.TrimToNull(), pageNumber = Results.TotalPages, pageSize = PageSize });

            return Page();
        }

        public async Task<IActionResult> OnPostAcceptAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.AcceptAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostCreateCardAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.CreateCardAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostCreatePrintingAsync(int id, int cardID) =>
            await RespondAsync(id, await _catalogReviewService.CreatePrintingAsync(id, cardID).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostDismissAllAsync()
        {
            var count = await _catalogReviewService.DismissAllInformationalAsync(Kind, Query.TrimToNull()).ConfigureAwait(false);
            TempData["Success"] = count == 1 ? "Dismissed 1 informational item." : $"Dismissed {count} informational items.";
            return RedirectToPage(new { filter = Filter, kind = Kind, q = Query.TrimToNull() });
        }

        public async Task<IActionResult> OnPostDismissAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.DismissAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostLinkKeepRetiredAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.LinkKeepRetiredAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostMapAsync(int id, int printingID) =>
            await RespondAsync(id, await _catalogReviewService.MapToPrintingAsync(id, printingID).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostRejectAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.RejectAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        public async Task<IActionResult> OnPostReopenAsync(int id) =>
            await RespondAsync(id, await _catalogReviewService.ReopenAsync(id).ConfigureAwait(false)).ConfigureAwait(false);

        private CatalogReviewSearchCriteria Criteria(int page, int pageSize) =>
            new() { Filter = Filter, Kind = Kind, Page = page, PageSize = pageSize, Query = Query.TrimToNull() };

        /// <summary>A decided item leaves the open tabs; a reopened one leaves the Resolved tab.</summary>
        private bool HasLeftTab(CatalogReviewItemViewModel item) =>
            Filter == CatalogReviewFilter.Resolved
                ? item.Status == CatalogReviewItemStatus.Open
                : item.Status != CatalogReviewItemStatus.Open;

        private Task<string> RenderRowAsync(CatalogReviewItemViewModel item) =>
            _razorPartialRenderer.RenderPartialAsync(this, "_ReviewItemRow", new CatalogReviewRowViewModel { FilterParams = GetFilterParams(), Item = item, ShowKind = Kind is null });

        /// <summary>
        /// After a decision: a full-page post redirects back to the same view. An AJAX post gets the item's row re-rendered
        /// (with the error, when the decision failed); when the item has left the current tab, it gets the item that moved up
        /// onto the end of this page instead (marked with <c>X-Ajax-Append</c>), or nothing. Either way the response carries
        /// the open counts, so the tabs, the kind filter, and the navigation badge stay current.
        /// </summary>
        private async Task<IActionResult> RespondAsync(int id, SaveResult result)
        {
            var error = result.Succeeded ? null : string.Join(" ", result.Errors);
            if (!Request.IsAjaxRequest())
            {
                if (error is not null)
                    TempData["Warning"] = error;
                return RedirectToPage(new { filter = Filter, kind = Kind, q = Query.TrimToNull(), pageNumber = PageNumber, pageSize = PageSize });
            }

            var openCounts = await _catalogReviewService.GetOpenCountsByKindAsync().ConfigureAwait(false);
            OpenCountsByKind = openCounts;
            Response.Headers["X-Review-Count"] = CountOpen(actionable: true).ToString();
            Response.Headers["X-Review-Open-Counts"] = JsonSerializer.Serialize(
                new
                {
                    Informational = CountOpen(actionable: false),
                    Kinds = openCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                    NeedsDecision = CountOpen(actionable: true)
                },
                CountsJsonOptions);

            // The item at the last slot of the current page: after a row leaves, it is the one that moved up onto this page.
            var pageSize = Paging.ClampPageSize(PageSize);
            var lastSlot = await _catalogReviewService
                .SearchAsync(Criteria(Paging.ClampPage(PageNumber) * pageSize, 1))
                .ConfigureAwait(false);
            Response.Headers["X-Total-Count"] = lastSlot.TotalCount.ToString();

            var item = await _catalogReviewService.GetItemAsync(id, error).ConfigureAwait(false);
            if (item is not null && (error is not null || !HasLeftTab(item)))
                return Content(await RenderRowAsync(item).ConfigureAwait(false), "text/html");

            if (error is not null || lastSlot.Items.FirstOrDefault() is not { } next || next.ID == id)
                return Content(string.Empty, "text/html");

            Response.Headers["X-Ajax-Append"] = "true";
            return Content(await RenderRowAsync(next).ConfigureAwait(false), "text/html");
        }
    }
}
