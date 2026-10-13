namespace CardCollector.Models.Catalog
{
    /// <summary>One (set code, rarity) a card source lists for a card, after rarity normalization and correction.</summary>
    public sealed record SourcePrinting(string SetCode, string? SetName, string RarityName);
}
