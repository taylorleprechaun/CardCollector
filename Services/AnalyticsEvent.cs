using CardCollector.Data.Models;

namespace CardCollector.Services
{
    /// <summary>An event prepared for analytics: its derived format and the rounds left after filtering.</summary>
    /// <param name="Event">The event as stored.</param>
    /// <param name="Format">Null when the event's date falls outside every format.</param>
    /// <param name="Matches">The event's rounds that passed the filters.</param>
    public sealed record AnalyticsEvent(Event Event, Format? Format, IReadOnlyList<Match> Matches);
}
