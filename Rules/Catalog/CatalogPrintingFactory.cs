using CardCollector.Data.Models.Catalog;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Creates catalog printings the same way wherever a sync adds one.</summary>
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

        /// <summary>"SET-EN001 Ultra Rare", with " (Variant)" appended when there is one.</summary>
        public static string Describe(CatalogPrinting printing)
        {
            if (printing is null) throw new ArgumentNullException(nameof(printing));

            return printing.PrintVariant is null
                ? $"{printing.SetCode} {printing.RarityName}"
                : $"{printing.SetCode} {printing.RarityName} ({printing.PrintVariant})";
        }
    }
}
