namespace CardCollector.Data.Models.Catalog
{
    public static class CatalogReviewItemKindExtensions
    {
        private static readonly IReadOnlySet<CatalogSource> AllSources = new HashSet<CatalogSource>
        {
            CatalogSource.TCGPlayer,
            CatalogSource.YamlYugi,
            CatalogSource.YGOProDeck
        };

        private static readonly IReadOnlySet<CatalogSource> CardSources = new HashSet<CatalogSource>
        {
            CatalogSource.YamlYugi,
            CatalogSource.YGOProDeck
        };

        private static readonly IReadOnlySet<CatalogSource> NoSources = new HashSet<CatalogSource>();

        private static readonly IReadOnlySet<CatalogSource> TCGPlayerSource = new HashSet<CatalogSource>
        {
            CatalogSource.TCGPlayer
        };

        /// <summary>
        /// True when the item waits on a decision before the catalog changes; false when it only records a change the
        /// sync already applied.
        /// </summary>
        public static bool IsActionable(this CatalogReviewItemKind kind) => kind switch
        {
            CatalogReviewItemKind.AliasAdded => false,
            CatalogReviewItemKind.CardAdded => false,
            CatalogReviewItemKind.CardTextChanged => false,
            CatalogReviewItemKind.LegacyDismissalUnmapped => false,
            CatalogReviewItemKind.LegacyRowAutoMapped => false,
            CatalogReviewItemKind.LegacyRowUnmapped => false,
            CatalogReviewItemKind.PasscodeAssigned => false,
            CatalogReviewItemKind.PrintingAdded => false,
            CatalogReviewItemKind.TCGProductChanged => false,
            _ => true
        };

        /// <summary>
        /// True for the kinds the one-time move from the pre-catalog database created. Nothing creates them any more; the
        /// inbox keeps them as history, and an open one can only be dismissed.
        /// </summary>
        public static bool IsLegacy(this CatalogReviewItemKind kind) =>
            kind is CatalogReviewItemKind.LegacyDismissalUnmapped
                or CatalogReviewItemKind.LegacyRowAutoMapped
                or CatalogReviewItemKind.LegacyRowUnmapped;

        /// <summary>
        /// The sources that must all succeed in a run before an open item of this kind that the run no longer detects
        /// counts as resolved by the source. Empty means the kind never resolves itself.
        /// </summary>
        public static IReadOnlySet<CatalogSource> SourcesRequiredToAutoResolve(this CatalogReviewItemKind kind) => kind switch
        {
            CatalogReviewItemKind.CardMatchConflict => CardSources,
            CatalogReviewItemKind.CardNameChanged => CardSources,
            CatalogReviewItemKind.CardNotSeen => CardSources,
            CatalogReviewItemKind.PasscodeChanged => CardSources,
            CatalogReviewItemKind.PrintingNotSeen => AllSources,
            CatalogReviewItemKind.TCGListingChanged => TCGPlayerSource,
            CatalogReviewItemKind.UnmappedTCGProduct => TCGPlayerSource,
            _ => NoSources
        };
    }
}
