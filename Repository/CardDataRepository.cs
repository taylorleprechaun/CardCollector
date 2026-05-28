using CardCollector.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CardCollector.Repository
{
    public class CardDataRepository : ICardDataRepository
    {
        private readonly IReadOnlyList<(Card Card, Image Image)> _artworks;
        private readonly Dictionary<int, Card> _cardIndex;
        private const string CARD_INFO_PATH = "cardinfo.php.json";
        private readonly IReadOnlyList<Card> _cards;
        private readonly ILogger<CardDataRepository> _logger;

        public CardDataRepository(ILogger<CardDataRepository> logger)
        {
            _logger = logger;
            _cards = LoadCards();
            _artworks = BuildArtworkList(_cards);
            _cardIndex = _cards.ToDictionary(c => c.ID);
        }

        public IEnumerable<(Card Card, Image Image)> GetAllArtworks() => _artworks;

        public IEnumerable<Card> GetAllCards() => _cards;

        public Card? GetCardByID(int cardID) =>
            _cardIndex.GetValueOrDefault(cardID);

        private static IReadOnlyList<(Card, Image)> BuildArtworkList(IReadOnlyList<Card> cards)
        {
            var artworks = new List<(Card, Image)>();
            foreach (var card in cards)
            {
                if (card.CardImages is null)
                    continue;
                foreach (var image in card.CardImages)
                    artworks.Add((card, image));
            }
            return artworks;
        }

        private IReadOnlyList<Card> LoadCards()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), CARD_INFO_PATH);
            if (!File.Exists(path))
                return [];

            var jsonData = File.ReadAllText(path);
            if (string.IsNullOrEmpty(jsonData))
                return [];

            try
            {
                var cardArray = JsonConvert.DeserializeObject<CardArray>(jsonData);
                return (cardArray?.Cards ?? []).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load card data from {Path}", path);
                return [];
            }
        }
    }
}
