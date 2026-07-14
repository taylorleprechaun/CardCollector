using CardCollector.DTO;

namespace CardCollector.Services
{
    /// <summary>
    /// Flags preferred printings worth prioritizing for purchase — printings that are themselves one of only
    /// 1-2 scarce foil prints a card has ever had, sitting 5+ years without a reprint, on a card old enough to judge.
    /// </summary>
    public static class PurchasePriorityAnalyzer
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const int MaxFoilCount = 2;
        private const int MinDebutAgeYears = 2;
        private const int MinFoilAgeYears = 5;

        private static readonly IReadOnlySet<Rarity> _primaryFoilRarities = new HashSet<Rarity>
        {
            Rarity.CollectorsRare,
            Rarity.ExtraSecretRare,
            Rarity.GhostGoldRare,
            Rarity.GhostRare,
            Rarity.GoldRare,
            Rarity.GoldSecretRare,
            Rarity.GrandMasterRare,
            Rarity.MosaicRare,
            Rarity.PlatinumRare,
            Rarity.PlatinumSecretRare,
            Rarity.PremiumGoldRare,
            Rarity.PrismaticSecretRare,
            Rarity.QuarterCenturySecretRare,
            Rarity.SecretRare,
            Rarity.SecretRarePharaohsRare,
            Rarity.ShatterfoilRare,
            Rarity.Starfoil,
            Rarity.StarfoilRare,
            Rarity.StarlightRare,
            Rarity.SuperRare,
            Rarity.TenThousandSecretRare,
            Rarity.UltimateRare,
            Rarity.UltraRare,
            Rarity.UltraRarePharaohsRare,
            Rarity.UltraSecretRare,
        };

        private static readonly IReadOnlySet<Rarity> _secondaryFoilRarities = new HashSet<Rarity>
        {
            Rarity.DuelTerminalNormalParallelRare,
            Rarity.DuelTerminalNormalRareParallelRare,
            Rarity.DuelTerminalRareParallelRare,
            Rarity.DuelTerminalSuperParallelRare,
            Rarity.DuelTerminalUltraParallelRare,
            Rarity.NormalParallelRare,
            Rarity.SuperParallelRare,
            Rarity.UltraParallelRare,
        };

        /// <summary>
        /// Evaluates whether the given preferred printing (setCode/rarityName) is itself part of the card's
        /// scarce foil pool and old enough to be at risk. Card-level completeness is the caller's concern.
        /// </summary>
        public static PurchasePriorityCandidate? Evaluate(
            Card card,
            string preferredSetCode,
            string? preferredRarityName,
            Func<string, string?> resolveTcgDate,
            DateTime asOfUtc)
        {
            if (card is null) throw new ArgumentNullException(nameof(card));
            if (preferredSetCode is null) throw new ArgumentNullException(nameof(preferredSetCode));
            if (resolveTcgDate is null) throw new ArgumentNullException(nameof(resolveTcgDate));

            var datedSets = GetDatedSets(card, resolveTcgDate);
            if (datedSets.Count == 0)
                return null;

            var debutDate = datedSets.Min(x => x.Date)!;
            if (IsAfterCutoff(debutDate, asOfUtc, MinDebutAgeYears))
                return null;

            var foilPool = GetFoilPool(datedSets);
            var foilSetCodes = foilPool.Select(x => x.Set.Code!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (foilSetCodes.Count is 0 or > MaxFoilCount)
                return null;

            var preferredEntry = datedSets.FirstOrDefault(x =>
                string.Equals(x.Set.Code, preferredSetCode, StringComparison.OrdinalIgnoreCase)
                && (preferredRarityName is null || string.Equals(x.Set.RarityName, preferredRarityName, StringComparison.OrdinalIgnoreCase)));

            if (preferredEntry.Set is null)
                return null; // the preferred printing itself has no resolvable date

            if (!IsFoilRarity(preferredEntry.Set.Rarity))
                return null; // the preferred printing isn't a foil at all — no scarcity risk to prioritize

            if (IsAfterCutoff(preferredEntry.Date!, asOfUtc, MinFoilAgeYears))
                return null; // the printing being chased isn't stale enough yet

            return new PurchasePriorityCandidate
            {
                CardID = card.ID,
                CardName = card.Name ?? string.Empty,
                DebutDate = debutDate,
                FoilCount = foilSetCodes.Count,
                PrintingDate = preferredEntry.Date!,
            };
        }

        private static IReadOnlyList<(Set Set, string? Date)> GetDatedSets(Card card, Func<string, string?> resolveTcgDate) =>
            (card.CardSets ?? [])
                .Where(s => !string.IsNullOrEmpty(s.Code))
                .Select(s => (Set: s, Date: resolveTcgDate(s.Code!)))
                .Where(x => x.Date is not null)
                .ToList();

        private static IReadOnlyList<(Set Set, string? Date)> GetFoilPool(IReadOnlyList<(Set Set, string? Date)> datedSets)
        {
            var primary = datedSets.Where(x => _primaryFoilRarities.Contains(x.Set.Rarity)).ToList();
            if (primary.Count > 0)
                return primary;

            return datedSets.Where(x => _secondaryFoilRarities.Contains(x.Set.Rarity)).ToList();
        }

        private static bool IsAfterCutoff(string date, DateTime asOfUtc, int minAgeYears)
        {
            var cutoff = asOfUtc.AddYears(-minAgeYears).ToString(DateFormat);
            return string.Compare(date, cutoff, StringComparison.Ordinal) > 0;
        }

        private static bool IsFoilRarity(Rarity rarity) =>
            _primaryFoilRarities.Contains(rarity) || _secondaryFoilRarities.Contains(rarity);
    }
}
