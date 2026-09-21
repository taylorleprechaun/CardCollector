namespace CardCollector.Services
{
    /// <summary>
    /// Outcome of importing (or previewing) a pasted deck list; a bad paste is reported here instead of thrown.
    /// </summary>
    public sealed class DeckImportResult
    {
        /// <summary>The new deck's ID; 0 for a preview.</summary>
        public int DeckID { get; init; }

        /// <summary>Why the import failed; empty on success.</summary>
        public IReadOnlyList<string> Errors { get; init; } = [];

        /// <summary>Cards in the extra deck, including copies.</summary>
        public int ExtraCount { get; init; }

        /// <summary>How many events now point at the new deck; 0 for a preview.</summary>
        public int LinkedEventCount { get; init; }

        /// <summary>Cards in the main deck, including copies.</summary>
        public int MainCount { get; init; }

        /// <summary>Cards in the side deck, including copies.</summary>
        public int SideCount { get; init; }

        public bool Succeeded => Errors.Count == 0;

        /// <summary>Distinct passcodes the card data doesn't know. They are still stored.</summary>
        public IReadOnlyList<int> UnknownPasscodes { get; init; } = [];

        public static DeckImportResult Failure(string error) => new() { Errors = [error] };

        public static DeckImportResult Failure(IReadOnlyList<string> errors) => new() { Errors = errors };
    }
}
