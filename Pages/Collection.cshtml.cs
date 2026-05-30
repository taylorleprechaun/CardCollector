using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class CollectionModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly ICollectionRepository _collectionRepository;

        public PagedResult<CollectionGroupViewModel> GroupedCards { get; private set; } = new();

        public CollectionModel(ICardService cardService, ICollectionRepository collectionRepository)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
        }

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            GroupedCards = await _cardService.SearchGroupedOwnedAsync(Query, PageNumber, PageSize);
        }

        public IActionResult OnGetAutocomplete(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new JsonResult(Array.Empty<string>());

            return new JsonResult(_cardService.GetCardNameSuggestions(q));
        }

        public async Task<IActionResult> OnPostAddPurchaseAsync(
            int cardID, int imageID, string setCode,
            int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry,
            bool setAsPreferred = false,
            string? rarityName = null)
        {
            await _cardService.AddEntryAsync(
                cardID, imageID, setCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod, false,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(cardID, imageID, setCode);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int entryID)
        {
            await _collectionRepository.DeleteAsync(entryID);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync(
            int entryID, int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry,
            string? rarityName = null)
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
                Quantity = quantity < 1 ? 1 : quantity,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName
            };

            await _collectionRepository.UpdateAsync(entry);
            return RedirectToPage();
        }
    }
}
