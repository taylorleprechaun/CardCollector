using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class CardModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;

        [BindProperty]
        public int CardID { get; set; }

        public bool CardNotFound { get; private set; }

        public IReadOnlyDictionary<(string SetCode, string RarityName), (CollectionStatus Status, int TotalQuantity)> CollectionEntriesBySetCode { get; private set; }
            = new Dictionary<(string, string), (CollectionStatus, int)>();

        public Card? CurrentCard { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ImageID { get; set; }

        public PreferredVersion? PreferredVersion { get; private set; }

        [BindProperty]
        public string? RarityName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnURL { get; set; }

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public CardModel(ICardService cardService, ICardSetRepository cardSetRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
        }

        public CollectionCompletionStatus? GetCompletionStatus(CollectionStatus status, int totalQuantity, string setCode, string rarityName)
        {
            if (status != CollectionStatus.Owned) return null;

            var isPreferred = PreferredVersion is not null
                && PreferredVersion.SetCode.Equals(setCode, StringComparison.OrdinalIgnoreCase)
                && (PreferredVersion.RarityName is null || PreferredVersion.RarityName.Equals(rarityName, StringComparison.OrdinalIgnoreCase));

            if (isPreferred)
                return totalQuantity >= CardPrinting.CompleteThreshold
                    ? CollectionCompletionStatus.Complete
                    : CollectionCompletionStatus.Incomplete;

            if (PreferredVersion is not null
                && CollectionEntriesBySetCode.TryGetValue(
                    (PreferredVersion.SetCode, PreferredVersion.RarityName ?? string.Empty),
                    out var preferredSummary)
                && preferredSummary.Status == CollectionStatus.Owned
                && preferredSummary.TotalQuantity >= CardPrinting.CompleteThreshold)
                return CollectionCompletionStatus.Owned;

            return CollectionCompletionStatus.Placeholder;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            if (ID == 0)
            {
                CardNotFound = true;
                return;
            }

            CurrentCard = _cardService.GetCardByID(ID);
            if (CurrentCard is null)
            {
                CardNotFound = true;
                return;
            }

            var entries = await _cardService.GetEntriesByCardIDAsync(ID);
            CollectionEntriesBySetCode = entries
                .GroupBy(e => (e.SetCode, e.RarityName ?? string.Empty))
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Status: g.Any(e => e.Status == CollectionStatus.Owned) ? CollectionStatus.Owned : CollectionStatus.Ordered,
                        TotalQuantity: g.Sum(e => e.Quantity)
                    ));

            PreferredVersion = await _cardService.GetPreferredVersionByCardIDAsync(ID);
        }

        public async Task<IActionResult> OnPostOrderAsync(
            int quantity = 1, CardCondition? condition = null, CardEdition? edition = null,
            AcquisitionMethod? acquisitionMethod = null,
            DateTime? purchaseDate = null, decimal? purchasePrice = null, decimal? marketPriceAtEntry = null,
            bool setAsPreferred = false, string? rarityName = null)
        {
            await this.WarnIfEditionMismatchAsync(_cardService, CardID, SetCode, rarityName, edition);

            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Ordered,
                quantity, condition, edition,
                acquisitionMethod,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode, rarityName);

            return RedirectToPage(new { ID, ImageID, ReturnURL });
        }

        public async Task<IActionResult> OnPostOwnAsync(
            int quantity = 1, CardCondition? condition = null, CardEdition? edition = null,
            AcquisitionMethod? acquisitionMethod = null,
            DateTime? purchaseDate = null, decimal? purchasePrice = null, decimal? marketPriceAtEntry = null,
            bool setAsPreferred = false, string? rarityName = null)
        {
            await this.WarnIfEditionMismatchAsync(_cardService, CardID, SetCode, rarityName, edition);

            await _cardService.AddEntryAsync(
                CardID, ImageID, SetCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode, rarityName);

            return RedirectToPage(new { ID, ImageID, ReturnURL });
        }

        public async Task<IActionResult> OnPostRemovePreferredAsync()
        {
            await _cardService.RemoveFromWishlistAsync(ImageID);
            return RedirectToPage(new { ID, ImageID, ReturnURL });
        }

        public async Task<IActionResult> OnPostSetPreferredAsync()
        {
            await _cardService.SavePreferredVersionAsync(CardID, ImageID, SetCode, RarityName);
            return RedirectToPage(new { ID, ImageID, ReturnURL });
        }
    }
}
