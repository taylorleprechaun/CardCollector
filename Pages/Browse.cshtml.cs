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

        public IReadOnlyList<string> AvailableCardTypes { get; } = AdvancedFiltersViewModel.CardTypes;

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        protected override ICardService CardService => _cardService;

        [BindProperty(SupportsGet = true)]
        public string? CardType { get; set; }

        public bool HasActiveFilters => !string.IsNullOrWhiteSpace(CardType)
            || !string.IsNullOrWhiteSpace(RarityName)
            || !string.IsNullOrWhiteSpace(SetName);

        [BindProperty(SupportsGet = true)]
        public string? RarityName { get; set; }

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SetName { get; set; }

        public BrowseModel(ICardService cardService, ICardDataRepository cardDataRepository)
        {
            _cardService = cardService;
            _cardDataRepository = cardDataRepository;
        }

        public IReadOnlyDictionary<string, string?> GetPaginationParams() => new Dictionary<string, string?>
        {
            ["cardType"] = CardType,
            ["rarityName"] = RarityName,
            ["setName"] = SetName
        };

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            AvailableRarityNames = _cardDataRepository.DistinctRarityNames;
            AvailableSetNames = _cardDataRepository.DistinctSetNames;

            var criteria = new BrowseSearchCriteria
            {
                CardType = CardType,
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName
            };

            Results = await _cardService.SearchCardsAsync(criteria);
        }
    }
}
