using System.Globalization;
using CardCollector.Extensions;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CardCollector.Pages
{
    public sealed class BuyListModel : SearchablePageModel
    {
        private readonly ICardService _cardService;

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

        public BuyListModel(ICardService cardService)
        {
            _cardService = cardService;
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

        public async Task<IActionResult> OnPostAddToCartAsync(int cardID, int imageID, string setCode, string? rarityName, int quantity, decimal? marketPrice)
        {
            if (cardID <= 0 || imageID <= 0 || string.IsNullOrWhiteSpace(setCode))
                return BadRequest();

            var (count, total, cartQuantity) = await _cardService.AddToCartAsync(
                cardID, imageID, setCode, rarityName, quantity, marketPrice).ConfigureAwait(false);

            return new JsonResult(new { count, total, cartQuantity });
        }
    }
}
