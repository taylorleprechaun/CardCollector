using CardCollector.Extensions;
using CardCollector.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace CardCollector.Pages;

public class ExportModel(ICardService cardService) : PageModel
{
    private readonly ICardService _cardService = cardService;

    public IActionResult OnGet() => RedirectToPage("/Collection");

    public async Task<IActionResult> OnGetCollectionAsync()
    {
        var entries = await _cardService.GetEnrichedOwnedAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Card Name,Set Code,Set Name,Rarity,Condition,Edition,Acquisition Method,Quantity,Purchase Price,Market Price At Entry,Purchase Date,Is Placeholder,Date Added");

        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(",",
                Csv(e.CardName),
                Csv(e.SetCode),
                Csv(e.SetName),
                Csv(!string.IsNullOrEmpty(e.RarityName) ? e.RarityName : e.RarityCode),
                Csv(e.Condition?.GetDisplayName()),
                Csv(e.Edition?.GetDisplayName()),
                Csv(e.AcquisitionMethod?.GetDisplayName()),
                e.Quantity.ToString(),
                e.PurchasePrice?.ToString("F2") ?? string.Empty,
                e.MarketPriceAtEntry?.ToString("F2") ?? string.Empty,
                e.PurchaseDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                e.IsPlaceholder ? "Yes" : "No",
                e.DateCreated.ToString("yyyy-MM-dd")));
        }

        return CsvFile(sb, $"collection-{DateTime.Today:yyyy-MM-dd}.csv");
    }

    public async Task<IActionResult> OnGetWishlistAsync()
    {
        var items = await _cardService.GetWishlistAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Card Name,Set Code,Set Name,Rarity,Market Price");

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                Csv(item.CardName),
                Csv(item.SetCode),
                Csv(item.SetName),
                Csv(item.RarityName),
                item.Price?.ToString("F2") ?? string.Empty));
        }

        return CsvFile(sb, $"wishlist-{DateTime.Today:yyyy-MM-dd}.csv");
    }

    private FileContentResult CsvFile(StringBuilder sb, string filename)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", filename);
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
