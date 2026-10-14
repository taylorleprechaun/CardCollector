using CardCollector.Data.Models.Catalog;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Catalog
{
    public sealed class CardsModel : PageModel
    {
        private readonly ICatalogAdminService _catalogAdminService;

        public CardsModel(ICatalogAdminService catalogAdminService)
        {
            _catalogAdminService = catalogAdminService;
        }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = Paging.DEFAULT_PAGE_SIZE;

        [BindProperty(SupportsGet = true, Name = "q")]
        public string? Query { get; set; }

        public PagedResult<CatalogCardRowViewModel> Results { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public CatalogStatus? Status { get; set; }

        public IReadOnlyDictionary<string, string?> GetPaginationParams() =>
            new Dictionary<string, string?>
            {
                ["q"] = Query,
                ["status"] = Status?.ToString()
            };

        public async Task OnGetAsync()
        {
            PageNumber = Paging.ClampPage(PageNumber);
            PageSize = Paging.NormalizePageSize(PageSize);

            Results = await _catalogAdminService
                .SearchCardsAsync(new CatalogCardSearchCriteria { Page = PageNumber, PageSize = PageSize, Query = Query, Status = Status })
                .ConfigureAwait(false);
        }
    }
}
