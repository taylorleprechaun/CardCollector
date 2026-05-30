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
        IEnumerable<(Card Card, Image Image)> GetBrowseableArtworks();
        IEnumerable<Card> GetAllCards();
        IEnumerable<Card> GetBrowseableCards();
        Card? GetCardByID(int cardID);
        IReadOnlyDictionary<string, string> GetSetNamesByCode();
    }
}
