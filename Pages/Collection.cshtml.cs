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

        private static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        public PagedResult<CollectionGroupViewModel> GroupedCards { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25;

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        public CollectionModel(ICardService cardService, ICollectionRepository collectionRepository)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
        }

        public async Task OnGetAsync()
        {
            if (PageNumber < 1)
                PageNumber = 1;

            if (!ValidPageSizes.Contains(PageSize))
                PageSize = 25;

            GroupedCards = await _cardService.SearchGroupedOwnedAsync(Query, PageNumber, PageSize);
        }

        public async Task<IActionResult> OnPostAddPurchaseAsync(
            int cardID, int imageID, string setCode,
            int quantity,
            CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice)
        {
            await _cardService.AddEntryAsync(
                cardID, imageID, setCode, CollectionStatus.Owned,
                quantity, condition, edition,
                acquisitionMethod, false,
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
            DateTime? purchaseDate, decimal? purchasePrice)
        {
            var entry = new CollectionEntry
            {
                ID = entryID,
                AcquisitionMethod = acquisitionMethod,
                Condition = condition,
                Edition = edition,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity
            };

            await _collectionRepository.UpdateAsync(entry);
            return RedirectToPage();
        }
    }
}
