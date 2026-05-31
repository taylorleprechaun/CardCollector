using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class BrowseModel : SearchablePageModel
    {
        private static readonly IReadOnlyList<string> CardTypes = ["Monster", "Spell", "Trap"];

        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardService _cardService;

        [BindProperty(SupportsGet = true)]
        public string? Attribute { get; set; }

        public IReadOnlyList<string> AvailableAttributes { get; private set; } = [];

        public IReadOnlyList<string> AvailableCardTypes { get; } = CardTypes;

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public string? CardType { get; set; }

        public bool HasActiveFilters => !string.IsNullOrWhiteSpace(CardType)
            || !string.IsNullOrWhiteSpace(Attribute)
            || !string.IsNullOrWhiteSpace(RarityName)
            || LevelMin.HasValue
            || LevelMax.HasValue;

        [BindProperty(SupportsGet = true)]
        public int? LevelMax { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? LevelMin { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RarityName { get; set; }

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        protected override ICardService CardService => _cardService;

        public BrowseModel(ICardService cardService, ICardDataRepository cardDataRepository)
        {
            _cardService = cardService;
            _cardDataRepository = cardDataRepository;
        }

        public IDictionary<string, string?> GetPaginationParams() => new Dictionary<string, string?>
        {
            ["attribute"] = Attribute,
            ["cardType"] = CardType,
            ["levelMax"] = LevelMax?.ToString(),
            ["levelMin"] = LevelMin?.ToString(),
            ["rarityName"] = RarityName
        };

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            AvailableAttributes = _cardDataRepository.DistinctAttributes;
            AvailableRarityNames = _cardDataRepository.DistinctRarityNames;

            var criteria = new BrowseSearchCriteria
            {
                Attribute = Attribute,
                CardType = CardType,
                LevelMax = LevelMax,
                LevelMin = LevelMin,
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName
            };

            Results = await _cardService.SearchCardsAsync(criteria);
        }
    }
}
