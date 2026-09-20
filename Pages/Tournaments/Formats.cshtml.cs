using CardCollector.Data.Models;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Tournaments
{
    public sealed class FormatsModel : PageModel
    {
        private readonly IFormatService _formatService;

        public FormatsModel(IFormatService formatService)
        {
            _formatService = formatService;
        }

        /// <summary>The format whose date range contains today, or null when none does.</summary>
        public int? CurrentFormatID { get; private set; }

        public IReadOnlyList<string> Errors { get; private set; } = [];

        public IReadOnlyList<Format> Formats { get; private set; } = [];

        [BindProperty]
        public FormatInputModel Input { get; set; } = new();

        /// <summary>True when a failed save should reopen the Add/Edit modal with the submitted values.</summary>
        public bool ShowFormModal { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadFormatsAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
        {
            var deleted = await _formatService.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

            if (deleted)
                TempData["Success"] = "Format deleted.";
            else
                TempData["Error"] = "That format no longer exists.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
        {
            var errors = GetBindingErrors();
            if (errors.Count == 0)
            {
                var result = await SaveAsync(cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    TempData["Success"] = Input.ID == 0 ? "Format added." : "Format updated.";
                    return RedirectToPage();
                }

                errors = [.. result.Errors];
            }

            Errors = errors;
            ShowFormModal = true;
            await LoadFormatsAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        private Format BuildFormat() =>
            new()
            {
                EndDate = Input.IsOngoing ? null : Input.EndDate,
                ID = Input.ID,
                Name = Input.Name ?? string.Empty,
                Notes = Input.Notes,
                StartDate = Input.StartDate ?? default,
                Strategies = (Input.Strategies ?? [])
                    .Select(s => new FormatStrategy { Name = s ?? string.Empty })
                    .ToList()
            };

        private List<string> GetBindingErrors()
        {
            var errors = new List<string>();

            var startState = ModelState.GetFieldValidationState($"{nameof(Input)}.{nameof(Input.StartDate)}");
            if (startState == ModelValidationState.Invalid)
                errors.Add("Start date is not a valid date.");
            else if (Input.StartDate is null)
                errors.Add("Start date is required.");

            if (ModelState.GetFieldValidationState($"{nameof(Input)}.{nameof(Input.EndDate)}") == ModelValidationState.Invalid)
                errors.Add("End date is not a valid date.");

            return errors;
        }

        private async Task LoadFormatsAsync(CancellationToken cancellationToken)
        {
            Formats = await _formatService.GetAllAsync(cancellationToken).ConfigureAwait(false);
            CurrentFormatID = FormatRules.FindForDate(Formats, DateOnly.FromDateTime(DateTime.Today))?.ID;
        }

        private Task<FormatSaveResult> SaveAsync(CancellationToken cancellationToken)
        {
            var format = BuildFormat();
            return format.ID == 0
                ? _formatService.AddAsync(format, cancellationToken)
                : _formatService.UpdateAsync(format, cancellationToken);
        }
    }
}
