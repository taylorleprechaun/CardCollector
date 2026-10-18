namespace CardCollector.Models.Legacy
{
    /// <summary>The catalog, indexed for mapping legacy rows: which cards each old card id may mean, and the printings per set code.</summary>
    public sealed class LegacyMappingIndex
    {
        /// <summary>
        /// Every passcode or source id, mapped to the catalog cards that have carried it, strongest claim first. A legacy card
        /// id can be an alias of more than one card (yaml-yugi and YGOProDeck disagree on some passcodes).
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<int>> CardIDsByAlias { get; init; } = new Dictionary<int, IReadOnlyList<int>>();

        /// <summary>Active printings grouped by set code, case-insensitively.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<LegacyPrintingTarget>> PrintingsBySetCode { get; init; } =
            new Dictionary<string, IReadOnlyList<LegacyPrintingTarget>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The known-bad (set code, rarity) table, applied to legacy rows the same way the sync applies it to sources.</summary>
        public IReadOnlyDictionary<(string SetCode, string RarityName), string> RarityCorrections { get; init; } =
            new Dictionary<(string, string), string>();
    }
}
