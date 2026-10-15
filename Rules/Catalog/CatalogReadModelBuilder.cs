using System.Globalization;
using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.Repository;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Builds the app's read model from the catalog: <see cref="Card"/> ids are catalog ids, not passcodes.</summary>
    public static class CatalogReadModelBuilder
    {
        public static CatalogReadModel Build(CatalogState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));

            var cards = state.Cards
                .Where(c => c.Status == CatalogStatus.Active)
                .OrderBy(c => c.ID)
                .Select(ToCard)
                .ToList();
            var browseable = cards.Where(c => c.CardSets?.Count > 0).ToList();
            var (setNamesByCode, setPrefixByName) = CardDataMapper.BuildSetNameIndex(cards);

            var browseablePrefixes = browseable
                .SelectMany(c => c.CardSets ?? [])
                .Where(s => !string.IsNullOrEmpty(s.Code))
                .Select(s => CardDataMapper.GetSetPrefix(s.Code!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var printings = cards
                .SelectMany(c => (c.CardSets ?? []).Select(s => new CatalogPrintingRef(c, s)))
                .Where(r => r.Printing.PrintingID > 0)
                .ToList();

            return new CatalogReadModel
            {
                AllRarityNames = printings
                    .Select(r => r.Printing.RarityName)
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                BrowseableCards = browseable,
                CardIDsByPrintingID = state.Cards
                    .SelectMany(c => c.Printings.Select(p => (PrintingID: p.ID, CardID: c.ID)))
                    .ToDictionary(x => x.PrintingID, x => x.CardID),
                Cards = cards,
                CardsByID = cards.ToDictionary(c => c.ID),
                CardsByName = cards
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .GroupBy(c => c.Name!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<Card>)g.ToList(), StringComparer.OrdinalIgnoreCase),
                DistinctRarityNames = browseable
                    .SelectMany(c => c.CardSets ?? [])
                    .Select(s => RarityExtensions.NormalizeRarityName(s.RarityName))
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Select(r => r!)
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList(),
                DistinctSetNames = setPrefixByName
                    .Where(kvp => browseablePrefixes.Contains(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .OrderBy(n => n)
                    .ToList(),
                PasscodeAliases = BuildPasscodeAliases(state.Cards),
                PrintingsByID = printings.ToDictionary(r => r.Printing.PrintingID),
                PrintingsBySetCode = printings
                    .Where(r => !string.IsNullOrWhiteSpace(r.Printing.Code))
                    .GroupBy(r => r.Printing.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<CatalogPrintingRef>)g.ToList(), StringComparer.OrdinalIgnoreCase),
                SetNamesByCode = setNamesByCode,
                SetPrefixByName = setPrefixByName
            };
        }

        /// <summary>
        /// Maps every passcode and source id to its card. A value several cards have carried goes to the card with the
        /// strongest alias kind, then the most recently seen; a card's own displayed passcode always counts as its alias.
        /// </summary>
        public static IReadOnlyDictionary<int, int> BuildPasscodeAliases(IEnumerable<CatalogCard> cards)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));

            var active = cards.Where(c => c.Status == CatalogStatus.Active).ToList();
            var aliases = new Dictionary<int, int>();

            foreach (var card in active.Where(c => c.Passcode.HasValue))
                aliases.TryAdd(card.Passcode!.Value, card.ID);

            var ranked = active
                .SelectMany(c => c.Aliases.Select(a => (CardID: c.ID, Alias: a)))
                .OrderBy(x => x.Alias.Kind.ResolutionPriority())
                .ThenByDescending(x => x.Alias.LastSeenUTC);
            foreach (var (cardID, alias) in ranked)
                aliases.TryAdd(alias.Value, cardID);

            return aliases;
        }

        private static IReadOnlyList<string>? SplitList(string? joined) =>
            string.IsNullOrWhiteSpace(joined) ? null : joined.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        private static Card ToCard(CatalogCard card)
        {
            var images = card.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new Image { ID = i.ImageID, ImageURL = i.ImageURL, ImageURLSmall = i.ImageURLSmall })
                .ToList();
            if (images.Count == 0 && card.Passcode is int passcode)
                images.Add(CardDataMapper.BuildFallbackImage(passcode));

            return new Card
            {
                Archetype = card.Archetype,
                ATK = card.ATK,
                Attribute = card.Attribute,
                CardImages = images,
                CardSets = card.Printings
                    .Where(p => p.Status == CatalogStatus.Active)
                    .OrderBy(p => p.ID)
                    .Select(ToSet)
                    .ToList(),
                CardType = card.CardType,
                DEF = card.DEF,
                Description = card.Description,
                ID = card.ID,
                KonamiID = card.KonamiID,
                Level = card.Level,
                LinkMarkers = SplitList(card.LinkMarkers),
                LinkRating = card.LinkRating,
                Name = card.Name,
                Passcode = card.Passcode,
                Type = card.Type,
                TypeLine = SplitList(card.TypeLine)
            };
        }

        private static Set ToSet(CatalogPrinting printing) => new()
        {
            Code = printing.SetCode,
            Name = printing.SetName ?? printing.Set?.Name,
            PrintingID = printing.ID,
            PrintVariant = printing.PrintVariant,
            RarityCode = RarityExtensions.GetRarityCode(printing.RarityName),
            RarityName = printing.RarityName,
            TCGProductIDs = printing.Sources
                .Where(s => s.Source == CatalogSource.TCGPlayer)
                .Select(s => int.TryParse(s.ExternalKey, NumberStyles.None, CultureInfo.InvariantCulture, out var productID) ? productID : 0)
                .Where(id => id > 0)
                .ToList()
        };
    }
}
