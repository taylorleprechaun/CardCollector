using CardCollector.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CardCollector.Repository
{
    public class CardDataRepository : ICardDataRepository
    {
        private readonly IReadOnlyList<(Card Card, Image Image)> _artworks;
        private readonly IReadOnlyList<(Card Card, Image Image)> _browseableArtworks;
        private readonly IReadOnlyList<Card> _browseableCards;
        private readonly Dictionary<int, Card> _cardIndex;
        private const string CARD_INFO_PATH = "cardinfo.php.json";
        private readonly IReadOnlyList<Card> _cards;
        private readonly ILogger<CardDataRepository> _logger;

        public IReadOnlyList<string> DistinctAttributes { get; }
        public IReadOnlyList<string> DistinctRarityNames { get; }

        public CardDataRepository(ILogger<CardDataRepository> logger)
        {
            _logger = logger;
            _cards = LoadCards();
            _browseableCards = _cards.Where(HasNonSpeedDuelPrinting).ToList();
            _artworks = BuildArtworkList(_cards);
            _browseableArtworks = BuildArtworkList(_browseableCards);
            _cardIndex = _cards.ToDictionary(c => c.ID);
            DistinctAttributes = _browseableCards
                .Where(c => !string.IsNullOrEmpty(c.Attribute))
                .Select(c => c.Attribute!)
                .Distinct()
                .OrderBy(a => a)
                .ToList();
            DistinctRarityNames = _browseableCards
                .SelectMany(c => c.CardSets ?? [])
                .Select(s => s.RarityName)
                .Where(r => !string.IsNullOrEmpty(r))
                .Distinct()
                .OrderBy(r => r)
                .ToList();
        }

        public IEnumerable<(Card Card, Image Image)> GetAllArtworks() => _artworks;

        public IEnumerable<(Card Card, Image Image)> GetBrowseableArtworks() => _browseableArtworks;

        public IEnumerable<Card> GetAllCards() => _cards;

        public IEnumerable<Card> GetBrowseableCards() => _browseableCards;

        public Card? GetCardByID(int cardID) =>
            _cardIndex.GetValueOrDefault(cardID);

        private static bool HasNonSpeedDuelPrinting(Card card) =>
            card.CardSets?.Any(s => !s.Name.Contains("Speed Duel", StringComparison.OrdinalIgnoreCase)) == true;

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
