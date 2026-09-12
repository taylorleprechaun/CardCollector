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
        /// <summary>
        /// Rewrites any Set row whose (SetCode, RarityName) matches a known-bad entry in <paramref name="corrections"/>
        /// to the corrected rarity name, recomputing RarityCode to match. Matches case-insensitively on both
        /// SetCode and RarityName. Returns the number of rows corrected, for caller-side logging.
        /// </summary>
        public static int ApplyRarityCorrections(IReadOnlyList<Card> cards, IReadOnlyDictionary<(string SetCode, string RarityName), string> corrections)
        {
            var correctedCount = 0;

            foreach (var card in cards)
            {
                if (card.CardSets is null)
                    continue;

                foreach (var set in card.CardSets)
                {
                    if (string.IsNullOrWhiteSpace(set.Code) || string.IsNullOrWhiteSpace(set.RarityName))
                        continue;

                    if (!corrections.TryGetValue((set.Code.ToUpperInvariant(), set.RarityName.ToUpperInvariant()), out var newRarityName))
                        continue;

                    set.RarityName = newRarityName;
                    set.RarityCode = RarityExtensions.GetRarityCode(newRarityName);
                    correctedCount++;
                }
            }

            return correctedCount;
        }

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
        /// Expands each card's CardSets with print-variant printings from the tcgcsv catalog (e.g. Extended Art),
        /// matched by card name + set code. Existing rows are split into variants via <see cref="ApplyVariantSplits"/>;
        /// any tcgcsv rarity under a set code with no existing row is added as a new row, inheriting Name from a
        /// sibling row. Returns the number of new-rarity rows added, for caller-side logging.
        /// </summary>
        public static int EnrichWithPrintVariants(IReadOnlyList<Card> cards, IReadOnlyList<TCGPriceSet> catalogPrintings)
        {
            var printingsByCardAndSetCode = catalogPrintings
                .Where(p => !string.IsNullOrWhiteSpace(p.CardName) && !string.IsNullOrWhiteSpace(p.Code))
                .GroupBy(p => (CardName: p.CardName!.ToUpperInvariant(), Code: p.Code.ToUpperInvariant()))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => (RarityName: RarityExtensions.NormalizeRarityName(p.RarityName) ?? p.RarityName, p.PrintVariant))
                        .Distinct()
                        .ToList());

            var newRarityRowsAdded = 0;

            foreach (var card in cards)
            {
                if (card.CardSets is null || string.IsNullOrWhiteSpace(card.Name))
                    continue;

                var additions = new List<Set>();

                // Grouped by SetCode so the new-rarity pass below runs once per (CardName, SetCode), not once per
                // existing sibling row sharing that SetCode.
                var groupsByCode = card.CardSets
                    .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                    .GroupBy(s => s.Code!.ToUpperInvariant());

                foreach (var group in groupsByCode)
                {
                    var lookupKey = (CardName: card.Name.ToUpperInvariant(), Code: group.Key);
                    if (!printingsByCardAndSetCode.TryGetValue(lookupKey, out var printings))
                        continue;

                    foreach (var set in group)
                        ApplyVariantSplits(set, printings, additions);

                    var existingRaritiesInGroup = group
                        .Select(s => RarityExtensions.NormalizeRarityName(s.RarityName) ?? s.RarityName)
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var templateSet = group.First();

                    foreach (var rarityGroup in printings
                        .Where(p => !string.IsNullOrWhiteSpace(p.RarityName) && !existingRaritiesInGroup.Contains(p.RarityName))
                        .GroupBy(p => p.RarityName, StringComparer.OrdinalIgnoreCase))
                    {
                        var rarityName = rarityGroup.Key;
                        var newSet = new Set
                        {
                            Code = templateSet.Code,
                            Name = templateSet.Name,
                            RarityName = rarityName,
                            RarityCode = RarityExtensions.GetRarityCode(rarityName)
                        };
                        additions.Add(newSet);
                        newRarityRowsAdded++;
                        ApplyVariantSplits(newSet, printings, additions);
                    }
                }

                if (additions.Count > 0)
                    card.CardSets = card.CardSets.Concat(additions).ToList();
            }

            return newRarityRowsAdded;
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

        /// <summary>
        /// For cards present in both <paramref name="primaryCards"/> and <paramref name="supplementalCards"/>
        /// (matched by CardID), adds any (SetCode, RarityName) combo the supplemental card has that the primary
        /// card doesn't. Rarity is compared by normalized spelling, garbage rarity strings are filtered via
        /// <see cref="RarityExtensions.ParseRarity"/>, and Speed Duel sets are stripped. Returns the number of
        /// Set rows added, for caller-side logging.
        /// </summary>
        public static int MergeMissingSetPrintings(IReadOnlyList<Card> primaryCards, IReadOnlyList<Card> supplementalCards)
        {
            var supplementalByID = supplementalCards
                .GroupBy(c => c.ID)
                .ToDictionary(g => g.Key, g => g.First());

            var addedCount = 0;

            foreach (var primaryCard in primaryCards)
            {
                if (!supplementalByID.TryGetValue(primaryCard.ID, out var supplementalCard) || supplementalCard.CardSets is null)
                    continue;

                var existingCombos = (primaryCard.CardSets ?? [])
                    .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                    .Select(s => (
                        Code: s.Code!.ToUpperInvariant(),
                        Rarity: (RarityExtensions.NormalizeRarityName(s.RarityName) ?? s.RarityName ?? string.Empty).ToUpperInvariant()))
                    .ToHashSet();

                var additions = new List<Set>();
                foreach (var supplementalSet in supplementalCard.CardSets)
                {
                    if (string.IsNullOrWhiteSpace(supplementalSet.Code) || IsSpeedDuelSet(supplementalSet.Name))
                        continue;

                    var normalizedRarity = RarityExtensions.NormalizeRarityName(supplementalSet.RarityName) ?? supplementalSet.RarityName;
                    if (RarityExtensions.ParseRarity(normalizedRarity) == Rarity.Error)
                        continue;

                    var comboKey = (Code: supplementalSet.Code.ToUpperInvariant(), Rarity: (normalizedRarity ?? string.Empty).ToUpperInvariant());
                    if (!existingCombos.Add(comboKey))
                        continue;

                    additions.Add(new Set
                    {
                        Code = supplementalSet.Code,
                        Name = supplementalSet.Name,
                        RarityName = supplementalSet.RarityName,
                        RarityCode = supplementalSet.RarityCode,
                        PrintVariant = supplementalSet.PrintVariant
                    });
                }

                if (additions.Count > 0)
                {
                    primaryCard.CardSets = (primaryCard.CardSets ?? []).Concat(additions).ToList();
                    addedCount += additions.Count;
                }
            }

            return addedCount;
        }

        /// <summary>
        /// Expands baseSet into its distinct tcgcsv print variants for the given rarity. When the catalog also
        /// lists a plain (non-variant) product for that rarity, each variant is added as a new sibling row;
        /// otherwise baseSet is rewritten in place to carry the first variant instead of duplicating a
        /// nonexistent plain print.
        /// </summary>
        private static void ApplyVariantSplits(Set baseSet, List<(string RarityName, string? PrintVariant)> printings, List<Set> additions)
        {
            var normalizedRarity = RarityExtensions.NormalizeRarityName(baseSet.RarityName) ?? baseSet.RarityName;
            var rarityPrintings = printings.Where(p => string.Equals(p.RarityName, normalizedRarity, StringComparison.OrdinalIgnoreCase)).ToList();
            if (rarityPrintings.Count == 0)
                return;

            var distinctVariants = rarityPrintings
                .Where(p => !string.IsNullOrWhiteSpace(p.PrintVariant))
                .Select(p => p.PrintVariant!)
                .Distinct()
                .ToList();
            if (distinctVariants.Count == 0)
                return;

            var startIndex = 0;
            var hasPlainPrint = rarityPrintings.Any(p => string.IsNullOrWhiteSpace(p.PrintVariant));
            if (!hasPlainPrint)
            {
                baseSet.PrintVariant = distinctVariants[0];
                startIndex = 1;
            }

            for (var i = startIndex; i < distinctVariants.Count; i++)
                additions.Add(new Set
                {
                    Code = baseSet.Code,
                    Name = baseSet.Name,
                    PrintVariant = distinctVariants[i],
                    RarityCode = baseSet.RarityCode,
                    RarityName = baseSet.RarityName
                });
        }
    }
}
