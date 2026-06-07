using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class CollectionModel : SearchablePageModel
    {
        private readonly ICardService _cardService;
        private readonly ICardSetRepository _cardSetRepository;
        private readonly ICollectionRepository _collectionRepository;

        protected override ICardService CardService => _cardService;

        public PagedResult<CollectionGroupViewModel> GroupedCards { get; private set; } = new();

        public CollectionModel(ICardService cardService, ICardSetRepository cardSetRepository, ICollectionRepository collectionRepository)
        {
            _cardService = cardService;
            _cardSetRepository = cardSetRepository;
            _collectionRepository = collectionRepository;
        }

        public string GetTCGDate(string setCode) =>
            _cardSetRepository.GetTCGDateBySetCode(setCode) ?? string.Empty;

        public async Task OnGetAsync()
        {
            NormalizeSearchParameters();
            GroupedCards = await _cardService.SearchGroupedOwnedAsync(Query, PageNumber, PageSize);
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
            var added = await _cardService.AddEntryAsync(
                cardID, imageID, setCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod, false,
                purchaseDate, purchasePrice, marketPriceAtEntry, rarityName);

            if (!added)
                TempData["Error"] = "That printing is already in your collection.";
            else if (setAsPreferred)
                await _cardService.SavePreferredVersionAsync(cardID, imageID, setCode, rarityName);

            return RedirectToPage(new { query = Query, pageNumber = PageNumber, pageSize = PageSize });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int entryID)
        {
            await _collectionRepository.DeleteAsync(entryID);
            return RedirectToPage(new { query = Query, pageNumber = PageNumber, pageSize = PageSize });
        }

        public async Task<IActionResult> OnPostEditAsync(
            int entryID, int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice,
            string? rarityName = null)
        {
            var entry = new CollectionEntry
            {
                ID = entryID,
                AcquisitionMethod = acquisitionMethod,
                Condition = condition,
                Edition = edition,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity,
                RarityName = string.IsNullOrWhiteSpace(rarityName) ? null : rarityName
            };

            await _collectionRepository.UpdateAsync(entry);
            return RedirectToPage(new { query = Query, pageNumber = PageNumber, pageSize = PageSize });
        }
    }
}
