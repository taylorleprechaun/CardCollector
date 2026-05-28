using CardCollector.DTO;
using Newtonsoft.Json;

namespace CardCollector.Repository
{
    public class CardDataRepository : ICardDataRepository
    {
        private const string CARD_INFO_PATH = "cardinfo.php.json";

        private readonly IReadOnlyList<(Card Card, Image Image)> _artworks;
        private readonly IReadOnlyList<Card> _cards;

        public CardDataRepository()
        {
            _cards = LoadCards();
            _artworks = BuildArtworkList(_cards);
        }

        public IEnumerable<(Card Card, Image Image)> GetAllArtworks() => _artworks;

        public IEnumerable<Card> GetAllCards() => _cards;

        public Card? GetCardByID(int cardID) =>
            _cards.FirstOrDefault(c => c.ID == cardID);

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

        private static IReadOnlyList<Card> LoadCards()
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
            catch (Exception)
            {
                return [];
            }
        }
    }
}
