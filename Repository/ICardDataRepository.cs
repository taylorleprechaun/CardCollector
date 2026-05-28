using CardCollector.DTO;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides read-only access to card data parsed from the JSON source file.
    /// </summary>
    public interface ICardDataRepository
    {
        IEnumerable<Card> GetAllCards();
        IEnumerable<(Card Card, Image Image)> GetAllArtworks();
        Card? GetCardByID(int cardID);
    }
}
