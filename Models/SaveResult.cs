namespace CardCollector.Models
{
    /// <summary>Outcome of saving a format, event or deck; validation failures are reported here instead of thrown.</summary>
    public sealed class SaveResult
    {
        private SaveResult(IReadOnlyList<string> errors, bool notFound, bool succeeded)
        {
            Errors = errors;
            NotFound = notFound;
            Succeeded = succeeded;
        }

        public IReadOnlyList<string> Errors { get; }

        /// <summary>True when the record being updated does not exist.</summary>
        public bool NotFound { get; }

        public bool Succeeded { get; }

        public static SaveResult Failure(IReadOnlyList<string> errors) => new(errors, false, false);

        public static SaveResult Missing(string message) => new([message], true, false);

        public static SaveResult Success() => new([], false, true);
    }
}
