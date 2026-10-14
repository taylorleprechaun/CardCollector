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

        /// <summary>The collection's status and total quantity of each of the card's printings, by catalog printing ID.</summary>
        public IReadOnlyDictionary<int, (CollectionStatus Status, int TotalQuantity)> CollectionByPrinting { get; private set; }
            = new Dictionary<int, (CollectionStatus, int)>();

        public Card? CurrentCard { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        public bool IsIgnored { get; private set; }

        [BindProperty]
        public int PrintingID { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnURL { get; set; }

        public IReadOnlyList<PreferredVersion> TrackedPrintings { get; private set; } = [];

        public CardModel(ICardService cardService, ICardSetRepository cardSetRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
        }

        public CollectionCompletionStatus? GetCompletionStatus(CollectionStatus status, int totalQuantity, int printingID)
        {
            if (status != CollectionStatus.Owned) return null;

            var tracked = GetTrackedPrinting(printingID);

            if (tracked is not null)
                return totalQuantity >= tracked.DesiredQuantity
                    ? CollectionCompletionStatus.Complete
                    : CollectionCompletionStatus.Incomplete;

            // This particular printing isn't one of the tracked ones — if some other tracked printing for
            // this card is already complete, this copy still counts as "owned," just not the target.
            var anyOtherTrackedComplete = TrackedPrintings.Any(pv =>
                pv.CatalogPrintingID is int trackedID
                && CollectionByPrinting.TryGetValue(trackedID, out var summary)
                && summary.Status == CollectionStatus.Owned
                && summary.TotalQuantity >= pv.DesiredQuantity);

            return anyOtherTrackedComplete ? CollectionCompletionStatus.Owned : CollectionCompletionStatus.Placeholder;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        /// <summary>The tracked printing for the catalog printing, or null when it isn't tracked.</summary>
        public PreferredVersion? GetTrackedPrinting(int printingID) => TrackedPrintings.FirstOrDefault(pv => pv.CatalogPrintingID == printingID);

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
            CollectionByPrinting = entries
                .Where(e => e.CatalogPrintingID is not null)
                .GroupBy(e => e.CatalogPrintingID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Status: g.Any(e => e.Status == CollectionStatus.Owned) ? CollectionStatus.Owned : CollectionStatus.Ordered,
                        TotalQuantity: g.Sum(e => e.Quantity)
                    ));

            TrackedPrintings = await _cardService.GetTrackedPrintingsByCardIDAsync(ID);
            IsIgnored = await _cardService.IsCardIgnoredAsync(ID);
        }

        public async Task<IActionResult> OnPostIgnoreAsync()
        {
            await _cardService.IgnoreCardAsync(CardID);
            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostOrderAsync(
            int quantity = 1, CardCondition? condition = null, CardEdition? edition = null,
            AcquisitionMethod? acquisitionMethod = null,
            DateTime? purchaseDate = null, decimal? purchasePrice = null, decimal? marketPriceAtEntry = null,
            bool setAsPreferred = false)
        {
            await this.WarnIfEditionMismatchAsync(_cardService, PrintingID, edition);

            await _cardService.AddEntryAsync(
                PrintingID, CollectionStatus.Ordered,
                quantity, condition, edition,
                acquisitionMethod,
                purchaseDate, purchasePrice, marketPriceAtEntry);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(PrintingID);

            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostOwnAsync(
            int quantity = 1, CardCondition? condition = null, CardEdition? edition = null,
            AcquisitionMethod? acquisitionMethod = null,
            DateTime? purchaseDate = null, decimal? purchasePrice = null, decimal? marketPriceAtEntry = null,
            bool setAsPreferred = false)
        {
            await this.WarnIfEditionMismatchAsync(_cardService, PrintingID, edition);

            await _cardService.AddEntryAsync(
                PrintingID, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod,
                purchaseDate, purchasePrice, marketPriceAtEntry);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(PrintingID);

            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostRemovePreferredAsync(int preferredVersionID)
        {
            await _cardService.RemoveFromWishlistAsync(preferredVersionID);
            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostSetDesiredQuantityAsync(int preferredVersionID, int desiredQuantity)
        {
            if (preferredVersionID > 0 && desiredQuantity >= 1)
                await _cardService.SetDesiredQuantityAsync(preferredVersionID, desiredQuantity).ConfigureAwait(false);

            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostSetPreferredAsync()
        {
            await _cardService.SavePreferredVersionAsync(PrintingID);
            return RedirectToPage(new { ID, ReturnURL });
        }

        public async Task<IActionResult> OnPostUnignoreAsync()
        {
            await _cardService.UnignoreCardAsync(CardID);
            return RedirectToPage(new { ID, ReturnURL });
        }
    }
}
