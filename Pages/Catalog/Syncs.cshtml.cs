using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardCollector.Pages.Catalog
{
    public sealed class SyncsModel : PageModel
    {
        private readonly ICatalogAdminService _catalogAdminService;

        public SyncsModel(ICatalogAdminService catalogAdminService)
        {
            _catalogAdminService = catalogAdminService;
        }

        public CatalogSyncHistoryViewModel History { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = Paging.DEFAULT_PAGE_SIZE;

        public async Task OnGetAsync()
        {
            PageNumber = Paging.ClampPage(PageNumber);
            PageSize = Paging.NormalizePageSize(PageSize);

            History = await _catalogAdminService.GetSyncHistoryAsync(PageNumber, PageSize).ConfigureAwait(false);
        }
    }
}
