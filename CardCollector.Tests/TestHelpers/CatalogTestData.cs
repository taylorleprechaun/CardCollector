using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;

namespace CardCollector.Tests.TestHelpers
{
    /// <summary>Builders for synthetic catalog-sync data, so each test spells out only the fields its scenario is about.</summary>
    internal static class CatalogTestData
    {
        public static readonly DateTime EarlierUTC = new(2026, 10, 1, 4, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime NowUTC = new(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc);

        public static CatalogCard Card(
            int id,
            string name,
            int? konamiID = null,
            int? passcode = null,
            params (CatalogSource Source, CatalogAliasKind Kind, int Value)[] aliases)
        {
            var card = new CatalogCard
            {
                FirstSeenUTC = EarlierUTC,
                ID = id,
                KonamiID = konamiID,
                LastSeenUTC = EarlierUTC,
                Name = name,
                Passcode = passcode,
                Status = CatalogStatus.Active
            };
            foreach (var (source, kind, value) in aliases)
                card.Aliases.Add(new CatalogCardAlias { CatalogCardID = id, Kind = kind, LastSeenUTC = EarlierUTC, Source = source, Value = value });

            return card;
        }

        public static CatalogSyncContext Context(bool isBootstrap = false, bool tcgPlayer = true, bool yamlYugi = true, bool ygoProDeck = true) => new()
        {
            IsBootstrap = isBootstrap,
            NowUTC = NowUTC,
            TCGPlayerSucceeded = tcgPlayer,
            YamlYugiSucceeded = yamlYugi,
            YGOProDeckSucceeded = ygoProDeck
        };

        public static CatalogPrinting Printing(
            CatalogCard card,
            int id,
            string setCode,
            string rarityName,
            string? printVariant = null,
            params int[] tcgProductIDs)
        {
            var set = new CatalogSet { Name = "Synthetic Set", Prefix = setCode.Split('-')[0] };
            var printing = new CatalogPrinting
            {
                Card = card,
                CatalogCardID = card.ID,
                FirstSeenUTC = EarlierUTC,
                ID = id,
                LastSeenUTC = EarlierUTC,
                PrintVariant = printVariant,
                RarityName = rarityName,
                Set = set,
                SetCode = setCode,
                SetName = set.Name,
                Status = CatalogStatus.Active
            };
            foreach (var productID in tcgProductIDs)
                printing.Sources.Add(new CatalogPrintingSource
                {
                    ExternalKey = productID.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    LastSeenUTC = EarlierUTC,
                    Source = CatalogSource.TCGPlayer
                });

            card.Printings.Add(printing);
            return printing;
        }

        public static SourceTCGProduct Product(int productID, string cardName, string setCode, string rarityName, string? printVariant = null) =>
            new(productID, 1, cardName, setCode, rarityName, printVariant);

        /// <summary>A catalog state holding the cards and every set their printings use.</summary>
        public static CatalogState State(params CatalogCard[] cards) => new()
        {
            Cards = cards.ToList(),
            Sets = cards.SelectMany(c => c.Printings).Select(p => p.Set!).GroupBy(s => s.Prefix).Select(g => g.First()).ToList()
        };

        public static SourceCard YamlCard(string name, int? konamiID = null, int? passcode = null, params SourcePrinting[] printings) => new()
        {
            KonamiID = konamiID,
            Name = name,
            Passcode = passcode,
            Printings = printings,
            Source = CatalogSource.YamlYugi
        };

        public static SourceCard YGOProDeckCard(string name, int id, int? konamiID = null, int? betaID = null, params SourcePrinting[] printings) => new()
        {
            BetaID = betaID,
            Images = [new CardCollector.DTO.Image { ID = id, ImageURL = $"https://images.example/{id}.jpg" }],
            KonamiID = konamiID,
            Name = name,
            Printings = printings,
            Source = CatalogSource.YGOProDeck,
            YGOProDeckID = id
        };
    }
}
