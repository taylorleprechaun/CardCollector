namespace CardCollector.Data.Models
{
    /// <summary>A stored deck list. Events point at it through <see cref="Event.DeckID"/>.</summary>
    public sealed class Deck
    {
        public IReadOnlyList<DeckCard> Cards { get; set; } = new List<DeckCard>();

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
}
