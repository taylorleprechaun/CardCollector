namespace CardCollector.DTO
{
    /// <summary>One row of <c>Config/RarityCorrections.json</c>: a (set code, rarity) a source gets wrong and the rarity it should read as.</summary>
    public sealed class RarityCorrectionEntry
    {
        public string? NewRarityName { get; set; }

        public string? OldRarityName { get; set; }

        public string? SetCode { get; set; }
    }
}
