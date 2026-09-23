using System.Globalization;

namespace CardCollector.Data.Models
{
    /// <summary>A tournament the user played. Its format is derived from <see cref="Date"/>, never stored.</summary>
    public class Event
    {
        public DateOnly Date { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        /// <summary>The <see cref="Deck"/> played; deliberately has no foreign key, so deleting a deck clears it in code.</summary>
        public int? DeckID { get; set; }

        public string? DecklistURL { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public EventType EventType { get; set; }

        /// <summary>Null when unknown or when the finish was a range (see <see cref="FinishNote"/>).</summary>
        public int? Finish { get; set; }

        public string? FinishNote { get; set; }

        /// <summary>The finish as shown: the placing, or <see cref="FinishNote"/> when there is none. Null when neither is set.</summary>
        public string? FinishText => Finish?.ToString(CultureInfo.InvariantCulture) ?? FinishNote;

        public int ID { get; set; }

        public string Location { get; set; } = string.Empty;

        public IReadOnlyList<Match> Matches { get; set; } = new List<Match>();

        public string? Notes { get; set; }

        public int? Players { get; set; }

        /// <summary>Free text such as "Top 8" or "1st".</summary>
        public string? TopCut { get; set; }
    }
}
