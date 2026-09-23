namespace CardCollector.Models
{
    /// <summary>Outcome of renaming a deck; validation failures are reported here instead of thrown.</summary>
    public sealed class DeckSaveResult
    {
        private DeckSaveResult(IReadOnlyList<string> errors, bool notFound, bool succeeded)
        {
            Errors = errors;
            NotFound = notFound;
            Succeeded = succeeded;
        }

        public IReadOnlyList<string> Errors { get; }

        /// <summary>True when the deck does not exist.</summary>
        public bool NotFound { get; }

        public bool Succeeded { get; }

        public static DeckSaveResult Failure(IReadOnlyList<string> errors) => new(errors, false, false);

        public static DeckSaveResult Missing(string message) => new([message], true, false);

        public static DeckSaveResult Success() => new([], false, true);
    }
}
