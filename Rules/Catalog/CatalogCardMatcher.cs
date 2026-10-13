using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Works out which source records describe the same card, and which catalog card that is, without trusting passcodes:
    /// the sources reassign them (yaml-yugi swapped two on 2026-10-10) and YGOProDeck uses temporary ids before release.
    /// </summary>
    public static class CatalogCardMatcher
    {
        /// <summary>
        /// Groups yaml-yugi documents by Konami ID, then pairs each YGOProDeck card with the group for the same card, trying in order: Konami ID, any of its
        /// ids equal to a yaml-yugi passcode or stand-in id, a set code only one yaml-yugi card has, and a name only one
        /// yaml-yugi card has. A pairing is refused when both sides have different Konami IDs. YGOProDeck cards left
        /// unpaired become observed cards of their own.
        /// </summary>
        public static IReadOnlyList<ObservedCard> Combine(IReadOnlyList<SourceCard> yamlYugi, IReadOnlyList<SourceCard> ygoProDeck)
        {
            if (yamlYugi is null) throw new ArgumentNullException(nameof(yamlYugi));
            if (ygoProDeck is null) throw new ArgumentNullException(nameof(ygoProDeck));

            // yaml-yugi documents sharing a Konami ID are one card; the one listing the most printings leads.
            var groups = yamlYugi
                .GroupBy(y => y.KonamiID?.ToString() ?? $"no-konami:{y.Passcode}:{y.FakePassword}:{y.Name}")
                .Select(g => g.OrderByDescending(y => y.Printings.Count).ThenBy(y => y.Passcode).ToList())
                .ToList();
            var primaries = groups.Select(g => g[0]).ToList();

            var byKonamiID = new Dictionary<int, int>();
            var byPasscode = new Dictionary<int, int>();
            var bySetCode = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, IList<int>>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < groups.Count; i++)
            {
                if (primaries[i].KonamiID is int konamiID)
                    byKonamiID.TryAdd(konamiID, i);
                if (!byName.TryGetValue(primaries[i].Name, out var named))
                    byName[primaries[i].Name] = named = new List<int>();
                named.Add(i);

                foreach (var yaml in groups[i])
                {
                    if (yaml.Passcode is int passcode)
                        byPasscode.TryAdd(passcode, i);
                    if (yaml.FakePassword is int fakePassword)
                        byPasscode.TryAdd(fakePassword, i);
                    foreach (var printing in yaml.Printings)
                    {
                        if (!bySetCode.TryGetValue(printing.SetCode, out var indexes))
                            bySetCode[printing.SetCode] = indexes = [];
                        indexes.Add(i);
                    }
                }
            }

            var pairedByGroup = groups.Select(_ => new List<SourceCard>()).ToList();
            var unpaired = new List<ObservedCard>();

            foreach (var ygo in ygoProDeck)
            {
                var index = FindYamlIndex(ygo, primaries, byKonamiID, byPasscode, bySetCode, byName);
                if (index is int i)
                    pairedByGroup[i].Add(ygo);
                else
                    unpaired.Add(new ObservedCard(null, [ygo]));
            }

            return groups
                .Select((group, i) => new ObservedCard(group[0], pairedByGroup[i], group.Skip(1).ToList()))
                .Concat(unpaired)
                .ToList();
        }

        /// <summary>
        /// Finds the catalog card for each observed card: by Konami ID first, then by any alias value, preferring the alias
        /// with the strongest <see cref="CatalogAliasKindExtensions.ResolutionPriority"/> and then the most recently seen,
        /// and last by exact name among catalog cards that have no Konami ID yet. A catalog card with a different Konami ID than the observed card is never chosen, and no catalog card is given
        /// to two observed cards.
        /// </summary>
        public static CatalogCardMatchResult Match(IReadOnlyList<ObservedCard> observed, IEnumerable<CatalogCard> catalogCards)
        {
            if (observed is null) throw new ArgumentNullException(nameof(observed));
            if (catalogCards is null) throw new ArgumentNullException(nameof(catalogCards));

            var byKonamiID = catalogCards
                .Where(c => c.KonamiID.HasValue)
                .GroupBy(c => c.KonamiID!.Value)
                .ToDictionary(g => g.Key, g => g.First());
            var byAliasValue = catalogCards
                .SelectMany(c => c.Aliases.Select(a => (Card: c, Alias: a)))
                .ToLookup(x => x.Alias.Value);
            var byNameWithoutKonamiID = catalogCards
                .Where(c => c.KonamiID is null)
                .ToLookup(c => c.Name, StringComparer.OrdinalIgnoreCase);

            var cards = new CatalogCard?[observed.Count];
            var claimed = new HashSet<CatalogCard>(ReferenceEqualityComparer.Instance);
            var conflicts = new List<(ObservedCard, CatalogCard)>();

            for (var i = 0; i < observed.Count; i++)
            {
                if (observed[i].KonamiID is int konamiID && byKonamiID.TryGetValue(konamiID, out var card))
                {
                    cards[i] = card;
                    claimed.Add(card);
                }
            }

            var unmatched = Enumerable.Range(0, observed.Count).Where(i => cards[i] is null).ToList();
            var conflicted = new HashSet<int>();

            foreach (var i in unmatched)
            {
                var candidates = observed[i].Aliases
                    .SelectMany(alias => byAliasValue[alias.Value])
                    .Where(x => IsKonamiCompatible(observed[i].KonamiID, x.Card.KonamiID))
                    .OrderBy(x => x.Alias.Kind.ResolutionPriority())
                    .ThenByDescending(x => x.Alias.LastSeenUTC)
                    .Select(x => x.Card)
                    .Distinct(ReferenceEqualityComparer.Instance)
                    .Cast<CatalogCard>()
                    .ToList();

                // A pre-announced card can arrive with nothing but a name; it keeps its catalog card through to the day
                // the sources give it a Konami ID and passcode.
                if (candidates.Count == 0)
                    candidates = byNameWithoutKonamiID[observed[i].Name].ToList();

                var available = candidates.FirstOrDefault(c => !claimed.Contains(c));
                if (available is not null)
                {
                    cards[i] = available;
                    claimed.Add(available);
                }
                else if (candidates.Count > 0)
                {
                    conflicts.Add((observed[i], candidates[0]));
                    conflicted.Add(i);
                }
            }

            return new CatalogCardMatchResult
            {
                Conflicts = conflicts,
                Matches = Enumerable.Range(0, observed.Count)
                    .Where(i => !conflicted.Contains(i))
                    .Select(i => (observed[i], cards[i]))
                    .ToList()
            };
        }

        private static int? FindYamlIndex(
            SourceCard ygo,
            IReadOnlyList<SourceCard> primaries,
            IReadOnlyDictionary<int, int> byKonamiID,
            IReadOnlyDictionary<int, int> byPasscode,
            IReadOnlyDictionary<string, HashSet<int>> bySetCode,
            IReadOnlyDictionary<string, IList<int>> byName)
        {
            if (ygo.KonamiID is int konamiID && byKonamiID.TryGetValue(konamiID, out var konamiIndex))
                return konamiIndex;

            var ids = new[] { ygo.YGOProDeckID, ygo.BetaID }
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Concat(ygo.ArtworkIDs);
            foreach (var id in ids)
            {
                if (byPasscode.TryGetValue(id, out var passcodeIndex) && IsKonamiCompatible(ygo.KonamiID, primaries[passcodeIndex].KonamiID))
                    return passcodeIndex;
            }

            var setCodeIndexes = ygo.Printings
                .SelectMany(p => bySetCode.GetValueOrDefault(p.SetCode) ?? [])
                .Distinct()
                .Where(i => IsKonamiCompatible(ygo.KonamiID, primaries[i].KonamiID))
                .ToList();
            if (setCodeIndexes.Count == 1)
                return setCodeIndexes[0];

            var nameIndexes = (byName.GetValueOrDefault(ygo.Name) ?? [])
                .Where(i => IsKonamiCompatible(ygo.KonamiID, primaries[i].KonamiID))
                .ToList();
            return nameIndexes.Count == 1 ? nameIndexes[0] : null;
        }

        private static bool IsKonamiCompatible(int? first, int? second) => first is null || second is null || first == second;
    }
}
