using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    /// <summary>
    /// Coordinates card data from the JSON source with the user's collection state in SQLite.
    /// </summary>
    public interface ICardService
    {
        /// <summary>
        /// Adds a new entry to the collection.
        /// </summary>
        Task AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null,
            string? rarityName = null);

        /// <summary>
        /// Stages a printing into the cart as a pending order line, defaulting Condition to Near Mint,
        /// Edition to 1st Edition, and Purchase Date to today — the rest is filled in later from the Cart page.
        /// Returns the cart's new total line count and cost, plus the new staged quantity for this printing.
        /// </summary>
        Task<(int Count, decimal Total, int CartQuantity)> AddToCartAsync(
            int cardID, int imageID, string setCode, string? rarityName, int quantity, decimal? marketPrice);

        /// <summary>
        /// Fetches live prices for all owned entries, persists a daily snapshot, and returns the total value with a per-set breakdown.
        /// Calls <paramref name="onProgress"/> with (current, total) after each price is fetched when doing a live calculation.
        /// </summary>
        Task<(decimal TotalValue, int CardCount, IReadOnlyList<(string Label, decimal Value)> SetValueBreakdown, IReadOnlyList<(string CardName, string SetName, string RarityName, decimal Value)> TopValueCards)> CalculateCurrentMarketValueAsync(Func<int, int, Task>? onProgress = null);

        /// <summary>
        /// Checks whether the given (setCode, rarityName, edition) is a printing the live YGOProDeck data
        /// actually lists that edition for. Returns null if it looks fine, or the audit category otherwise.
        /// </summary>
        Task<EditionAuditCategory?> CheckEntryEditionAsync(int cardID, string setCode, string rarityName, CardEdition edition);

        /// <summary>
        /// Clears the checked-out status for the given (imageID, setCode, rarityName) group.
        /// </summary>
        Task CheckInCardAsync(int imageID, string setCode, string rarityName);

        /// <summary>
        /// Sets the checked-out quantity for the given group. Creates a new record if none exists (recording today as the checkout date); updates quantity on an existing record.
        /// </summary>
        Task CheckOutCardAsync(int cardID, int imageID, string setCode, string rarityName, int quantity);

        /// <summary>
        /// Records the given card set+rarity combination as dismissed so it no longer appears as an upgrade opportunity.
        /// </summary>
        Task DismissNewPrintingAsync(int cardID, string setCode, string rarityName);

        /// <summary>
        /// Returns the card with the given ID from the in-memory card data, or null if not found.
        /// </summary>
        Card? GetCardByID(int cardID);

        /// <summary>
        /// Returns up to maxResults card name suggestions whose names contain the given query string.
        /// </summary>
        IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10);

        /// <summary>
        /// Returns the price history for the given card name, with one series per tracked printing.
        /// </summary>
        Task<IReadOnlyList<CardPriceHistorySeries>> GetCardPriceHistoryAsync(string cardName);

        /// <summary>
        /// Returns the total line count and total cost across every staged (not-yet-submitted) cart line.
        /// </summary>
        Task<(int Count, decimal Total)> GetCartSummaryAsync();

        /// <summary>
        /// Returns aggregated collection statistics including rarity, set, acquisition, and value breakdowns.
        /// </summary>
        Task<CollectionStatsViewModel> GetCollectionStatsAsync();

        /// <summary>
        /// Returns high-level stats for the dashboard: counts, market value, and wishlist size.
        /// </summary>
        Task<DashboardStats> GetDashboardStatsAsync();

        /// <summary>
        /// Returns a map of card name → small image URL for every card that has at least one price snapshot.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> GetTrackedCardImageMapAsync();

        /// <summary>
        /// Returns all ordered entries enriched with card and set data.
        /// </summary>
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync();

        /// <summary>
        /// Returns all owned entries enriched with card and set data.
        /// </summary>
        Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync();

        /// <summary>
        /// Returns all collection entries for the given card ID across all artworks.
        /// </summary>
        Task<IEnumerable<CollectionEntry>> GetEntriesByCardIDAsync(int cardID);

        /// <summary>
        /// Returns owned entries grouped by printing, annotated with preferred-version and completion status.
        /// </summary>
        Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync();

        /// <summary>
        /// Returns all cards with a preferred version where a newer set printing exists and has not been dismissed.
        /// </summary>
        Task<IReadOnlyList<NewPrintingOpportunityViewModel>> GetNewPrintingOpportunitiesAsync();

        /// <summary>
        /// Returns every staged (not-yet-submitted) cart line, enriched with card and set data.
        /// </summary>
        Task<IReadOnlyList<PendingOrderLineViewModel>> GetPendingCartAsync();

        /// <summary>
        /// Returns the preferred version for the given card ID (any artwork), or null if none is set.
        /// </summary>
        Task<PreferredVersion?> GetPreferredVersionByCardIDAsync(int cardID);

        /// <summary>
        /// Builds a purchase plan by walking the purchase-priority list in order and taking candidates that fit
        /// within <paramref name="totalBudget"/> and/or <paramref name="maxCards"/>, skipping (not stopping at)
        /// any candidate too expensive to fit so a cheaper, lower-priority one further down can still be taken.
        /// Each candidate's cost accounts for the quantity still needed to complete that printing.
        /// </summary>
        Task<PurchasePlanViewModel> GetPurchasePlanAsync(decimal? totalBudget = null, int? maxCards = null, decimal? maxPricePerCard = null, DateTime? asOfUtc = null);

        /// <summary>
        /// Returns every not-yet-complete preferred printing on the wishlist, ordered so the ones worth
        /// prioritizing come first — printings that are themselves one of only 1-2 foil prints a card has ever
        /// had, gone 5+ years without a reprint, on a card old enough to judge. Everything else still appears
        /// afterward so it can still fill out a budget. Excludes cards where any preferred artwork is already
        /// fully collected. When <paramref name="maxPrice"/> is given, only printings currently priced at or
        /// below it are returned.
        /// </summary>
        Task<IReadOnlyList<PurchasePriorityCandidateViewModel>> GetPurchasePriorityCandidatesAsync(DateTime? asOfUtc = null, decimal? maxPrice = null);

        /// <summary>
        /// Returns a random card that has no preferred version set for any of its artworks.
        /// </summary>
        Task<Card?> GetRandomUncollectedAsync();

        /// <summary>
        /// Returns all preferred versions that have not yet been ordered or owned.
        /// </summary>
        Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync();

        /// <summary>
        /// Deletes the preferred version for the given image ID, removing the card from the wishlist.
        /// </summary>
        Task RemoveFromWishlistAsync(int imageID);

        /// <summary>
        /// Saves or updates the preferred printing for the given (cardID, imageID, setCode) combination.
        /// </summary>
        Task SavePreferredVersionAsync(int cardID, int imageID, string setCode, string? rarityName = null);

        /// <summary>
        /// Returns a paginated, filtered page of browseable cards matching the given criteria.
        /// </summary>
        Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(BrowseSearchCriteria criteria);

        /// <summary>
        /// Returns a paginated, filtered page of checked-out cards matching the given criteria.
        /// </summary>
        Task<PagedResult<CheckedOutCardViewModel>> SearchCheckedOutAsync(CheckedOutSearchCriteria criteria);

        /// <summary>
        /// Cross-checks collection entries' recorded Edition against the live YGOProDeck data for that printing,
        /// returning a paginated, filtered page of the ones that look like data-entry mistakes.
        /// </summary>
        Task<PagedResult<EditionAuditGroupViewModel>> SearchEditionAuditAsync(EditionAuditSearchCriteria criteria);

        /// <summary>
        /// Returns a paginated, filtered page of owned collection groups matching the given criteria.
        /// </summary>
        Task<PagedResult<CollectionGroupViewModel>> SearchGroupedOwnedAsync(CollectionSearchCriteria criteria);

        /// <summary>
        /// Returns a paginated, filtered, and sorted page of wishlist items.
        /// </summary>
        Task<WishlistSearchResult> SearchWishlistAsync(WishlistSearchCriteria criteria);

        /// <summary>
        /// Converts every staged cart line into a real Ordered collection entry, then clears the cart.
        /// Each override's Condition/Edition/PurchaseDate/PurchasePrice/MarketPriceAtEntry/Quantity replace
        /// the staged (mostly-null) values on the matching line by PendingOrderLineID before it's committed.
        /// Returns the number of entries created and their total cost.
        /// </summary>
        Task<(int Count, decimal Total)> SubmitCartAsync(IReadOnlyList<CartLineOverride> overrides);

        /// <summary>
        /// Returns the distinct rarity names present in the current wishlist.
        /// </summary>
        Task<IReadOnlyList<string>> GetWishlistDistinctRarityNamesAsync();

        /// <summary>
        /// Returns the distinct set names present in the current wishlist.
        /// </summary>
        Task<IReadOnlyList<string>> GetWishlistDistinctSetNamesAsync();

        /// <summary>
        /// Marks the given card as ignored, excluding it from Dashboard progress tracking. No-ops if already ignored.
        /// </summary>
        Task IgnoreCardAsync(int cardID);

        /// <summary>
        /// Returns true if the given card is currently ignored from Dashboard progress tracking.
        /// </summary>
        Task<bool> IsCardIgnoredAsync(int cardID);

        /// <summary>
        /// Clears the ignored status for the given card, resuming Dashboard progress tracking for it. No-ops if not currently ignored.
        /// </summary>
        Task UnignoreCardAsync(int cardID);

        /// <summary>
        /// Updates the preferred version for the given image ID to the specified newer printing.
        /// </summary>
        Task UpgradePreferredVersionAsync(int imageID, int cardID, string newSetCode, string newRarityName);
    }
}
