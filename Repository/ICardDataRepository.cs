using CardCollector.DTO;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides read-only access to card data parsed from the JSON source file.
    /// </summary>
    public interface ICardDataRepository
    {
        IReadOnlyList<string> DistinctAttributes { get; }
        IReadOnlyList<string> DistinctRarityNames { get; }
        IEnumerable<(Card Card, Image Image)> GetAllArtworks();
        IEnumerable<Card> GetAllCards();
        Card? GetCardByID(int cardID);
    }
}
