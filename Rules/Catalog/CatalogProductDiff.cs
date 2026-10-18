using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Repository;
using System.Globalization;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Links TCGPlayer products to catalog printings by product id. A product already linked keeps its printing for good;
    /// a new one is matched by set code, then card name (only to choose between cards sharing a set code), then rarity and
    /// variant, and a missing variant or rarity becomes a new printing.
    /// </summary>
    public static class CatalogProductDiff
    {
        /// <summary>Applies <paramref name="products"/> to <paramref name="state"/>, recording counts and items in <paramref name="result"/>.</summary>
        public static void Apply(
            CatalogState state,
            IReadOnlyList<SourceTCGProduct> products,
            CatalogSyncContext context,
            CatalogSetResolver setResolver,
            CatalogDiffResult result)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (products is null) throw new ArgumentNullException(nameof(products));
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (setResolver is null) throw new ArgumentNullException(nameof(setResolver));
            if (result is null) throw new ArgumentNullException(nameof(result));

            var now = context.NowUTC;
            var activePrintings = state.Cards
                .Where(c => c.Status == CatalogStatus.Active)
                .SelectMany(c => c.Printings.Where(p => p.Status == CatalogStatus.Active).Select(p => (Card: c, Printing: p)))
                .ToList();
            var printingsByCode = activePrintings
                .GroupBy(x => x.Printing.SetCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IList<(CatalogCard Card, CatalogPrinting Printing)>)g.ToList(), StringComparer.OrdinalIgnoreCase);
            var linksByProductID = state.Cards
                .SelectMany(c => c.Printings)
                .SelectMany(p => p.Sources.Where(s => s.Source == CatalogSource.TCGPlayer).Select(s => (Printing: p, Link: s)))
                .GroupBy(x => x.Link.ExternalKey)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var product in products)
            {
                var key = product.ProductID.ToString(CultureInfo.InvariantCulture);
                if (linksByProductID.TryGetValue(key, out var linked))
                {
                    TouchLink(linked.Printing, linked.Link, product, context, result);
                    continue;
                }

                var (card, reason) = ChooseCard(product, printingsByCode);
                if (card is null)
                {
                    if (!setResolver.IsKnownPrefix(CardDataMapper.GetSetPrefix(product.SetCode)))
                    {
                        result.ProductsIgnored++;
                        continue;
                    }

                    result.ProductsUnmapped++;
                    result.Items.Add(new DetectedReviewItem
                    {
                        ExternalSubject = $"tcg:{key}",
                        Kind = CatalogReviewItemKind.UnmappedTCGProduct,
                        Payload = new { product.ProductID, product.CardName, product.SetCode, product.RarityName, product.PrintVariant, Reason = reason },
                        Summary = $"TCGPlayer product {product.ProductID} \"{product.CardName}\" {product.SetCode} {product.RarityName}{FormatVariant(product.PrintVariant)}: {reason}"
                    });
                    continue;
                }

                var cardPrintings = card.Printings
                    .Where(p => p.Status == CatalogStatus.Active && string.Equals(p.SetCode, product.SetCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var printing = cardPrintings.FirstOrDefault(p =>
                    string.Equals(p.RarityName, product.RarityName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.PrintVariant, product.PrintVariant, StringComparison.OrdinalIgnoreCase));

                if (printing is null)
                {
                    var sibling = cardPrintings.First();
                    printing = CatalogPrintingFactory.Create(
                        card, sibling.SetCode, sibling.SetName, product.RarityName, product.PrintVariant, sibling.Set ?? setResolver.Resolve(sibling.SetCode, sibling.SetName), now);
                    printingsByCode[sibling.SetCode].Add((card, printing));
                    result.PrintingsAdded++;

                    if (!context.IsBootstrap)
                        result.Items.Add(new DetectedReviewItem
                        {
                            Kind = CatalogReviewItemKind.PrintingAdded,
                            Payload = new { CardName = card.Name, printing.SetCode, printing.RarityName, printing.PrintVariant, product.ProductID },
                            Printing = printing,
                            Summary = $"New printing of \"{card.Name}\" from TCGPlayer: {CatalogPrintingFactory.Describe(printing)}"
                        });
                }

                var link = new CatalogPrintingSource
                {
                    DateCreated = now,
                    DateModified = now,
                    ExternalKey = key,
                    ExternalSummary = product.Summary,
                    FirstSeenUTC = now,
                    LastSeenUTC = now,
                    Source = CatalogSource.TCGPlayer
                };
                printing.Sources.Add(link);
                printing.LastSeenUTC = now;
                linksByProductID[key] = (printing, link);
                result.ProductsLinked++;
            }

            if (!context.IsBootstrap)
                DetectVanishedListings(activePrintings.Select(x => x.Printing), context, result);
        }

        /// <summary>
        /// Picks the card a new product belongs to: the only card with a printing under its set code, or else the one whose
        /// name matches. Returns the reason instead when no card or more than one fits.
        /// </summary>
        private static (CatalogCard? Card, string? Reason) ChooseCard(
            SourceTCGProduct product,
            IReadOnlyDictionary<string, IList<(CatalogCard Card, CatalogPrinting Printing)>> printingsByCode)
        {
            if (!printingsByCode.TryGetValue(product.SetCode, out var withCode) || withCode.Count == 0)
                return (null, "no catalog printing has this set code");

            var cards = withCode.Select(x => x.Card).Distinct(ReferenceEqualityComparer.Instance).Cast<CatalogCard>().ToList();
            if (cards.Count == 1)
                return (cards[0], null);

            var named = cards.Where(c => string.Equals(c.Name, product.CardName, StringComparison.OrdinalIgnoreCase)).ToList();
            return named.Count == 1
                ? (named[0], null)
                : (null, $"{cards.Count} catalog cards share this set code and none is uniquely named \"{product.CardName}\"");
        }

        /// <summary>
        /// A printing whose TCGPlayer products all disappeared this run, while TCGPlayer itself was read fine, is reported:
        /// usually TCGPlayer merged or relisted it (the plain vs. variant churn that used to rewrite printings).
        /// </summary>
        private static void DetectVanishedListings(IEnumerable<CatalogPrinting> printings, CatalogSyncContext context, CatalogDiffResult result)
        {
            foreach (var printing in printings)
            {
                var links = printing.Sources.Where(s => s.Source == CatalogSource.TCGPlayer).ToList();
                if (links.Count == 0 || links.Any(l => l.LastSeenUTC >= context.NowUTC))
                    continue;

                var cardName = printing.Card?.Name ?? string.Empty;
                result.Items.Add(new DetectedReviewItem
                {
                    Kind = CatalogReviewItemKind.TCGListingChanged,
                    Payload = new
                    {
                        CardName = cardName,
                        printing.SetCode,
                        printing.RarityName,
                        printing.PrintVariant,
                        MissingProductIDs = links.Select(l => l.ExternalKey).OrderBy(k => k, StringComparer.Ordinal).ToList()
                    },
                    Printing = printing,
                    Summary = $"TCGPlayer no longer lists \"{cardName}\" {CatalogPrintingFactory.Describe(printing)}"
                });
            }
        }

        private static string FormatVariant(string? printVariant) => printVariant is null ? string.Empty : $" ({printVariant})";

        private static void TouchLink(
            CatalogPrinting printing,
            CatalogPrintingSource link,
            SourceTCGProduct product,
            CatalogSyncContext context,
            CatalogDiffResult result)
        {
            link.LastSeenUTC = context.NowUTC;
            printing.LastSeenUTC = context.NowUTC;

            if (string.Equals(link.ExternalSummary, product.Summary, StringComparison.Ordinal))
                return;

            if (!context.IsBootstrap)
                result.Items.Add(new DetectedReviewItem
                {
                    ExternalSubject = $"tcg:{product.ProductID}",
                    Kind = CatalogReviewItemKind.TCGProductChanged,
                    Payload = new { product.ProductID, Before = link.ExternalSummary, After = product.Summary },
                    Printing = printing,
                    Summary = $"TCGPlayer edited product {product.ProductID}: \"{link.ExternalSummary}\" is now \"{product.Summary}\""
                });

            link.ExternalSummary = product.Summary;
            link.DateModified = context.NowUTC;
        }
    }
}
