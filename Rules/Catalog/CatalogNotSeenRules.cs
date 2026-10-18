using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Reports active cards and printings that no source mentioned this run. Nothing is retired automatically; the review
    /// item lets the user decide. Only runs whose relevant sources all succeeded can tell "gone" from "not downloaded".
    /// </summary>
    /// <remarks>
    /// Printings added by hand are skipped: no source is expected to list them. A card the card sources don't list is not
    /// reported while TCGPlayer still lists one of its products (or couldn't be read this run): some real cards exist only
    /// on TCGPlayer, and a card still on sale shouldn't be retired.
    /// </remarks>
    public static class CatalogNotSeenRules
    {
        public static void Detect(CatalogState state, CatalogSyncContext context, CatalogDiffResult result)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (result is null) throw new ArgumentNullException(nameof(result));

            if (context.IsBootstrap || !context.CardSourcesSucceeded)
                return;

            var now = context.NowUTC;
            foreach (var card in state.Cards.Where(c => c.Status == CatalogStatus.Active))
            {
                if (card.LastSeenUTC < now)
                {
                    var productLinks = card.Printings.SelectMany(p => p.Sources).Where(s => s.Source == CatalogSource.TCGPlayer).ToList();
                    if (productLinks.Count > 0 && (!context.TCGPlayerSucceeded || productLinks.Any(l => l.LastSeenUTC >= now)))
                        continue;

                    result.Items.Add(new DetectedReviewItem
                    {
                        Card = card,
                        Kind = CatalogReviewItemKind.CardNotSeen,
                        Payload = new { card.Name, card.KonamiID, card.Passcode, card.LastSeenUTC },
                        Summary = $"Neither yaml-yugi nor YGOProDeck lists \"{card.Name}\" any more (last seen {card.LastSeenUTC:yyyy-MM-dd})"
                    });
                    continue;
                }

                if (!context.TCGPlayerSucceeded)
                    continue;

                foreach (var printing in card.Printings.Where(p => p.Status == CatalogStatus.Active && p.LastSeenUTC < now && !p.Sources.Any(s => s.Source == CatalogSource.Manual)))
                {
                    result.Items.Add(new DetectedReviewItem
                    {
                        Kind = CatalogReviewItemKind.PrintingNotSeen,
                        Payload = new { CardName = card.Name, printing.SetCode, printing.RarityName, printing.PrintVariant, printing.LastSeenUTC },
                        Printing = printing,
                        Summary = $"No source lists \"{card.Name}\" {CatalogPrintingFactory.Describe(printing)} any more (last seen {printing.LastSeenUTC:yyyy-MM-dd})"
                    });
                }
            }
        }
    }
}
