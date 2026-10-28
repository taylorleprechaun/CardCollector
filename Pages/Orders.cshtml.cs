using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace CardCollector.Pages
{
    public sealed class OrdersModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        /// <summary>Cards across every order matching the search, not just this page.</summary>
        public int CardCount => Results.TotalQuantitySum;

        protected override ICardService CardService => _cardService;

        public PagedResult<EditionAuditEntryViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; }

        /// <summary>Purchase price times quantity across every order matching the search, not just this page.</summary>
        public decimal TotalCost { get; private set; }

        public OrdersModel(ICardService cardService, ICollectionRepository collectionRepository, IRazorPartialRenderer razorPartialRenderer)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
            _razorPartialRenderer = razorPartialRenderer;
        }

        /// <summary>The current search, filters, sort, and page as route values, so a post can come back to the same view.</summary>
        public IDictionary<string, string> GetFilterRouteValues()
        {
            var values = new Dictionary<string, string>
            {
                ["pageNumber"] = PageNumber.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = PageSize.ToString(CultureInfo.InvariantCulture),
                ["sortDescending"] = SortDescending.ToString()
            };
            AddIfPresent(values, "cardType", CardType);
            AddIfPresent(values, "query", Query);
            AddIfPresent(values, "rarityName", RarityName);
            AddIfPresent(values, "setName", SetName);
            return values;
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>(base.GetPaginationParams())
            {
                ["sortDescending"] = SortDescending.ToString()
            };

        public async Task OnGetAsync()
        {
            await LoadAsync().ConfigureAwait(false);
        }

        // Suggests only names of cards with a pending order, unlike the catalog-wide suggestions on other pages.
        public async Task<IActionResult> OnGetOrderAutocompleteAsync(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new JsonResult(Array.Empty<string>());

            return new JsonResult(await _cardService.GetOrderedCardNameSuggestionsAsync(q.Trim()).ConfigureAwait(false));
        }

        public async Task<IActionResult> OnPostDeleteAsync(int entryID)
        {
            await _collectionRepository.DeleteAsync(entryID);
            return RedirectToPage(GetFilterRouteValues());
        }

        public async Task<IActionResult> OnPostEditAsync(
            int entryID, int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice,
            decimal? marketPriceAtEntry)
        {
            var entry = new CollectionEntry
            {
                ID = entryID,
                AcquisitionMethod = acquisitionMethod,
                Condition = condition,
                Edition = edition,
                MarketPriceAtEntry = marketPriceAtEntry,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity
            };

            await _collectionRepository.UpdateAsync(entry);

            if (!Request.IsAjaxRequest())
                return RedirectToPage(GetFilterRouteValues());

            await LoadAsync().ConfigureAwait(false);
            Response.Headers["X-Orders-Count"] = Results.TotalCount.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Orders-Cards"] = CardCount.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Orders-Total"] = TotalCost.ToString(CultureInfo.InvariantCulture);

            var match = Results.Items.FirstOrDefault(o => o.EntryID == entryID);
            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_OrderRow", match);
            return Content(html, "text/html");
        }

        public async Task<IActionResult> OnPostMarkOwnedAsync(int entryID, int quantity)
        {
            await _collectionRepository.UpdateStatusAsync(entryID, CollectionStatus.Owned, quantity < 1 ? 1 : quantity);
            return RedirectToPage(GetFilterRouteValues());
        }

        private static void AddIfPresent(IDictionary<string, string> values, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values[key] = value.Trim();
        }

        private async Task LoadAsync()
        {
            NormalizeSearchParameters();

            var result = await _cardService.SearchOrdersAsync(new OrderSearchCriteria
            {
                CardType = CardType,
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName,
                SortDescending = SortDescending
            }).ConfigureAwait(false);

            AvailableRarityNames = result.AvailableRarityNames;
            AvailableSetNames = result.AvailableSetNames;
            Results = result.PagedItems;
            TotalCost = result.TotalCost;
        }
    }
}
