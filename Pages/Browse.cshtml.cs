using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class BrowseModel : SearchablePageModel
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardService _cardService;

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public string? CollectionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? OrderedFilter { get; set; }

        protected override ICardService CardService => _cardService;

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? WishlistFilter { get; set; }

        public override int ActiveFilterCount =>
            base.ActiveFilterCount
            + (string.IsNullOrEmpty(CollectionFilter) ? 0 : 1)
            + (string.IsNullOrEmpty(OrderedFilter) ? 0 : 1)
            + (string.IsNullOrEmpty(WishlistFilter) ? 0 : 1);

        public override bool HasActiveFilters =>
            base.HasActiveFilters
            || !string.IsNullOrEmpty(CollectionFilter)
            || !string.IsNullOrEmpty(OrderedFilter)
            || !string.IsNullOrEmpty(WishlistFilter);

        public BrowseModel(ICardService cardService, ICardDataRepository cardDataRepository)
        {
            _cardService = cardService;
            _cardDataRepository = cardDataRepository;
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>(base.GetPaginationParams())
            {
                ["collectionFilter"] = CollectionFilter,
                ["orderedFilter"] = OrderedFilter,
                ["wishlistFilter"] = WishlistFilter
            };

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            AvailableRarityNames = _cardDataRepository.DistinctRarityNames;
            AvailableSetNames = _cardDataRepository.DistinctSetNames;

            var criteria = new BrowseSearchCriteria
            {
                CardType = CardType,
                InCollection = CollectionFilter == "incomplete" ? true : ParseFilter(CollectionFilter),
                InWishlist = ParseFilter(WishlistFilter),
                IsIncomplete = CollectionFilter == "incomplete" ? true : null,
                IsOrdered = ParseFilter(OrderedFilter),
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName
            };

            Results = await _cardService.SearchCardsAsync(criteria);
        }

        private static bool? ParseFilter(string? value) =>
            value == "yes" ? true : value == "no" ? false : null;
    }
}
