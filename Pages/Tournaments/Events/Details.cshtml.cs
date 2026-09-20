using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments.Events
{
    public sealed class DetailsModel : PageModel
    {
        private readonly IEventService _eventService;

        public DetailsModel(IEventService eventService)
        {
            _eventService = eventService;
        }

        public EventDetailViewModel? Detail { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ID { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            Detail = await _eventService.GetAsync(ID, cancellationToken).ConfigureAwait(false);
            if (Detail is null)
                return NotFound();

            return Page();
        }
    }
}
