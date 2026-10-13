namespace CardCollector.Models.Catalog
{
    /// <summary>One TCGPlayer product (a sellable printing, all editions together) from the tcgcsv catalog.</summary>
    public sealed record SourceTCGProduct(int ProductID, int GroupID, string CardName, string SetCode, string RarityName, string? PrintVariant)
    {
        /// <summary>The fields a listing edit would change, used to notice when TCGPlayer edits a linked product.</summary>
        public string Summary => $"{CardName} | {SetCode} | {RarityName} | {PrintVariant}";
    }
}
