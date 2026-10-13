using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.DTO.YGOProDeck;
using CardCollector.Repository.Sources;
using CardCollector.Rules;
using CardCollector.Services;
using Microsoft.Extensions.Logging;

namespace CardCollector.Repository
{
    public sealed class CardDataRepository : ICardDataRepository
    {
        private readonly ILogger<CardDataRepository> _logger;
        private readonly ITCGCatalogCache _tcgCatalogCache;
        private readonly IYamlYugiSource _yamlYugiSource;
        private readonly IYGOProDeckSource _ygoProDeckSource;
        private IReadOnlyList<Card> _browseableCards = default!;
        private IReadOnlyDictionary<int, Card> _cardIndex = default!;
        private IReadOnlyList<Card> _cards = default!;
        private IReadOnlyDictionary<int, int> _passcodeAliases = default!;
        private IReadOnlyDictionary<string, string> _setNamesByCode = default!;
        private IReadOnlyDictionary<string, string> _setPrefixByName = default!;

        public CardDataRepository(
            ILogger<CardDataRepository> logger,
            ITCGCatalogCache tcgCatalogCache,
            IYamlYugiSource yamlYugiSource,
            IYGOProDeckSource ygoProDeckSource)
        {
            _logger = logger;
            _tcgCatalogCache = tcgCatalogCache;
            _yamlYugiSource = yamlYugiSource;
            _ygoProDeckSource = ygoProDeckSource;

            // Disk-only, no network, so construction never blocks startup.
            Initialize(_yamlYugiSource.ReadCached(), _ygoProDeckSource.ReadCached());
        }

        public IReadOnlyList<string> DistinctRarityNames { get; private set; } = default!;
        public IReadOnlyList<string> DistinctSetNames { get; private set; } = default!;
        public IEnumerable<Card> GetAllCards() => _cards;

        public IEnumerable<Card> GetBrowseableCards() => _browseableCards;

        public Card? GetCardByID(int cardID) =>
            _cardIndex.GetValueOrDefault(cardID);

        public IReadOnlyDictionary<int, int> GetPasscodeAliases() => _passcodeAliases;

        public IReadOnlyDictionary<string, string> GetSetNamesByCode() => _setNamesByCode;

        public string? GetSetPrefixByName(string name) =>
            _setPrefixByName.TryGetValue(name, out var prefix) ? prefix : null;

        public async Task LoadIfStaleAsync()
        {
            var ygoProDeckCards = await _ygoProDeckSource.LoadAsync(forceRefresh: false).ConfigureAwait(false);
            var yamlCards = await _yamlYugiSource.LoadAsync(forceRefresh: false).ConfigureAwait(false);
            Initialize(yamlCards, ygoProDeckCards);
        }

        public async Task RefreshAsync()
        {
            var ygoProDeckCards = await _ygoProDeckSource.LoadAsync(forceRefresh: true).ConfigureAwait(false);
            var yamlCards = await _yamlYugiSource.LoadAsync(forceRefresh: true).ConfigureAwait(false);
            Initialize(yamlCards, ygoProDeckCards);
        }

        private IReadOnlyList<Card> BuildCards(IReadOnlyList<Card> yamlCards, IReadOnlyList<Card> ygoProDeckCards)
        {
            // Built before the merge: afterwards YGOProDeck's alternate primary ids exist as cards of their own,
            // and the yaml-yugi ids are what tell the real card from its duplicate.
            _passcodeAliases = PasscodeAliasResolver.BuildAliases(yamlCards.Select(c => c.ID), ygoProDeckCards);

            var corrections = RarityCorrectionLoader.Load(_logger);
            var correctedCount = CardDataMapper.ApplyRarityCorrections(yamlCards, corrections)
                + CardDataMapper.ApplyRarityCorrections(ygoProDeckCards, corrections);
            if (correctedCount > 0)
                _logger.LogInformation(
                    "Corrected {Count} known-bad rarity name(s) using the curated correction table",
                    correctedCount);

            var addedSetPrintings = CardDataMapper.MergeMissingSetPrintings(yamlCards, ygoProDeckCards);
            if (addedSetPrintings > 0)
                _logger.LogInformation(
                    "Added {Count} set printing(s) found in YGOProDeck but missing from yaml-yugi for cards present in both sources",
                    addedSetPrintings);

            var mergedCards = CardDataMapper.MergeMissingCards(yamlCards, ygoProDeckCards);

            var strippedCount = CardDataMapper.StripPlaceholderSets(mergedCards);
            if (strippedCount > 0)
                _logger.LogInformation(
                    "Removed {Count} printing(s) with a placeholder set code (unnumbered set) until a source publishes the real code",
                    strippedCount);

            return mergedCards;
        }

        private void Initialize(IReadOnlyList<YamlCard> yamlCards, IReadOnlyList<YGOProDeckCard> ygoProDeckCards)
        {
            // Captured before BuildCards, which merges these same card objects into the result.
            var imagesByCardID = ygoProDeckCards
                .Where(c => c.CardImages?.Any() == true)
                .ToDictionary(c => c.ID, c => c.CardImages!);

            var rawCards = BuildCards(CardDataMapper.ConvertYamlCards(yamlCards), ygoProDeckCards);
            CardDataMapper.AttachImages(rawCards, imagesByCardID);

            var newRarityRowCount = CardDataMapper.EnrichWithPrintVariants(rawCards, _tcgCatalogCache.GetAllPrintings());
            if (newRarityRowCount > 0)
                _logger.LogInformation(
                    "Added {Count} new rarity row(s) from tcgcsv catalog for set codes with no prior matching rarity",
                    newRarityRowCount);

            _cards = rawCards;
            _browseableCards = rawCards.Where(c => c.CardSets?.Any() == true).ToList();
            _cardIndex = rawCards.ToDictionary(c => c.ID);

            var (setNamesByCode, setPrefixByName) = CardDataMapper.BuildSetNameIndex(rawCards);
            _setNamesByCode = setNamesByCode;
            _setPrefixByName = setPrefixByName;

            var browseablePrefixes = new HashSet<string>(
                _browseableCards
                    .SelectMany(c => c.CardSets ?? [])
                    .Where(s => !string.IsNullOrEmpty(s.Code))
                    .Select(s => CardDataMapper.GetSetPrefix(s.Code!)),
                StringComparer.OrdinalIgnoreCase);

            DistinctRarityNames = _browseableCards
                .SelectMany(c => c.CardSets ?? [])
                .Select(s => RarityExtensions.NormalizeRarityName(s.RarityName))
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => r!)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            DistinctSetNames = _setPrefixByName
                .Where(kvp => browseablePrefixes.Contains(kvp.Value))
                .Select(kvp => kvp.Key)
                .OrderBy(n => n)
                .ToList();
        }
    }
}
