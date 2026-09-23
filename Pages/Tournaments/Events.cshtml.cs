using System.Globalization;
using CardCollector.Data.Models;
using CardCollector.Extensions;
using CardCollector.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments
{
    public sealed class EventsModel : PageModel
    {
        private const int DEFAULT_PAGE_SIZE = 25;

        private static readonly int[] ValidPageSizes = [10, 25, 50, 100];

        private readonly IDeckService _deckService;
        private readonly IEventService _eventService;
        private readonly IFormatService _formatService;

        public EventsModel(IDeckService deckService, IEventService eventService, IFormatService formatService)
        {
            _deckService = deckService;
            _eventService = eventService;
            _formatService = formatService;
        }

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Deck { get; set; }

        public IReadOnlyList<string> DeckNames { get; private set; } = [];

        /// <summary>The decks an event can be pointed at instead of importing a new one.</summary>
        public IReadOnlyList<DeckListItemViewModel> DeckOptions { get; private set; } = [];

        public IReadOnlyList<string> Errors { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public int? FormatID { get; set; }

        public IReadOnlyList<Format> Formats { get; private set; } = [];

        public bool HasActiveFilters =>
            DateFrom is not null
            || DateTo is not null
            || !string.IsNullOrWhiteSpace(Deck)
            || FormatID is not null
            || !string.IsNullOrWhiteSpace(Location)
            || Type is not null;

        [BindProperty]
        public EventInputModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Location { get; set; }

        public IReadOnlyList<string> Locations { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = DEFAULT_PAGE_SIZE;

        public PagedResult<EventListItemViewModel> Results { get; private set; } = new();

        /// <summary>True when a failed save should reopen the Add/Edit modal with the submitted values.</summary>
        public bool ShowEventModal { get; private set; }

        [BindProperty(SupportsGet = true)]
        public EventType? Type { get; set; }

        /// <summary>The active filters as query-string values, without paging.</summary>
        public IReadOnlyDictionary<string, string?> GetFilterParams()
        {
            var values = new Dictionary<string, string?>();
            AddIfPresent(values, "dateFrom", TournamentDisplay.IsoDate(DateFrom));
            AddIfPresent(values, "dateTo", TournamentDisplay.IsoDate(DateTo));
            AddIfPresent(values, "deck", Deck?.Trim());
            AddIfPresent(values, "formatID", FormatID?.ToString(CultureInfo.InvariantCulture));
            AddIfPresent(values, "location", Location?.Trim());
            AddIfPresent(values, "type", Type?.ToString());
            return values;
        }

        /// <summary>The active filters plus the current page, so a POST can send the user back to where they were.</summary>
        public IDictionary<string, string> GetReturnRouteData()
        {
            var values = new Dictionary<string, string>();
            foreach (var (key, value) in GetFilterParams())
            {
                if (value is not null)
                    values[key] = value;
            }

            values["pageNumber"] = PageNumber.ToString(CultureInfo.InvariantCulture);
            values["pageSize"] = PageSize.ToString(CultureInfo.InvariantCulture);
            return values;
        }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
        {
            var deleted = await _eventService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

            if (deleted)
                TempData["Success"] = "Event deleted.";
            else
                TempData["Error"] = "That event no longer exists.";

            return RedirectToPage(null, GetReturnRouteData());
        }

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
        {
            var errors = GetBindingErrors();
            if (errors.Count == 0)
            {
                var result = await SaveAsync(cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    TempData["Success"] = Input.ID == 0 ? "Event added." : "Event updated.";
                    return RedirectToPage(null, GetReturnRouteData());
                }

                errors = [.. result.Errors];
            }

            Errors = errors;
            ShowEventModal = true;
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        private static void AddIfPresent(Dictionary<string, string?> values, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                values[key] = value;
        }

        private Event BuildEvent() =>
            new()
            {
                Date = Input.Date ?? default,
                DeckName = Input.DeckName ?? string.Empty,
                DecklistURL = Input.DecklistURL,
                EventType = Input.EventType ?? default,
                Finish = Input.Finish,
                FinishNote = Input.FinishNote,
                ID = Input.ID,
                Location = Input.Location ?? string.Empty,
                Notes = Input.Notes,
                Players = Input.Players,
                TopCut = Input.TopCut
            };

        private List<string> GetBindingErrors()
        {
            var errors = new List<string>();

            if (ModelState.IsFieldInvalid(nameof(Input), nameof(Input.Date)))
                errors.Add("Date is not a valid date.");
            else if (Input.Date is null)
                errors.Add("Date is required.");

            if (ModelState.IsFieldInvalid(nameof(Input), nameof(Input.EventType)))
                errors.Add("Event type is not valid.");
            else if (Input.EventType is null)
                errors.Add("Event type is required.");

            if (ModelState.IsFieldInvalid(nameof(Input), nameof(Input.Finish)))
                errors.Add("Finish must be a whole number.");

            if (ModelState.IsFieldInvalid(nameof(Input), nameof(Input.Players)))
                errors.Add("Players must be a whole number.");

            return errors;
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            NormalizeParameters();

            Formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);
            DeckNames = await _eventService.GetDeckNamesAsync(cancellationToken).ConfigureAwait(false);
            DeckOptions = await _deckService.GetAllAsync(cancellationToken).ConfigureAwait(false);
            Locations = await _eventService.GetLocationsAsync(cancellationToken).ConfigureAwait(false);

            Results = await SearchAsync(cancellationToken).ConfigureAwait(false);

            // Deleting the last row of the last page would otherwise leave the user on an empty page.
            if (Results.Items.Count == 0 && Results.TotalCount > 0 && PageNumber > Results.TotalPages)
            {
                PageNumber = Results.TotalPages;
                Results = await SearchAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void NormalizeParameters()
        {
            if (PageNumber < 1) PageNumber = 1;
            if (!ValidPageSizes.Contains(PageSize)) PageSize = DEFAULT_PAGE_SIZE;
        }

        private Task<SaveResult> SaveAsync(CancellationToken cancellationToken)
        {
            var tournamentEvent = BuildEvent();
            return tournamentEvent.ID == 0
                ? _eventService.AddAsync(tournamentEvent, cancellationToken)
                : _eventService.UpdateAsync(tournamentEvent, cancellationToken);
        }

        private Task<PagedResult<EventListItemViewModel>> SearchAsync(CancellationToken cancellationToken) =>
            _eventService.SearchAsync(
                new EventSearchCriteria
                {
                    DateFrom = DateFrom,
                    DateTo = DateTo,
                    DeckName = Deck,
                    EventType = Type,
                    FormatID = FormatID,
                    Location = Location,
                    Page = PageNumber,
                    PageSize = PageSize
                },
                cancellationToken);
    }
}
