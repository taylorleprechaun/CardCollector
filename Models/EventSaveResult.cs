namespace CardCollector.Models
{
    /// <summary>Outcome of saving an event; validation failures are reported here instead of thrown.</summary>
    public sealed class EventSaveResult
    {
        private EventSaveResult(bool succeeded, IReadOnlyList<string> errors)
        {
            Errors = errors;
            Succeeded = succeeded;
        }

        public IReadOnlyList<string> Errors { get; }

        public bool Succeeded { get; }

        public static EventSaveResult Failure(IReadOnlyList<string> errors) => new(false, errors);

        public static EventSaveResult Success() => new(true, []);
    }
}
