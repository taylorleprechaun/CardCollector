using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Maps rows of the legacy database, which identify a printing by (card id = passcode, set code, rarity, variant), to
    /// catalog printings. The set code carries most of the identity, because passcodes are what drifted.
    /// </summary>
    public static class LegacyRowMapper
    {
        public static LegacyMappingIndex BuildIndex(
            CatalogState state,
            IReadOnlyDictionary<(string SetCode, string RarityName), string> rarityCorrections)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (rarityCorrections is null) throw new ArgumentNullException(nameof(rarityCorrections));

            var active = state.Cards.Where(c => c.Status == CatalogStatus.Active).ToList();

            var cardIDsByAlias = active
                .SelectMany(c => c.Aliases.Select(a => (CardID: c.ID, a.Value, Priority: a.Kind.ResolutionPriority(), a.LastSeenUTC)))
                .Concat(active.Where(c => c.Passcode.HasValue).Select(c => (CardID: c.ID, Value: c.Passcode!.Value, Priority: -1, LastSeenUTC: DateTime.MaxValue)))
                .GroupBy(x => x.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g
                        .OrderBy(x => x.Priority)
                        .ThenByDescending(x => x.LastSeenUTC)
                        .Select(x => x.CardID)
                        .Distinct()
                        .ToList());

            var printingsBySetCode = active
                .SelectMany(c => c.Printings
                    .Where(p => p.Status == CatalogStatus.Active)
                    .Select(p => new LegacyPrintingTarget(p.ID, c.ID, p.SetCode, p.RarityName, p.PrintVariant)))
                .GroupBy(p => p.SetCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<LegacyPrintingTarget>)g.ToList(), StringComparer.OrdinalIgnoreCase);

            return new LegacyMappingIndex
            {
                CardIDsByAlias = cardIDsByAlias,
                PrintingsBySetCode = printingsBySetCode,
                RarityCorrections = rarityCorrections
            };
        }

        /// <summary>Returns the catalog card a legacy card id (a passcode or source id) means, or null when no card has it.</summary>
        public static int? MapCard(LegacyMappingIndex index, int legacyCardID)
        {
            if (index is null) throw new ArgumentNullException(nameof(index));

            return index.CardIDsByAlias.TryGetValue(legacyCardID, out var cardIDs) ? cardIDs[0] : null;
        }

        /// <summary>
        /// Finds the catalog printing for a legacy printing reference. The rarity is corrected and normalized and a variant
        /// that only restates the rarity is dropped, as the sync does for sources; then the rules are tried in order:
        /// <see cref="LegacyMappingRule.Exact"/>, <see cref="LegacyMappingRule.CardCorrected"/>,
        /// <see cref="LegacyMappingRule.VariantFilled"/>.
        /// </summary>
        public static LegacyPrintingMatch MapPrinting(LegacyMappingIndex index, int legacyCardID, string setCode, string? rarityName, string? printVariant)
        {
            if (index is null) throw new ArgumentNullException(nameof(index));
            if (string.IsNullOrWhiteSpace(setCode))
                return LegacyPrintingMatch.Unmapped("The row has no set code.");
            if (string.IsNullOrWhiteSpace(rarityName))
                return LegacyPrintingMatch.Unmapped("The row has no rarity.");

            var corrected = index.RarityCorrections.GetValueOrDefault((setCode.ToUpperInvariant(), rarityName.ToUpperInvariant()), rarityName);
            var rarity = RarityExtensions.NormalizeRarityName(corrected) ?? corrected;
            // A variant that restates the rarity (before or after correction) was never a real variant.
            var variant = string.IsNullOrWhiteSpace(printVariant)
                || RarityExtensions.IsRarityRestatement(printVariant, rarity)
                || RarityExtensions.IsRarityRestatement(printVariant, rarityName)
                ? null
                : printVariant;

            if (!index.PrintingsBySetCode.TryGetValue(setCode, out var withCode))
                return LegacyPrintingMatch.Unmapped($"No catalog printing has set code {setCode}.");

            var withRarity = withCode.Where(p => string.Equals(p.RarityName, rarity, StringComparison.OrdinalIgnoreCase)).ToList();
            var exactPrintings = withRarity.Where(p => string.Equals(p.PrintVariant, variant, StringComparison.OrdinalIgnoreCase)).ToList();
            var cardIDs = index.CardIDsByAlias.GetValueOrDefault(legacyCardID) ?? [];

            foreach (var cardID in cardIDs)
            {
                var exact = exactPrintings.FirstOrDefault(p => p.CardID == cardID);
                if (exact is not null)
                    return new LegacyPrintingMatch(exact, LegacyMappingRule.Exact, null);
            }

            if (exactPrintings.Select(p => p.CardID).Distinct().Count() == 1)
                return new LegacyPrintingMatch(exactPrintings[0], LegacyMappingRule.CardCorrected, null);

            if (variant is null)
            {
                var forCard = withRarity
                    .Where(p => cardIDs.Contains(p.CardID) || withRarity.Select(x => x.CardID).Distinct().Count() == 1)
                    .ToList();
                if (forCard.Count == 1)
                    return new LegacyPrintingMatch(forCard[0], LegacyMappingRule.VariantFilled, null);
            }

            var described = variant is null ? $"{setCode} {rarity}" : $"{setCode} {rarity} ({variant})";
            return exactPrintings.Count > 1
                ? LegacyPrintingMatch.Unmapped($"{described} belongs to several catalog cards and the row's card id {legacyCardID} matches none of them.")
                : LegacyPrintingMatch.Unmapped($"The catalog has no printing {described}.");
        }
    }
}
