using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments.Decks
{
    public sealed class DetailsModel : PageModel
    {
        private readonly IDeckLegalityService _deckLegalityService;
        private readonly IDeckService _deckService;

        public DeckDetailViewModel? Detail { get; private set; }

        /// <summary>Which of the deck's events the At event tab resolves its list from.</summary>
        [BindProperty(SupportsGet = true)]
        public int? EventID { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        public DeckLegalityViewModel? Legality { get; private set; }

        /// <summary>Overrides the active tab's list.</summary>
        [BindProperty(SupportsGet = true)]
        public DateOnly? ListDate { get; set; }

        /// <summary>Where a card's page sends the user back to.</summary>
        public string ReturnURL => $"/Tournaments/Decks/Details?id={ID}";

        [BindProperty(SupportsGet = true)]
        public DeckLegalityView? View { get; set; }

        public DetailsModel(IDeckLegalityService deckLegalityService, IDeckService deckService)
        {
            _deckLegalityService = deckLegalityService;
            _deckService = deckService;
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            Detail = await _deckService.GetAsync(ID, cancellationToken).ConfigureAwait(false);
            if (Detail is null)
                return NotFound();

            Legality = await _deckLegalityService.GetAsync(Detail, View, EventID, ListDate, cancellationToken).ConfigureAwait(false);

            return Page();
        }
    }
}
