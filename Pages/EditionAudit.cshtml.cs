using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class EditionAuditModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICollectionRepository _collectionRepository;

        public IReadOnlyList<string> AvailableRarityNames { get; private set; } = [];

        public IReadOnlyList<string> AvailableSetNames { get; private set; } = [];

        protected override ICardService CardService => _cardService;

        [BindProperty(SupportsGet = true)]
        public EditionAuditCategory? Category { get; set; }

        public PagedResult<EditionAuditResult> Results { get; private set; } = new();

        public override int ActiveFilterCount => base.ActiveFilterCount + (Category.HasValue ? 1 : 0);

        public override bool HasActiveFilters => base.HasActiveFilters || Category.HasValue;

        public EditionAuditModel(
            ICardDataRepository cardDataRepository,
            ICardService cardService,
            ICollectionRepository collectionRepository)
        {
            _cardDataRepository = cardDataRepository;
            _cardService = cardService;
            _collectionRepository = collectionRepository;
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams()
        {
            var dict = new Dictionary<string, string?>(base.GetPaginationParams())
            {
                ["category"] = Category?.ToString()
            };
            return dict;
        }

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();

            var setCodes = await _collectionRepository.GetDistinctSetCodesAsync().ConfigureAwait(false);
            var setNamesByCode = _cardDataRepository.GetSetNamesByCode();
            AvailableSetNames = setCodes
                .Select(c => setNamesByCode.TryGetValue(c, out var n) ? n : c)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            AvailableRarityNames = await _collectionRepository.GetDistinctRarityNamesAsync().ConfigureAwait(false);

            var criteria = new EditionAuditSearchCriteria
            {
                CardType = CardType,
                Category = Category,
                Page = PageNumber,
                PageSize = PageSize,
                Query = Query,
                RarityName = RarityName,
                SetName = SetName
            };

            Results = await _cardService.SearchEditionAuditAsync(criteria).ConfigureAwait(false);
        }
    }
}
