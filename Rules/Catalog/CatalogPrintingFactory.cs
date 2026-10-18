using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Repository;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Creates catalog printings the same way wherever one is added: by a sync, in the catalog admin, or from the review inbox.</summary>
    public static class CatalogPrintingFactory
    {
        /// <summary>Creates an active printing and adds it to <paramref name="card"/>'s printings.</summary>
        public static CatalogPrinting Create(
            CatalogCard card,
            string setCode,
            string? setName,
            string rarityName,
            string? printVariant,
            CatalogSet set,
            DateTime nowUTC)
        {
            if (card is null) throw new ArgumentNullException(nameof(card));
            if (set is null) throw new ArgumentNullException(nameof(set));

            var printing = new CatalogPrinting
            {
                Card = card,
                DateCreated = nowUTC,
                DateModified = nowUTC,
                FirstSeenUTC = nowUTC,
                LastSeenUTC = nowUTC,
                PrintVariant = printVariant,
                RarityName = rarityName,
                Set = set,
                SetCode = setCode,
                SetName = setName,
                Status = CatalogStatus.Active
            };
            card.Printings.Add(printing);
            return printing;
        }

        /// <summary>
        /// Creates a printing the user asked for (in the catalog admin or the review inbox). It joins the set its set code's
        /// sibling printing uses, else the set with the code's prefix (found by <paramref name="findSetByPrefix"/>), else a new
        /// set passed to <paramref name="addSet"/>. It gets a Manual source, so the sync doesn't report it as no longer seen
        /// when no source lists it.
        /// </summary>
        public static async Task<CatalogPrinting> CreateByHandAsync(
            CatalogCard card,
            string setCode,
            string rarityName,
            string? printVariant,
            string summary,
            Func<string, Task<CatalogSet?>> findSetByPrefix,
            Action<CatalogSet> addSet,
            DateTime nowUTC)
        {
            if (card is null) throw new ArgumentNullException(nameof(card));
            if (findSetByPrefix is null) throw new ArgumentNullException(nameof(findSetByPrefix));
            if (addSet is null) throw new ArgumentNullException(nameof(addSet));

            var sibling = card.Printings.FirstOrDefault(p => string.Equals(p.SetCode, setCode, StringComparison.OrdinalIgnoreCase));
            var prefix = CardDataMapper.GetSetPrefix(setCode);
            var set = sibling?.Set ?? await findSetByPrefix(prefix).ConfigureAwait(false);
            if (set is null)
            {
                set = new CatalogSet { DateCreated = nowUTC, DateModified = nowUTC, Name = prefix, Prefix = prefix };
                addSet(set);
            }

            var printing = Create(card, setCode, sibling?.SetName ?? set.Name, rarityName, printVariant, set, nowUTC);
            printing.Sources.Add(new CatalogPrintingSource
            {
                DateCreated = nowUTC,
                DateModified = nowUTC,
                ExternalKey = $"manual:{Guid.NewGuid():N}",
                ExternalSummary = summary,
                FirstSeenUTC = nowUTC,
                LastSeenUTC = nowUTC,
                Source = CatalogSource.Manual
            });
            return printing;
        }

        /// <summary>"SET-EN001 Ultra Rare", with " (Variant)" appended when there is one.</summary>
        public static string Describe(CatalogPrinting printing)
        {
            if (printing is null) throw new ArgumentNullException(nameof(printing));

            return printing.PrintVariant is null
                ? $"{printing.SetCode} {printing.RarityName}"
                : $"{printing.SetCode} {printing.RarityName} ({printing.PrintVariant})";
        }

        /// <summary>Normalizes a reported rarity and drops a variant that is blank or only restates it.</summary>
        public static (string Rarity, string? Variant) NormalizeRarityAndVariant(string rawRarity, string? rawVariant)
        {
            if (rawRarity is null) throw new ArgumentNullException(nameof(rawRarity));

            var rarity = RarityExtensions.NormalizeRarityName(rawRarity) ?? rawRarity;
            var variant = string.IsNullOrWhiteSpace(rawVariant) || RarityExtensions.IsRarityRestatement(rawVariant, rarity) ? null : rawVariant;
            return (rarity, variant);
        }
    }
}
