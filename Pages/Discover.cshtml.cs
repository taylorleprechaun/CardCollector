using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages
{
    public class DiscoverModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly ICollectionRepository _collectionRepository;

        public Card? CurrentCard { get; private set; }
        public Image? CurrentImage { get; private set; }
        public bool IsComplete { get; private set; }

        [BindProperty]
        public int CardID { get; set; }

        [BindProperty]
        public int ImageID { get; set; }

        [BindProperty]
        public bool IsPlaceholder { get; set; }

        [BindProperty]
        public DateTime? PurchaseDate { get; set; }

        [BindProperty]
        public decimal? PurchasePrice { get; set; }

        [BindProperty]
        public AcquisitionMethod? SelectedAcquisitionMethod { get; set; }

        [BindProperty]
        public CardCondition? SelectedCondition { get; set; }

        [BindProperty]
        public CardEdition? SelectedEdition { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        [BindProperty]
        public string SetCode { get; set; } = string.Empty;

        public DiscoverModel(ICardService cardService, ICollectionRepository collectionRepository)
        {
            _cardService = cardService;
            _collectionRepository = collectionRepository;
        }

        public async Task OnGetAsync()
        {
            var result = await _cardService.GetRandomUncollectedAsync();
            if (result is null)
            {
                IsComplete = true;
                return;
            }

            CurrentCard = result.Value.Card;
            CurrentImage = result.Value.Image;
        }

        public async Task<IActionResult> OnPostOrderAsync() =>
            await SaveEntryAsync(CollectionStatus.Ordered);

        public async Task<IActionResult> OnPostOwnAsync() =>
            await SaveEntryAsync(CollectionStatus.Owned);

        private async Task<IActionResult> SaveEntryAsync(CollectionStatus status)
        {
            if (await _collectionRepository.ExistsAsync(ImageID, SetCode))
                return RedirectToPage();

            var entry = new CollectionEntry
            {
                AcquisitionMethod = SelectedAcquisitionMethod,
                CardID = CardID,
                Condition = SelectedCondition,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                Edition = SelectedEdition,
                ImageID = ImageID,
                IsPlaceholder = status == CollectionStatus.Owned && IsPlaceholder,
                PurchaseDate = PurchaseDate,
                PurchasePrice = PurchasePrice,
                Quantity = Quantity < 1 ? 1 : Quantity,
                SetCode = SetCode,
                Status = status
            };

            await _collectionRepository.AddAsync(entry);
            return RedirectToPage();
        }
    }
}
