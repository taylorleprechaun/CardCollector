using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.DTO.YGOProDeck;
using CardCollector.Models.Catalog;
using CardCollector.Repository;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Turns each source's raw records into the shape the catalog sync compares: one <see cref="SourceCard"/> per card
    /// and one <see cref="SourceTCGProduct"/> per TCGPlayer product.
    /// </summary>
    /// <remarks>
    /// Printings are filtered the same way for every source: Speed Duel sets and placeholder set codes (containing '?')
    /// are dropped, known-bad rarities are corrected, and rarity names are normalized.
    /// </remarks>
    public static class CatalogSourceNormalizer
    {
        /// <summary>
        /// Groups tcgcsv price rows (one per product and edition) into products. Rows written before product ids were
        /// captured, or missing a card name, are skipped.
        /// </summary>
        public static IReadOnlyList<SourceTCGProduct> FromTCGPlayer(IEnumerable<TCGPriceSet> priceRows)
        {
            if (priceRows is null) throw new ArgumentNullException(nameof(priceRows));

            return priceRows
                .Where(r => r.ProductID > 0 && !string.IsNullOrWhiteSpace(r.CardName) && !string.IsNullOrWhiteSpace(r.Code))
                .GroupBy(r => r.ProductID)
                .Select(g =>
                {
                    var row = g.First();
                    return new SourceTCGProduct(
                        row.ProductID,
                        row.GroupID,
                        row.CardName!,
                        row.Code,
                        RarityExtensions.NormalizeRarityName(row.RarityName) ?? row.RarityName,
                        string.IsNullOrWhiteSpace(row.PrintVariant) ? null : row.PrintVariant);
                })
                .ToList();
        }

        /// <summary>Converts yaml-yugi documents, skipping any without an English name. Cards with no passcode are kept.</summary>
        public static IReadOnlyList<SourceCard> FromYamlYugi(
            IEnumerable<YamlCard> documents,
            IReadOnlyDictionary<(string SetCode, string RarityName), string> rarityCorrections)
        {
            if (documents is null) throw new ArgumentNullException(nameof(documents));
            if (rarityCorrections is null) throw new ArgumentNullException(nameof(rarityCorrections));

            return documents
                .Where(y => !string.IsNullOrWhiteSpace(y.Name?.En))
                .Select(y => new SourceCard
                {
                    ATK = int.TryParse(y.Atk, out var atk) ? atk : null,
                    Attribute = y.Attribute,
                    CardType = CardDataMapper.DeriveCardType(y),
                    DEF = int.TryParse(y.Def, out var def) ? def : null,
                    Description = y.Text?.En,
                    FakePassword = y.FakePassword,
                    KonamiID = y.KonamiID,
                    Level = y.Level ?? y.Rank,
                    LinkRating = y.LinkArrows?.Count,
                    Name = y.Name!.En!,
                    Passcode = y.Password,
                    Printings = NormalizePrintings(
                        (y.Sets?.En ?? []).SelectMany(e => (e.Rarities ?? []).Select(r => (e.SetNumber, e.SetName, (string?)r))),
                        rarityCorrections,
                        dropUnknownRarities: false),
                    Source = CatalogSource.YamlYugi,
                    Type = CardDataMapper.DeriveRace(y)
                })
                .ToList();
        }

        /// <summary>
        /// Converts YGOProDeck cards, skipping any without a name. Printings with a rarity the app doesn't recognize are
        /// dropped, as the YGOProDeck merge always has.
        /// </summary>
        public static IReadOnlyList<SourceCard> FromYGOProDeck(
            IEnumerable<YGOProDeckCard> cards,
            IReadOnlyDictionary<(string SetCode, string RarityName), string> rarityCorrections)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));
            if (rarityCorrections is null) throw new ArgumentNullException(nameof(rarityCorrections));

            return cards
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new SourceCard
                {
                    Archetype = c.Archetype,
                    ArtworkIDs = (c.CardImages ?? []).Select(i => i.ID).Where(id => id != c.ID).Distinct().ToList(),
                    ATK = c.ATK,
                    Attribute = c.Attribute,
                    BetaID = c.BetaID,
                    CardType = c.CardType,
                    DEF = c.DEF,
                    Description = c.Description,
                    Images = c.CardImages ?? [],
                    KonamiID = c.KonamiID,
                    Level = c.Level,
                    LinkMarkers = c.LinkMarkers is { Count: > 0 } ? string.Join(",", c.LinkMarkers) : null,
                    LinkRating = c.LinkRating,
                    Name = c.Name!,
                    Printings = NormalizePrintings(
                        (c.CardSets ?? []).Select(s => (s.Code, s.Name, s.RarityName)),
                        rarityCorrections,
                        dropUnknownRarities: true),
                    Source = CatalogSource.YGOProDeck,
                    Type = c.Type,
                    TypeLine = c.TypeLine is { Count: > 0 } ? string.Join(",", c.TypeLine) : null,
                    YGOProDeckID = c.ID
                })
                .ToList();
        }

        private static IReadOnlyList<SourcePrinting> NormalizePrintings(
            IEnumerable<(string? SetCode, string? SetName, string? RarityName)> rows,
            IReadOnlyDictionary<(string SetCode, string RarityName), string> rarityCorrections,
            bool dropUnknownRarities)
        {
            var printings = new List<SourcePrinting>();
            var seen = new HashSet<(string, string)>();

            foreach (var (setCode, setName, rarityName) in rows)
            {
                if (string.IsNullOrWhiteSpace(setCode) || setCode.Contains('?') || string.IsNullOrWhiteSpace(rarityName))
                    continue;
                if (CardDataMapper.IsSpeedDuelSet(setName))
                    continue;

                var corrected = rarityCorrections.GetValueOrDefault((setCode.ToUpperInvariant(), rarityName.ToUpperInvariant()), rarityName);
                var normalized = RarityExtensions.NormalizeRarityName(corrected) ?? corrected;
                if (dropUnknownRarities && RarityExtensions.ParseRarity(normalized) == Rarity.Error)
                    continue;

                if (seen.Add((setCode.ToUpperInvariant(), normalized.ToUpperInvariant())))
                    printings.Add(new SourcePrinting(setCode, setName, normalized));
            }

            return printings;
        }
    }
}
