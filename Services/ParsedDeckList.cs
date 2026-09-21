namespace CardCollector.Services
{
    /// <summary>The passcodes of a pasted deck list, one entry per copy, in the order they were arranged.</summary>
    public sealed class ParsedDeckList
    {
        public required IReadOnlyList<int> Extra { get; init; }

        public required DeckListFormat Format { get; init; }

        public required IReadOnlyList<int> Main { get; init; }

        public required IReadOnlyList<int> Side { get; init; }
    }
}
