using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class CollectionModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly ICollectionRepository _collectionRepository;

        public IEnumerable<CollectionGroupViewModel> GroupedCards { get; private set; } = [];

        public CollectionModel(ICardService cardService, ICollectionRepository collectionRepository)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
        }

        public async Task OnGetAsync()
        {
            GroupedCards = await _cardService.GetGroupedOwnedAsync();
        }

        public async Task<IActionResult> OnPostAddPurchaseAsync(
            int cardID, int imageID, string setCode,
            int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice)
        {
            await _cardService.AddEntryAsync(
                cardID, imageID, setCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod, isPlaceholder,
                purchaseDate, purchasePrice);
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
            bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice)
        {
            var entry = new CollectionEntry
            {
                ID = entryID,
                AcquisitionMethod = acquisitionMethod,
                Condition = condition,
                Edition = edition,
                IsPlaceholder = isPlaceholder,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity
            };

            await _collectionRepository.UpdateAsync(entry);
            return RedirectToPage();
        }
    }
}
