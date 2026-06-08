using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;

namespace CardCollector.Pages
{
    public sealed class BrowseModel : SearchablePageModel
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICardService _cardService;

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        protected override ICardService CardService => _cardService;

        public PagedResult<CardListItemViewModel> Results { get; private set; } = new();

        public BrowseModel(ICardService cardService, ICardDataRepository cardDataRepository)
        {
            _cardService = cardService;
            _cardDataRepository = cardDataRepository;
        }

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
