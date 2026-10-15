using System.Globalization;
using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.ViewModels;
using Newtonsoft.Json.Linq;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Turns stored review items into inbox rows: the decisions each kind allows, button wording, the payload as readable
    /// lines, and the printings or cards an unmapped product or legacy row can be placed on.
    /// </summary>
    public static class CatalogReviewPresenter
    {
        private static readonly IReadOnlySet<string> HiddenRowColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DateCreated",
            "DateModified",
            "IsPlaceholder"
        };

        /// <summary>
        /// The decisions an item of this kind and status allows. A rejected or dismissed item can only be reopened; one that
        /// was accepted or resolved by its source allows nothing, since its effect is already applied.
        /// </summary>
        public static IReadOnlyList<CatalogReviewAction> AllowedActions(CatalogReviewItemKind kind, CatalogReviewItemStatus status)
        {
            if (status is CatalogReviewItemStatus.Rejected or CatalogReviewItemStatus.Dismissed)
                return [CatalogReviewAction.Reopen];
            if (status != CatalogReviewItemStatus.Open)
                return [];

            return kind switch
            {
                CatalogReviewItemKind.CardNameChanged or CatalogReviewItemKind.CardNotSeen or CatalogReviewItemKind.PasscodeChanged
                    or CatalogReviewItemKind.PrintingNotSeen or CatalogReviewItemKind.TCGListingChanged
                    => [CatalogReviewAction.Accept, CatalogReviewAction.Reject],
                CatalogReviewItemKind.LegacyRowUnmapped
                    => [CatalogReviewAction.MapToPrinting, CatalogReviewAction.CreatePrinting, CatalogReviewAction.Reject],
                CatalogReviewItemKind.UnmappedTCGProduct
                    => [CatalogReviewAction.MapToPrinting, CatalogReviewAction.CreatePrinting, CatalogReviewAction.CreateCard, CatalogReviewAction.Reject],
                _ => [CatalogReviewAction.Dismiss]
            };
        }

        /// <summary>
        /// "SET-EN001 Ultra Rare", with " (Variant)" appended when there is one, and prefixed with "Card — " when
        /// <paramref name="includeCardName"/> is true (when a picker's options span more than one card).
        /// </summary>
        public static string DescribePrinting(Card card, Set printing, bool includeCardName = true)
        {
            if (card is null) throw new ArgumentNullException(nameof(card));
            if (printing is null) throw new ArgumentNullException(nameof(printing));

            var variant = string.IsNullOrWhiteSpace(printing.PrintVariant) ? string.Empty : $" ({printing.PrintVariant})";
            var label = $"{printing.Code} {printing.RarityName}{variant}";
            return includeCardName ? $"{card.Name} — {label}" : label;
        }

        /// <summary>Reads a payload property as text; null when it is missing or null.</summary>
        public static string? ReadString(JObject payload, string property) =>
            payload is null ? throw new ArgumentNullException(nameof(payload))
            : payload[property] is JValue { Value: not null } value ? Convert.ToString(value.Value, CultureInfo.InvariantCulture) : null;

        public static CatalogReviewItemViewModel ToViewModel(CatalogReviewItem item, CatalogReviewLookup lookup, string? errorMessage = null)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            if (lookup is null) throw new ArgumentNullException(nameof(lookup));

            var payload = ParsePayload(item.PayloadJson);
            var actions = AllowedActions(item.Kind, item.Status);

            // Creating a card is only offered when no catalog card has the product's name, so it can't make a duplicate.
            if (actions.Contains(CatalogReviewAction.CreateCard)
                && (ReadString(payload, "CardName") is not { } productCardName || lookup.CardsByName(productCardName).Count > 0))
                actions = actions.Where(a => a != CatalogReviewAction.CreateCard).ToList();

            var card = item.CatalogCardID is int cardID ? lookup.CardByID(cardID) : null;
            var placesOnPrinting = actions.Contains(CatalogReviewAction.MapToPrinting);
            var (printingOptions, cardOptions) = placesOnPrinting ? BuildPlacementOptions(item.Kind, payload, lookup) : ([], []);
            var (acceptLabel, rejectLabel) = Labels(item.Kind, payload);

            return new CatalogReviewItemViewModel
            {
                AcceptLabel = acceptLabel,
                Actions = actions,
                CardName = card?.Name ?? ReadString(payload, "CardName") ?? ReadString(payload, "Name"),
                CardOptions = cardOptions,
                CatalogCardID = item.CatalogCardID,
                Details = BuildDetails(payload),
                DetectionCount = item.DetectionCount,
                ErrorMessage = errorMessage,
                FirstDetectedUTC = item.FirstDetectedUTC,
                ID = item.ID,
                ImageURLSmall = card?.CardImages?.FirstOrDefault()?.ImageURLSmall,
                IsActionable = item.Kind.IsActionable(),
                Kind = item.Kind,
                LastDetectedUTC = item.LastDetectedUTC,
                PrintingOptions = printingOptions,
                RejectLabel = rejectLabel,
                Resolution = item.Resolution,
                ResolvedUTC = item.ResolvedUTC,
                Status = item.Status,
                Summary = item.Summary
            };
        }

        private static IReadOnlyList<KeyValuePair<string, string>> BuildDetails(JObject payload)
        {
            var details = new List<KeyValuePair<string, string>>();
            foreach (var property in payload.Properties())
            {
                var text = property.Value switch
                {
                    JObject row => string.Join(", ", row.Properties()
                        .Where(p => p.Value.Type != JTokenType.Null && !HiddenRowColumns.Contains(p.Name))
                        .Select(p => $"{p.Name} {FormatValue(p.Value)}")),
                    JArray array => string.Join(", ", array.Select(FormatValue)),
                    _ => FormatValue(property.Value)
                };
                if (!string.IsNullOrWhiteSpace(text))
                    details.Add(new KeyValuePair<string, string>(property.Name, text));
            }

            return details;
        }

        /// <summary>
        /// The printings an unmapped product or legacy row could be placed on (every printing under its set code, else every
        /// printing of the card it names), and the cards a missing printing could be created on.
        /// </summary>
        private static (IReadOnlyList<CatalogReviewOption> Printings, IReadOnlyList<CatalogReviewOption> Cards) BuildPlacementOptions(
            CatalogReviewItemKind kind,
            JObject payload,
            CatalogReviewLookup lookup)
        {
            string? setCode;
            var namedCards = new List<Card>();
            if (kind == CatalogReviewItemKind.UnmappedTCGProduct)
            {
                setCode = ReadString(payload, "SetCode");
                if (ReadString(payload, "CardName") is { } name)
                    namedCards.AddRange(lookup.CardsByName(name));
            }
            else
            {
                var row = payload["Row"] as JObject ?? new JObject();
                setCode = ReadString(row, "SetCode");
                if (int.TryParse(ReadString(row, "CardID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var passcode)
                    && lookup.CardByPasscode(passcode) is { } passcodeCard)
                    namedCards.Add(passcodeCard);
            }

            var underCode = setCode is not null ? lookup.PrintingsBySetCode(setCode) : [];
            var candidates = underCode.Count > 0
                ? underCode
                : namedCards.SelectMany(c => (c.CardSets ?? []).Select(s => new CatalogPrintingRef(c, s))).ToList();

            // The row already names the card, so the card name only helps when the options belong to different cards.
            var spansCards = candidates.Select(x => x.Card.ID).Distinct().Count() > 1;
            var printingOptions = candidates
                .Where(x => x.Printing.PrintingID > 0)
                .Select(x => new CatalogReviewOption(x.Printing.PrintingID, DescribePrinting(x.Card, x.Printing, spansCards)))
                .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var cardOptions = namedCards
                .Concat(underCode.Select(x => x.Card))
                .DistinctBy(c => c.ID)
                .Select(c => new CatalogReviewOption(c.ID, c.Passcode is int p ? $"{c.Name} ({p})" : c.Name ?? $"Card {c.ID}"))
                .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (printingOptions, cardOptions);
        }

        private static string FormatValue(JToken token) => token.Type switch
        {
            JTokenType.Null => string.Empty,
            JTokenType.Object or JTokenType.Array => token.ToString(Newtonsoft.Json.Formatting.None),
            JTokenType.Date => token.Value<DateTime>().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        private static (string? Accept, string? Reject) Labels(CatalogReviewItemKind kind, JObject payload) => kind switch
        {
            CatalogReviewItemKind.CardNameChanged => ($"Rename to \"{ReadString(payload, "Reported")}\"", "Keep name"),
            CatalogReviewItemKind.CardNotSeen => ("Retire card", "Keep card"),
            CatalogReviewItemKind.LegacyRowUnmapped => (null, "Discard row"),
            CatalogReviewItemKind.PasscodeChanged => ($"Use {ReadString(payload, "Reported")}", $"Keep {ReadString(payload, "Current")}"),
            CatalogReviewItemKind.PrintingNotSeen or CatalogReviewItemKind.TCGListingChanged => ("Retire printing", "Keep printing"),
            CatalogReviewItemKind.UnmappedTCGProduct => (null, "Ignore product"),
            _ => (null, null)
        };

        private static JObject ParsePayload(string payloadJson)
        {
            try
            {
                return JObject.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return new JObject();
            }
        }
    }
}
