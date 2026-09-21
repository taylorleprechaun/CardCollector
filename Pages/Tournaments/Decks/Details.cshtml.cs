using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments.Decks
{
    public sealed class DetailsModel : PageModel
    {
        private readonly IDeckService _deckService;

        public DetailsModel(IDeckService deckService)
        {
            _deckService = deckService;
        }

        public DeckDetailViewModel? Detail { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        /// <summary>Where a card's page sends the user back to.</summary>
        public string ReturnURL => $"/Tournaments/Decks/Details?id={ID}";

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            Detail = await _deckService.GetAsync(ID, cancellationToken).ConfigureAwait(false);
            if (Detail is null)
                return NotFound();

            return Page();
        }
    }
}
