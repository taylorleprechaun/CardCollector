using CardCollector.ViewModels;

namespace CardCollector.Models.Catalog
{
    /// <summary>Where an unmapped product can be placed.</summary>
    /// <param name="Printings">Existing printings it can be mapped onto, best match first.</param>
    /// <param name="Cards">Cards a missing printing can be created on.</param>
    /// <param name="SetCode">The set code the item reports; null when it reports none.</param>
    /// <param name="SetCodeInCatalog">True when an active printing has that set code, so <paramref name="Printings"/> are all under it.</param>
    /// <param name="HasExactMatch">True when one of <paramref name="Printings"/> has the reported set code, rarity, and variant.</param>
    public sealed record CatalogReviewPlacement(IReadOnlyList<CatalogReviewOption> Printings, IReadOnlyList<CatalogReviewOption> Cards, string? SetCode, bool SetCodeInCatalog, bool HasExactMatch = false);
}
