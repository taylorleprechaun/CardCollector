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

        public IReadOnlyList<OrderEntryViewModel> Orders { get; private set; } = [];

        public OrdersModel(ICardService cardService, ICollectionRepository collectionRepository)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
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

        public async Task<IActionResult> OnPostMarkOwnedAsync(int entryID, int quantity)
        {
            await _collectionRepository.UpdateStatusAsync(entryID, CollectionStatus.Owned, quantity < 1 ? 1 : quantity);
            return RedirectToPage();
        }
    }
}
