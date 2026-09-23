namespace CardCollector.Models
{
    /// <summary>Outcome of parsing a pasted deck list; a bad paste is reported here instead of thrown.</summary>
    public sealed class DeckListParseResult
    {
        /// <summary>The parsed deck; null unless parsing succeeded.</summary>
        public ParsedDeckList? Deck { get; }

        /// <summary>A message fit to show the user; null unless parsing failed.</summary>
        public string? Error { get; }

        public bool Succeeded => Deck is not null;

        private DeckListParseResult(ParsedDeckList? deck, string? error)
        {
            Deck = deck;
            Error = error;
        }

        public static DeckListParseResult Failure(string error) => new(null, error);

        public static DeckListParseResult Success(ParsedDeckList deck) => new(deck, null);
    }
}
