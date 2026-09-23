using CardCollector.Models;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides the TCG Forbidden &amp; Limited list in effect on a date, and the current list. Never throws for a
    /// missing or unreachable source — callers get null and treat the feature as unavailable.
    /// </summary>
    public interface IBanlistRepository
    {
        /// <summary>Effective dates of every known dated list, newest first. Empty when no data is available.</summary>
        Task<IReadOnlyList<DateOnly>> GetAvailableListsAsync();

        /// <summary>The current TCG list, or null when no data is available.</summary>
        Task<Banlist?> GetCurrentAsync();

        /// <summary>The list whose effective date matches exactly, or null when there is none.</summary>
        Task<Banlist?> GetListAsync(DateOnly effectiveDate);

        /// <summary>The latest list on or before <paramref name="date"/>, or null when before the first known list.</summary>
        Task<Banlist?> GetListForDateAsync(DateOnly date);

        /// <summary>Fetches a fresh copy if the on-disk cache is missing or stale; otherwise a no-op.</summary>
        Task LoadIfStaleAsync();
    }
}
