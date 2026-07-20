using CardCollector.Data.Models;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public sealed class OrdersModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IRazorPartialRenderer _razorPartialRenderer;

        public IReadOnlyList<EditionAuditEntryViewModel> Orders { get; private set; } = [];

        public OrdersModel(ICardService cardService, ICollectionRepository collectionRepository, IRazorPartialRenderer razorPartialRenderer)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
            _razorPartialRenderer = razorPartialRenderer;
        }

        public async Task OnGetAsync()
        {
            Orders = (await _cardService.GetEnrichedOrdersAsync()).ToList();
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
            DateTime? purchaseDate, decimal? purchasePrice,
            decimal? marketPriceAtEntry, string? rarityName = null)
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

            if (!IsAjaxRequest())
                return RedirectToPage();

            var orders = await _cardService.GetEnrichedOrdersAsync();
            var match = orders.FirstOrDefault(o => o.EntryID == entryID);
            if (match is null)
                return Content(string.Empty, "text/html");

            var html = await _razorPartialRenderer.RenderPartialAsync(this, "_OrderRow", match);
            return Content(html, "text/html");
        }

        public async Task<IActionResult> OnPostMarkOwnedAsync(int entryID, int quantity)
        {
            await _collectionRepository.UpdateStatusAsync(entryID, CollectionStatus.Owned, quantity < 1 ? 1 : quantity);
            return RedirectToPage();
        }

        private bool IsAjaxRequest() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    }
}
