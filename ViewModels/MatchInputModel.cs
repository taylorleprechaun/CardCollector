using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>
    /// Posted values of the Add/Edit Round form. Everything is nullable so MVC's implicit "required"
    /// handling for non-nullable reference types never fires; MatchRules owns validation.
    /// </summary>
    public sealed class MatchInputModel
    {
        public int? GamesLost { get; set; }

        public int? GamesTied { get; set; }

        public int? GamesWon { get; set; }

        /// <summary>Zero means a new round.</summary>
        public int ID { get; set; }

        public bool IsBye { get; set; }

        public string? Notes { get; set; }

        public string? OpponentDeck { get; set; }

        public MatchResult? Result { get; set; }

        public string? Round { get; set; }

        /// <summary>Null when the dice roll wasn't recorded.</summary>
        public bool? WonDiceRoll { get; set; }
    }
}
