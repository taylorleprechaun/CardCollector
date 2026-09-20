namespace CardCollector.Data.Models
{
    /// <summary>One round of an <see cref="Event"/>.</summary>
    public class Match
    {
        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int EventID { get; set; }

        public int GamesLost { get; set; }

        public int GamesTied { get; set; }

        public int GamesWon { get; set; }

        public int ID { get; set; }

        public bool IsBye { get; set; }

        public string? Notes { get; set; }

        public string OpponentDeck { get; set; } = string.Empty;

        /// <summary>Stored rather than derived from the game score, so a manual override survives.</summary>
        public MatchResult Result { get; set; }

        /// <summary>A number ("1"), or a label such as "Top 8", "Top 4" or "Finals".</summary>
        public string Round { get; set; } = string.Empty;

        /// <summary>1-based position within the event; preserves the order rounds were played.</summary>
        public int Sequence { get; set; }

        /// <summary>Null when the dice roll wasn't recorded.</summary>
        public bool? WonDiceRoll { get; set; }
    }
}
