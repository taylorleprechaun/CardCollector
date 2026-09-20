namespace CardCollector.ViewModels
{
    /// <summary>What the shared round form fields need to render; the same fields serve the Add and Edit forms.</summary>
    public sealed class MatchFormViewModel
    {
        /// <summary>Keeps element ids unique when the fields appear more than once on a page.</summary>
        public required string IDPrefix { get; init; }

        /// <summary>The round label the field starts with; empty for a form that is filled in when it opens.</summary>
        public string InitialRound { get; init; } = string.Empty;
    }
}
