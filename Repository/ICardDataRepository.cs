using CardCollector.DTO;
using CardCollector.Models.Catalog;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides read-only access to the card catalog. Card ids are catalog ids, which never change; passcodes are looked up
    /// through <see cref="GetCardByPasscode"/>.
    /// </summary>
    public interface ICardDataRepository
    {
        /// <summary>
        /// All distinct rarity name strings present in the card data.
        /// </summary>
        IReadOnlyList<string> DistinctRarityNames { get; }

        /// <summary>
        /// All distinct set names from browseable cards, sorted.
        /// </summary>
        IReadOnlyList<string> DistinctSetNames { get; }

        /// <summary>
        /// Returns all cards including non-browseable ones (e.g. cards whose only sets were Speed Duel).
        /// </summary>
        IEnumerable<Card> GetAllCards();

        /// <summary>
        /// Returns cards eligible to appear in Browse and collection views (Speed Duel-only cards excluded).
        /// </summary>
        IEnumerable<Card> GetBrowseableCards();

        /// <summary>
        /// Returns the card with the given catalog id, or null if not found.
        /// </summary>
        Card? GetCardByID(int cardID);

        /// <summary>
        /// Returns the card a passcode (or any other id a source has used for it) belongs to, or null if no card has it.
        /// </summary>
        Card? GetCardByPasscode(int passcode);

        /// <summary>
        /// Returns the card a catalog printing belongs to, for any printing the catalog has ever had (retired ones included), or
        /// null when the ID is unknown. The card itself may be retired; <see cref="GetCardByID"/> then returns null.
        /// </summary>
        int? GetCardIDByPrintingID(int printingID);

        /// <summary>
        /// Returns a dictionary mapping every known passcode and source id to the catalog id of its card. A passcode with no
        /// entry is unknown.
        /// </summary>
        IReadOnlyDictionary<int, int> GetPasscodeAliases();

        /// <summary>
        /// Returns an active catalog printing with its card, or null when the ID is unknown or the printing (or its card) is
        /// retired.
        /// </summary>
        CatalogPrintingRef? GetPrintingByID(int printingID);

        /// <summary>
        /// Returns a dictionary mapping full card code (e.g. "LOB-EN001") to canonical set name (e.g. "Legend of Blue Eyes White Dragon").
        /// </summary>
        IReadOnlyDictionary<string, string> GetSetNamesByCode();

        /// <summary>
        /// Returns the set code prefix (e.g. "POTE") for the given canonical set name, or null if not found.
        /// </summary>
        string? GetSetPrefixByName(string canonicalName);

        /// <summary>
        /// Brings the catalog up to date with any source whose cache is stale, then reloads the in-memory card data.
        /// </summary>
        Task LoadIfStaleAsync();

        /// <summary>
        /// Downloads every source again, brings the catalog up to date, then reloads the in-memory card data.
        /// </summary>
        Task RefreshAsync();

        /// <summary>
        /// Reloads the in-memory card data from the catalog without touching the sources, e.g. after a catalog edit.
        /// </summary>
        Task ReloadAsync();
    }
}
