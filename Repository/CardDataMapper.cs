using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;

namespace CardCollector.Repository
{
    /// <summary>
    /// Pure parsing/derivation logic for turning yaml-yugi card data into the app's <see cref="Card"/> shape.
    /// Extracted from <see cref="CardDataRepository"/> so it's testable without touching file I/O or HTTP.
    /// </summary>
    public static class CardDataMapper
    {
        public static void AttachImages(IReadOnlyList<Card> cards, IReadOnlyDictionary<int, IReadOnlyList<Image>> imagesByCardID)
        {
            foreach (var card in cards)
            {
                if (imagesByCardID.TryGetValue(card.ID, out var images))
                    card.CardImages = images;
                else
                    card.CardImages = [BuildFallbackImage(card.ID)];
            }
        }

        public static Image BuildFallbackImage(int cardID) => new()
        {
            ID = cardID,
            ImageURL = $"https://images.ygoprodeck.com/images/cards/{cardID}.jpg",
            ImageURLSmall = $"https://images.ygoprodeck.com/images/cards_small/{cardID}.jpg"
        };

        public static (IReadOnlyDictionary<string, string> namesByCode, IReadOnlyDictionary<string, string> prefixByName) BuildSetNameIndex(IReadOnlyList<Card> cards)
        {
            // Step 1: prefix → shortest canonical name (shorter names are the base set, not promo variants)
            var prefixToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
                foreach (var set in card.CardSets ?? [])
                    if (!string.IsNullOrEmpty(set.Code) && !string.IsNullOrEmpty(set.Name))
                    {
                        var prefix = GetSetPrefix(set.Code!);
                        if (!prefixToName.TryGetValue(prefix, out var existing) || set.Name!.Length < existing.Length)
                            prefixToName[prefix] = set.Name!;
                    }

            // Step 2: full card code → canonical name
            var namesByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
                foreach (var set in card.CardSets ?? [])
                    if (!string.IsNullOrEmpty(set.Code) && prefixToName.TryGetValue(GetSetPrefix(set.Code!), out var canonicalName))
                        namesByCode.TryAdd(set.Code!, canonicalName);

            // Step 3: canonical name → prefix (reverse of step 1)
            var prefixByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (prefix, name) in prefixToName)
                prefixByName.TryAdd(name, prefix);

            return (namesByCode, prefixByName);
        }

        public static IReadOnlyList<Set> BuildSets(YamlCard y)
        {
            var sets = new List<Set>();
            foreach (var entry in y.Sets?.En ?? [])
            {
                if (IsSpeedDuelSet(entry.SetName))
                    continue;

                foreach (var rarity in entry.Rarities ?? [])
                {
                    sets.Add(new Set
                    {
                        Code = entry.SetNumber,
                        Name = entry.SetName,
                        RarityName = rarity,
                        RarityCode = RarityExtensions.GetRarityCode(rarity),
                    });
                }
            }
            return sets;
        }

        public static IReadOnlyList<Card> ConvertYamlCards(IReadOnlyList<YamlCard> yamlCards)
        {
            var result = new List<Card>(yamlCards.Count);
            foreach (var y in yamlCards)
            {
                if (y.Password is not int id)
                    continue;

                var sets = BuildSets(y);
                result.Add(new Card
                {
                    ID = id,
                    Name = y.Name?.En,
                    Description = y.Text?.En,
                    Attribute = y.Attribute,
                    Level = y.Level ?? y.Rank,
                    ATK = int.TryParse(y.Atk, out var atk) ? atk : null,
                    DEF = int.TryParse(y.Def, out var def) ? def : null,
                    LinkRating = y.LinkArrows?.Count,
                    CardType = DeriveCardType(y),
                    Type = DeriveRace(y),
                    CardSets = sets.Count > 0 ? sets : null,
                });
            }
            return result;
        }

        public static string DeriveCardType(YamlCard y) => y.CardType switch
        {
            "Spell" => "Spell Card",
            "Trap" => "Trap Card",
            _ => DeriveMonsterType(y.MonsterTypeLine)
        };

        public static string DeriveMonsterType(string? typeLine)
        {
            if (typeLine is null) return "Normal Monster";
            if (typeLine.Contains("Fusion")) return "Fusion Monster";
            if (typeLine.Contains("Synchro")) return "Synchro Monster";
            if (typeLine.Contains("Xyz")) return "Xyz Monster";
            if (typeLine.Contains("Link")) return "Link Monster";
            if (typeLine.Contains("Ritual")) return "Ritual Monster";
            if (typeLine.Contains("Effect")) return "Effect Monster";
            return "Normal Monster";
        }

