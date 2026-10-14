namespace CardCollector.Data.Models
{
    /// <summary>
    /// A user row that points at a catalog printing both by <see cref="CatalogPrintingID"/> and by the older
    /// (card, set code, rarity, variant) columns. Temporary: the columns go away once every page reads the ID.
    /// </summary>
    public interface IPrintingKeyedRow
    {
        int CardID { get; set; }

        int? CatalogPrintingID { get; set; }

        string? PrintVariant { get; set; }

        string? RarityName { get; set; }

        string SetCode { get; set; }
    }
}
