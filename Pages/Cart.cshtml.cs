using CardCollector.Extensions;
using CardCollector.Repository;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace CardCollector.Pages
{
    public sealed class CartModel : PageModel
    {
        private readonly ICardService _cardService;
        private readonly IPendingOrderRepository _pendingOrderRepository;

        public int CardCount => Lines.Sum(l => l.Quantity);

        public IReadOnlyList<PendingOrderLineViewModel> Lines { get; private set; } = [];

        public CartModel(ICardService cardService, IPendingOrderRepository pendingOrderRepository)
        {
            _cardService = cardService;
            _pendingOrderRepository = pendingOrderRepository;
        }

        public async Task OnGetAsync()
        {
            Lines = await _cardService.GetPendingCartAsync().ConfigureAwait(false);
        }

        public async Task<IActionResult> OnPostRemoveAsync(int id)
        {
            await _pendingOrderRepository.DeleteAsync(id).ConfigureAwait(false);

            if (!Request.IsAjaxRequest())
                return RedirectToPage();

            var (cartCount, cartTotal) = await _cardService.GetCartSummaryAsync().ConfigureAwait(false);
            Response.Headers["X-Cart-Count"] = cartCount.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Cart-Total"] = cartTotal.ToString(CultureInfo.InvariantCulture);
            return new OkResult();
        }

        public async Task<IActionResult> OnPostSubmitAllAsync(IReadOnlyList<CartLineOverride>? lines)
        {
            if (lines is null || lines.Select(l => l.PendingOrderLineID).Distinct().Count() != lines.Count)
                return BadRequest();

            if (lines.Any(l => !l.PurchasePrice.HasValue))
            {
                TempData["Warning"] = "Enter a price for every cart line before submitting.";
                return RedirectToPage();
            }

            var (count, total, editionWarnings) = await _cardService.SubmitCartAsync(lines).ConfigureAwait(false);

            if (editionWarnings.Count > 0)
                TempData["Warning"] = string.Join(" ", editionWarnings);

            TempData["Success"] = count == 0
                ? "Your cart is empty — nothing to submit."
                : $"Added {count} order{(count == 1 ? "" : "s")} for ${total:F2} to Orders.";

            return RedirectToPage("/Orders");
        }

        public async Task<IActionResult> OnPostUpdateLineAsync(CartLineOverride? line)
        {
            if (line is null)
                return BadRequest();

            var updated = await _cardService.UpdateCartLineAsync(line).ConfigureAwait(false);
            return updated ? new OkResult() : NotFound();
        }
    }
}
