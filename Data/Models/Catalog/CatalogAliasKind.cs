namespace CardCollector.Data.Models.Catalog
{
    /// <summary>What an outside identifier recorded against a catalog card is.</summary>
    public enum CatalogAliasKind
    {
        /// <summary>A YGOProDeck artwork id other than the card's own id (one per alternate artwork).</summary>
        Artwork,

        /// <summary>The temporary id YGOProDeck used before the card had a real passcode.</summary>
        BetaID,

        /// <summary>The id yaml-yugi records for a card that has no real passcode (e.g. the Egyptian God cards).</summary>
        FakePassword,

        /// <summary>The card's passcode as yaml-yugi reports it.</summary>
        Passcode,

        /// <summary>YGOProDeck's primary id for the card. Usually the passcode, but not always.</summary>
        YGOProDeckID
    }
}
