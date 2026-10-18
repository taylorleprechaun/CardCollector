using CardCollector.DTO;

namespace CardCollector.Models.Catalog
{
    /// <summary>A catalog printing as the app reads it, together with the card it belongs to.</summary>
    /// <param name="Card">The card.</param>
    /// <param name="Printing">One of the card's <see cref="Card.CardSets"/>; its <see cref="Set.PrintingID"/> is the catalog printing ID.</param>
    public sealed record CatalogPrintingRef(Card Card, Set Printing);
}
