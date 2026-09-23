using System.Globalization;

namespace CardCollector.Models
{
    /// <summary>Every dated TCG banlist, keyed by effective date after overrides are applied, plus the current list. Pure and immutable.</summary>
    public sealed class BanlistCollection
    {
        private readonly IReadOnlyList<Banlist> _listsNewestFirst;

        private BanlistCollection(IReadOnlyList<Banlist> listsNewestFirst, Banlist? current)
        {
            _listsNewestFirst = listsNewestFirst;
            Current = current;
            Dates = listsNewestFirst.Select(l => l.EffectiveDate).ToList();
        }

        public Banlist? Current { get; }

        /// <summary>Effective dates of every dated list, newest first.</summary>
        public IReadOnlyList<DateOnly> Dates { get; }

        /// <summary>
        /// Builds the collection from source-dated lists and the current list. An override in
        /// <paramref name="effectiveDateOverrides"/> replaces a list's source date only when it parses and falls
        /// strictly between the neighbouring lists' dates; otherwise it's ignored and logged. Dates that collide
        /// after overrides keep the list with the later source date.
        /// </summary>
        public static BanlistCollection Build(
            IReadOnlyList<Banlist> sourceDatedLists,
            Banlist? current,
            IDictionary<string, string>? effectiveDateOverrides,
            ILogger? logger = null)
        {
            if (sourceDatedLists is null) throw new ArgumentNullException(nameof(sourceDatedLists));

            var bySourceDate = sourceDatedLists
                .OrderBy(l => l.EffectiveDate)
                .ToList();

            var withOverrides = ApplyOverrides(bySourceDate, effectiveDateOverrides, logger);

            var deduplicated = withOverrides
                .GroupBy(entry => entry.List.EffectiveDate)
                .Select(group => group.OrderByDescending(entry => entry.SourceDate).First().List)
                .OrderByDescending(list => list.EffectiveDate)
                .ToList();

            return new BanlistCollection(deduplicated, current);
        }

        /// <summary>The list whose effective date matches exactly, or null when there is none.</summary>
        public Banlist? GetByDate(DateOnly effectiveDate) =>
            _listsNewestFirst.FirstOrDefault(l => l.EffectiveDate == effectiveDate);

        /// <summary>The latest list on or before <paramref name="date"/>, or null when <paramref name="date"/> is before the first list.</summary>
        public Banlist? GetForDate(DateOnly date) =>
            _listsNewestFirst.FirstOrDefault(l => l.EffectiveDate <= date);

        private static IReadOnlyList<(DateOnly SourceDate, Banlist List)> ApplyOverrides(
            IReadOnlyList<Banlist> bySourceDate,
            IDictionary<string, string>? effectiveDateOverrides,
            ILogger? logger)
        {
            var result = new List<(DateOnly SourceDate, Banlist List)>(bySourceDate.Count);

            for (var i = 0; i < bySourceDate.Count; i++)
            {
                var list = bySourceDate[i];
                var sourceDate = list.EffectiveDate;
                var effectiveDate = sourceDate;

                var sourceDateKey = sourceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (effectiveDateOverrides is not null && effectiveDateOverrides.TryGetValue(sourceDateKey, out var overrideText))
                {
                    if (DateOnly.TryParseExact(overrideText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var overrideDate) &&
                        IsStrictlyBetweenNeighbours(bySourceDate, i, overrideDate))
                    {
                        effectiveDate = overrideDate;
                    }
                    else
                    {
                        logger?.LogWarning(
                            "Ignoring invalid or out-of-order EffectiveDateOverrides entry for source date {SourceDate} -> {OverrideText}",
                            sourceDate, overrideText);
                    }
                }

                result.Add((sourceDate, effectiveDate == sourceDate
                    ? list
                    : new Banlist { EffectiveDate = effectiveDate, LimitsByKonamiID = list.LimitsByKonamiID }));
            }

            return result;
        }

        private static bool IsStrictlyBetweenNeighbours(IReadOnlyList<Banlist> bySourceDate, int index, DateOnly candidate)
        {
            var previous = index > 0 ? bySourceDate[index - 1].EffectiveDate : (DateOnly?)null;
            var next = index < bySourceDate.Count - 1 ? bySourceDate[index + 1].EffectiveDate : (DateOnly?)null;

            return (previous is null || candidate > previous) && (next is null || candidate < next);
        }
    }
}
