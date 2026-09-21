namespace CardCollector.Services
{
    /// <summary>A pasted deck list to store, and optionally the event it was played at.</summary>
    public sealed class DeckImportRequest
    {
        /// <summary>The event to link the new deck to, if any.</summary>
        public int? EventID { get; init; }

        /// <summary>Also link other events that have the same decklist URL as <see cref="EventID"/> and no deck yet.</summary>
        public bool LinkOtherEventsWithSameUrl { get; init; }

        /// <summary>The deck's name; falls back to the linked event's deck name when blank.</summary>
        public string? Name { get; init; }

        /// <summary>A YDKe code or the contents of a YDK file.</summary>
        public string? Text { get; init; }
    }
}
