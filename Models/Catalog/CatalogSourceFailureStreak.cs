using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>A source that has not been read successfully for <see cref="RunCount"/> sync runs in a row.</summary>
    /// <param name="Source">The failing source.</param>
    /// <param name="RunCount">How many of the most recent runs it failed or was skipped in.</param>
    /// <param name="LastSucceededUTC">When a run last read it successfully; null when no recorded run did.</param>
    public sealed record CatalogSourceFailureStreak(CatalogSource Source, int RunCount, DateTime? LastSucceededUTC);
}
