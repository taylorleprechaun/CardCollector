namespace CardCollector.Models.Catalog
{
    /// <summary>What the sync rules need to know about the run itself.</summary>
    public sealed class CatalogSyncContext
    {
        /// <summary>Both card sources succeeded, so a card or field missing from them is really missing.</summary>
        public bool CardSourcesSucceeded => YamlYugiSucceeded && YGOProDeckSucceeded;

        /// <summary>True when the catalog was empty at the start of the run; no informational items are recorded.</summary>
        public bool IsBootstrap { get; init; }

        /// <summary>The run's timestamp. Every row the run sees gets exactly this LastSeenUTC, which is how "not seen" is detected.</summary>
        public DateTime NowUTC { get; init; }

        /// <summary>Returns the TCG release date ("yyyy-MM-dd") for a full set code, or null.</summary>
        public Func<string, string?> SetReleaseDateLookup { get; init; } = _ => null;

        public bool TCGPlayerSucceeded { get; init; }

        public bool YamlYugiSucceeded { get; init; }

        public bool YGOProDeckSucceeded { get; init; }
    }
}
