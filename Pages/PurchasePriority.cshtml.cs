using System.Globalization;
using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class PurchasePriorityModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly IPendingOrderRepository _pendingOrderRepository;

        public PurchasePlanViewModel FullPlan { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? MaxCards { get; set; } = 100;

        [BindProperty(SupportsGet = true)]
        public decimal? MaxPricePerCard { get; set; } = 10;

        public string MassEntryText { get; private set; } = string.Empty;

        public PagedResult<PurchasePriorityCandidateViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public decimal? TotalBudget { get; set; } = 100;

        protected override ICardService CardService => _cardService;

        public PurchasePriorityModel(ICardService cardService, IPendingOrderRepository pendingOrderRepository)
        {
            _cardService = cardService;
            _pendingOrderRepository = pendingOrderRepository;
        }

        public override IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>
            {
                ["totalBudget"] = TotalBudget?.ToString(CultureInfo.InvariantCulture),
                ["maxCards"] = MaxCards?.ToString(CultureInfo.InvariantCulture),
                ["maxPricePerCard"] = MaxPricePerCard?.ToString(CultureInfo.InvariantCulture)
            };

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            if (TotalBudget is <= 0) TotalBudget = null;
            if (MaxCards is <= 0) MaxCards = null;
            if (MaxPricePerCard is <= 0) MaxPricePerCard = null;

            FullPlan = await _cardService.GetPurchasePlanAsync(TotalBudget, MaxCards, MaxPricePerCard).ConfigureAwait(false);
            MassEntryText = string.Join('\n', FullPlan.Items.Select(i =>
                $"{i.QuantityNeeded} {i.CardName} [{i.SetCode.ToTCGPlayerSetCode()}]"));

            var filtered = string.IsNullOrWhiteSpace(Query)
                ? FullPlan.Items
                : FullPlan.Items.Where(i =>
                    i.CardName.Contains(Query, StringComparison.OrdinalIgnoreCase)
                    || i.SetName.Contains(Query, StringComparison.OrdinalIgnoreCase)
                    || i.RarityName.Contains(Query, StringComparison.OrdinalIgnoreCase)).ToList();

            Results = new PagedResult<PurchasePriorityCandidateViewModel>
            {
                Items = filtered.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList(),
                Page = PageNumber,
                PageSize = PageSize,
                TotalCount = filtered.Count
            };
        }

        public async Task<IActionResult> OnPostAddToCartAsync(int cardID, int imageID, string setCode, string? rarityName, IReadOnlyList<PurchaseLineInput> lines)
        {
            var entries = lines.Select(l => new PendingOrderLine
            {
                CardID = cardID,
                ImageID = imageID,
                SetCode = setCode,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName,
                Condition = l.Condition,
                Edition = l.Edition,
                AcquisitionMethod = AcquisitionMethod.Purchased,
                MarketPriceAtEntry = l.MarketPriceAtEntry,
                PurchaseDate = l.PurchaseDate,
                PurchasePrice = l.PurchasePrice,
                Quantity = l.Quantity < 1 ? 1 : l.Quantity,
                DateCreated = DateTime.UtcNow
            });

            await _pendingOrderRepository.AddRangeAsync(entries).ConfigureAwait(false);
            var (count, total) = await _pendingOrderRepository.GetSummaryAsync().ConfigureAwait(false);

            return new JsonResult(new { count, total });
        }
    }
}
