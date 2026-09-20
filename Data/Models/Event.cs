namespace CardCollector.Data.Models
{
    /// <summary>A tournament the user played. Its format is derived from <see cref="Date"/>, never stored.</summary>
    public class Event
    {
        public DateOnly Date { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        /// <summary>Reserved for the deck link; deliberately has no foreign key, so the link is enforced in code.</summary>
        public int? DeckID { get; set; }

        public string? DecklistURL { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public EventType EventType { get; set; }

        /// <summary>Null when unknown or when the finish was a range (see <see cref="FinishNote"/>).</summary>
        public int? Finish { get; set; }

        public string? FinishNote { get; set; }

        public int ID { get; set; }

        public string Location { get; set; } = string.Empty;

        public IReadOnlyList<Match> Matches { get; set; } = new List<Match>();

        public string? Notes { get; set; }

        public int? Players { get; set; }

        /// <summary>Free text such as "Top 8" or "1st".</summary>
        public string? TopCut { get; set; }
    }
}