        public static string? DeriveRace(YamlCard y)
        {
            if (y.MonsterTypeLine is not null)
                return y.MonsterTypeLine.Split(" / ").First();
            return y.Property;
        }

        /// <summary>
        /// Expands each card's CardSets with print-variant printings found in the tcgcsv catalog (e.g. Extended
        /// Art), which yaml-yugi/YGOProDeck's own set data can't distinguish since they only track rarity, not the
        /// underlying sellable print variant. Matches by card name + set code. For a (SetCode, RarityName) pair
        /// where the catalog also lists a plain (non-variant) product, each variant found is added as a new Set
        /// entry alongside the original base-print entry. Where the catalog has no plain listing for that rarity
        /// at all — the inherited base entry is a mislabeled variant, not a real standalone print (e.g. a card
        /// whose "Starlight Rare" print only ever shipped as Extended Art) — the base entry is rewritten in place
        /// to carry the (first) variant instead of being duplicated alongside a phantom base print.
        /// </summary>
        public static void EnrichWithPrintVariants(IReadOnlyList<Card> cards, IReadOnlyList<TCGPriceSet> catalogPrintings)
        {
            var printingsByCardAndSetCode = catalogPrintings
                .Where(p => !string.IsNullOrWhiteSpace(p.CardName) && !string.IsNullOrWhiteSpace(p.Code))
                .GroupBy(p => (CardName: p.CardName!.ToUpperInvariant(), Code: p.Code.ToUpperInvariant()))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => (RarityName: RarityExtensions.NormalizeRarityName(p.RarityName) ?? p.RarityName, p.PrintVariant))
                        .Distinct()
                        .ToList());

            foreach (var card in cards)
            {
                if (card.CardSets is null || string.IsNullOrWhiteSpace(card.Name))
                    continue;

                var additions = new List<Set>();
                foreach (var set in card.CardSets)
                {
                    if (string.IsNullOrWhiteSpace(set.Code))
                        continue;

                    var lookupKey = (CardName: card.Name.ToUpperInvariant(), Code: set.Code.ToUpperInvariant());
                    if (!printingsByCardAndSetCode.TryGetValue(lookupKey, out var printings))
                        continue;

                    var normalizedSetRarity = RarityExtensions.NormalizeRarityName(set.RarityName) ?? set.RarityName;
                    var rarityPrintings = printings.Where(p => string.Equals(p.RarityName, normalizedSetRarity, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (rarityPrintings.Count == 0)
                        continue;

                    var distinctVariants = rarityPrintings
                        .Where(p => !string.IsNullOrWhiteSpace(p.PrintVariant))
                        .Select(p => p.PrintVariant!)
                        .Distinct()
                        .ToList();
                    if (distinctVariants.Count == 0)
                        continue;

                    var startIndex = 0;
                    var hasPlainPrint = rarityPrintings.Any(p => string.IsNullOrWhiteSpace(p.PrintVariant));
                    if (!hasPlainPrint)
                    {
                        set.PrintVariant = distinctVariants[0];
                        startIndex = 1;
                    }

                    for (var i = startIndex; i < distinctVariants.Count; i++)
                        additions.Add(new Set
                        {
                            Code = set.Code,
                            Name = set.Name,
                            PrintVariant = distinctVariants[i],
                            RarityCode = set.RarityCode,
                            RarityName = set.RarityName
                        });
                }

                if (additions.Count > 0)
                    card.CardSets = card.CardSets.Concat(additions).ToList();
            }
        }

        public static string GetSetPrefix(string code)
        {
            var hyphen = code.IndexOf('-');
            return hyphen > 0 ? code[..hyphen] : code;
        }

        public static bool IsSpeedDuelSet(string? setName) =>
            setName?.Contains("Speed Duel", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Adds any card present in <paramref name="supplementalCards"/> but absent from <paramref name="primaryCards"/>
        /// (by ID), backfilling gaps in the primary source. Used to fill yaml-yugi's card-list gaps from the
        /// already-fetched raw YGOProDeck response, which does not go through <see cref="BuildSets"/> and so needs
        /// its own Speed Duel set filtering applied here.
        /// </summary>
        public static IReadOnlyList<Card> MergeMissingCards(IReadOnlyList<Card> primaryCards, IReadOnlyList<Card> supplementalCards)
        {
            var existingIDs = primaryCards.Select(c => c.ID).ToHashSet();
            var merged = new List<Card>(primaryCards);

            foreach (var card in supplementalCards.Where(c => !existingIDs.Contains(c.ID)))
            {
                card.CardSets = card.CardSets?.Where(s => !IsSpeedDuelSet(s.Name)).ToList();
                merged.Add(card);
            }

            return merged;
        }
    }
}
