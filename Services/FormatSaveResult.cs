namespace CardCollector.Services
{
    /// <summary>Outcome of saving a format; validation failures are reported here instead of thrown.</summary>
    public sealed class FormatSaveResult
    {
        private FormatSaveResult(bool succeeded, IReadOnlyList<string> errors)
        {
            Errors = errors;
            Succeeded = succeeded;
        }

        public IReadOnlyList<string> Errors { get; }

        public bool Succeeded { get; }

        public static FormatSaveResult Failure(IReadOnlyList<string> errors) => new(false, errors);

        public static FormatSaveResult Success() => new(true, []);
    }
}
