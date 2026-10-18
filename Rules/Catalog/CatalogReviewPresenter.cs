using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models.Catalog;
using CardCollector.ViewModels;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Turns stored review items into inbox rows: the decisions each kind allows, button wording, the payload as readable
    /// lines, and the printings or cards an unmapped product can be placed on.
    /// </summary>
    public static class CatalogReviewPresenter
    {
        /// <summary>Payload properties the row shows as chips, so the details leave them out.</summary>
        private static readonly IReadOnlySet<string> ChipProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PrintVariant",
            "ProductID",
            "RarityName",
            "SetCode"
        };

        private static readonly IReadOnlyDictionary<string, string> DetailLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CardName"] = "Card name",
            ["LastSeenUTC"] = "Last seen",
            ["MissingProductIDs"] = "Missing products",
            ["PrintVariant"] = "Variant",
            ["ProductID"] = "Product",
            ["RarityName"] = "Rarity",
            ["SetCode"] = "Set code"
        };

        private static readonly IReadOnlySet<string> HiddenRowColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DateCreated",
            "DateModified",
            "IsPlaceholder"
        };

        /// <summary>
        /// The decisions an item of this kind and status allows. A rejected or dismissed item can only be reopened; one that
        /// was accepted or resolved by its source allows nothing, since its effect is already applied. A legacy item is
        /// history: an open one can only be dismissed, and a closed one allows nothing.
        /// </summary>
        public static IReadOnlyList<CatalogReviewAction> AllowedActions(CatalogReviewItemKind kind, CatalogReviewItemStatus status)
        {
            if (kind.IsLegacy())
                return status == CatalogReviewItemStatus.Open ? [CatalogReviewAction.Dismiss] : [];
            if (status is CatalogReviewItemStatus.Rejected or CatalogReviewItemStatus.Dismissed)
                return [CatalogReviewAction.Reopen];
            if (status != CatalogReviewItemStatus.Open)
                return [];

            return kind switch
            {
                CatalogReviewItemKind.CardNameChanged or CatalogReviewItemKind.CardNotSeen or CatalogReviewItemKind.PasscodeChanged
                    or CatalogReviewItemKind.PrintingNotSeen or CatalogReviewItemKind.TCGListingChanged
                    => [CatalogReviewAction.Accept, CatalogReviewAction.Reject],
                CatalogReviewItemKind.UnmappedTCGProduct
                    => [CatalogReviewAction.MapToPrinting, CatalogReviewAction.CreatePrinting, CatalogReviewAction.CreateCard, CatalogReviewAction.Reject],
                _ => [CatalogReviewAction.Dismiss]
            };
        }

        /// <summary>
        /// The decision an inbox row leads with. Accept when the kind has one; for an unmapped product, creating the printing
        /// when no printing has its set code, else mapping onto an exact match, else creating the card, else creating the
        /// printing (rather than mapping onto a different rarity or variant), else mapping; otherwise Dismiss or Reopen. Reject is never primary: it sits apart as the quiet way out.
        /// </summary>
        public static CatalogReviewAction? ChoosePrimaryAction(IReadOnlyList<CatalogReviewAction> actions, CatalogReviewPlacement placement)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            if (placement is null) throw new ArgumentNullException(nameof(placement));

            var canMap = actions.Contains(CatalogReviewAction.MapToPrinting) && placement.Printings.Count > 0;
            var canCreate = actions.Contains(CatalogReviewAction.CreatePrinting) && placement.Cards.Count > 0;

            if (actions.Contains(CatalogReviewAction.Accept))
                return CatalogReviewAction.Accept;
            if (canCreate && placement.SetCode is not null && !placement.SetCodeInCatalog)
                return CatalogReviewAction.CreatePrinting;
            if (canMap && placement.HasExactMatch)
                return CatalogReviewAction.MapToPrinting;
            if (actions.Contains(CatalogReviewAction.CreateCard))
                return CatalogReviewAction.CreateCard;
            if (canCreate)
                return CatalogReviewAction.CreatePrinting;
            if (canMap)
                return CatalogReviewAction.MapToPrinting;
            if (actions.Contains(CatalogReviewAction.Dismiss))
                return CatalogReviewAction.Dismiss;
            if (actions.Contains(CatalogReviewAction.Reopen))
                return CatalogReviewAction.Reopen;
            return null;
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

            var payload = ParsePayload(item.PayloadJSON);
            var actions = AllowedActions(item.Kind, item.Status);

            // Creating a card is only offered when no catalog card has the product's name, so it can't make a duplicate.
            if (actions.Contains(CatalogReviewAction.CreateCard)
                && (ReadString(payload, "CardName") is not { } productCardName || lookup.CardsByName(productCardName).Count > 0))
                actions = actions.Where(a => a != CatalogReviewAction.CreateCard).ToList();

            var card = item.CatalogCardID is int cardID ? lookup.CardByID(cardID) : null;

            // Only a unique name match supplies the art, so a name two cards share never shows the wrong one.
            var displayCard = card
                ?? (ReadString(payload, "CardName") is { } reportedName && lookup.CardsByName(reportedName) is { Count: 1 } named ? named[0] : null);
            var placesOnPrinting = actions.Contains(CatalogReviewAction.MapToPrinting);
            var placement = placesOnPrinting ? BuildPlacement(payload, lookup) : new CatalogReviewPlacement([], [], null, false);
            var (acceptLabel, rejectLabel) = Labels(item.Kind, payload);
            var cardName = card?.Name ?? ReadString(payload, "CardName") ?? ReadString(payload, "Name");
            var headline = BuildHeadline(item.Kind, payload);

            // The details leave out what the row already shows: the chips, the title's card name, and a reason used as the headline.
            var shown = new HashSet<string>(ChipProperties, StringComparer.OrdinalIgnoreCase);
            if (cardName is not null && string.Equals(ReadString(payload, "CardName"), cardName, StringComparison.OrdinalIgnoreCase))
                shown.Add("CardName");
            if (headline is not null && item.Kind == CatalogReviewItemKind.UnmappedTCGProduct)
                shown.Add("Reason");

            return new CatalogReviewItemViewModel
            {
                AcceptLabel = acceptLabel,
                Actions = actions,
                CardName = cardName,
                CardOptions = placement.Cards,
                CatalogCardID = item.CatalogCardID,
                Details = BuildDetails(payload, shown),
                DetectionCount = item.DetectionCount,
                DisplayCardID = displayCard?.ID,
                ErrorMessage = errorMessage,
                FirstDetectedUTC = item.FirstDetectedUTC,
                Headline = headline ?? item.Summary,
                ID = item.ID,
                ImageURLSmall = displayCard?.CardImages?.FirstOrDefault()?.ImageURLSmall,
                IsActionable = item.Kind.IsActionable(),
                Kind = item.Kind,
                LastDetectedUTC = item.LastDetectedUTC,
                PlacementSetCode = placement.SetCode,
                PlacementSetCodeInCatalog = placement.SetCodeInCatalog,
                PrimaryAction = ChoosePrimaryAction(actions, placement),
                PrintingOptions = placement.Printings,
                PrintVariant = ReadString(payload, "PrintVariant"),
                ProductID = int.TryParse(ReadString(payload, "ProductID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var productID) ? productID : null,
                RarityName = ReadString(payload, "RarityName"),
                RejectLabel = rejectLabel,
                Resolution = item.Resolution,
                ResolvedUTC = item.ResolvedUTC,
                SetCode = ReadString(payload, "SetCode"),
                Status = item.Status,
                Summary = item.Summary
            };
        }

        private static IReadOnlyList<KeyValuePair<string, string>> BuildDetails(JObject payload, IReadOnlySet<string> shown)
        {
            var details = new List<KeyValuePair<string, string>>();
            foreach (var property in payload.Properties().Where(p => !shown.Contains(p.Name)))
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
                    details.Add(new KeyValuePair<string, string>(DetailLabel(property.Name), text));
            }

            return details;
        }

        /// <summary>The short "what happened" line for kinds that have one; null (use the summary) for the rest or when the payload lacks a field.</summary>
        private static string? BuildHeadline(CatalogReviewItemKind kind, JObject payload)
        {
            var lastSeen = payload["LastSeenUTC"] is JValue { Type: JTokenType.Date } date
                ? date.Value<DateTime>().ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                : null;

            return kind switch
            {
                CatalogReviewItemKind.AliasAdded when payload["Added"] is JArray { Count: > 0 } added
                    => $"Gained {string.Join(", ", added.Select(FormatValue))}",
                CatalogReviewItemKind.CardAdded => "New card",
                CatalogReviewItemKind.CardNameChanged when ReadString(payload, "Reported") is { } reported => $"Now reported as \"{reported}\"",
                CatalogReviewItemKind.CardNotSeen when lastSeen is not null => $"No card source lists it any more (last seen {lastSeen})",
                CatalogReviewItemKind.CardTextChanged when payload["Changes"] is JObject { Count: > 0 } changes
                    => $"Changed: {string.Join(", ", changes.Properties().Select(p => p.Name))}",
                CatalogReviewItemKind.PasscodeAssigned when ReadString(payload, "Passcode") is { } passcode => $"Now has passcode {passcode}",
                CatalogReviewItemKind.PasscodeChanged when ReadString(payload, "Reported") is { } reported && ReadString(payload, "Current") is { } current
                    => $"Passcode reported as {reported}, not {current}",
                CatalogReviewItemKind.PrintingAdded => "New printing",
                CatalogReviewItemKind.PrintingNotSeen when lastSeen is not null => $"No source lists this printing any more (last seen {lastSeen})",
                CatalogReviewItemKind.TCGListingChanged => "TCGPlayer no longer lists this printing",
                CatalogReviewItemKind.TCGProductChanged => "TCGPlayer edited the product",
                CatalogReviewItemKind.UnmappedTCGProduct when ReadString(payload, "Reason") is { Length: > 0 } reason
                    => char.ToUpperInvariant(reason[0]) + reason[1..],
                _ => null
            };
        }

        /// <summary>
        /// The printings an unmapped product could be placed on (every printing under its set code, else every printing of the
        /// card it names), and the cards a missing printing could be created on. Under the set code, options whose rarity or
        /// variant differ from the reported one say so and sort after the exact matches.
        /// </summary>
        private static CatalogReviewPlacement BuildPlacement(JObject payload, CatalogReviewLookup lookup)
        {
            var setCode = ReadString(payload, "SetCode");
            var rawRarity = ReadString(payload, "RarityName");
            var (rarity, variant) = string.IsNullOrWhiteSpace(rawRarity)
                ? (null, null)
                : CatalogPrintingFactory.NormalizeRarityAndVariant(rawRarity, ReadString(payload, "PrintVariant"));

            IReadOnlyList<Card> namedCards = ReadString(payload, "CardName") is { } name ? lookup.CardsByName(name) : [];

            var underCode = setCode is not null ? lookup.PrintingsBySetCode(setCode) : [];
            var candidates = underCode.Count > 0
                ? underCode
                : namedCards.SelectMany(c => (c.CardSets ?? []).Select(s => new CatalogPrintingRef(c, s))).ToList();

            // The product already names the card, so the card name only helps when the options belong to different cards.
            var spansCards = candidates.Select(x => x.Card.ID).Distinct().Count() > 1;
            var printingOptions = candidates
                .Where(x => x.Printing.PrintingID > 0)
                .Select(x => (Ref: x, Difference: underCode.Count > 0 ? Difference(x.Printing, rarity, variant) : CatalogPlacementDifference.None))
                .OrderBy(x => x.Difference)
                .ThenBy(x => DescribePrinting(x.Ref.Card, x.Ref.Printing, spansCards), StringComparer.OrdinalIgnoreCase)
                .Select(x => new CatalogReviewOption(x.Ref.Printing.PrintingID, DescribePrinting(x.Ref.Card, x.Ref.Printing, spansCards) + x.Difference switch
                {
                    CatalogPlacementDifference.Rarity => " — different rarity",
                    CatalogPlacementDifference.Variant => " — different variant",
                    _ => string.Empty
                }))
                .ToList();
            var cardOptions = namedCards
                .Concat(underCode.Select(x => x.Card))
                .DistinctBy(c => c.ID)
                .Select(c => new CatalogReviewOption(c.ID, c.Passcode is int p ? $"{c.Name} ({p})" : c.Name ?? $"Card {c.ID}"))
                .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hasExactMatch = rarity is not null
                && underCode.Any(x => x.Printing.PrintingID > 0 && Difference(x.Printing, rarity, variant) == CatalogPlacementDifference.None);

            return new CatalogReviewPlacement(printingOptions, cardOptions, setCode, underCode.Count > 0, hasExactMatch);
        }

        /// <summary>A payload property name as a label: a known name's wording, else the name split into words ("ObservedKonamiID" → "Observed Konami ID").</summary>
        private static string DetailLabel(string property) =>
            DetailLabels.TryGetValue(property, out var label)
                ? label
                : Regex.Replace(property, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        
        /// <summary>How a candidate printing differs from the reported rarity and variant; nothing when no rarity was reported.</summary>
        private static CatalogPlacementDifference Difference(Set printing, string? rarity, string? variant)
        {
            if (rarity is null)
                return CatalogPlacementDifference.None;
            if (!string.Equals(RarityExtensions.NormalizeRarityName(printing.RarityName) ?? printing.RarityName, rarity, StringComparison.OrdinalIgnoreCase))
                return CatalogPlacementDifference.Rarity;
            return string.Equals(printing.PrintVariant ?? string.Empty, variant ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                ? CatalogPlacementDifference.None
                : CatalogPlacementDifference.Variant;
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
            CatalogReviewItemKind.PasscodeChanged => ($"Use {ReadString(payload, "Reported")}", $"Keep {ReadString(payload, "Current")}"),
            CatalogReviewItemKind.PrintingNotSeen or CatalogReviewItemKind.TCGListingChanged => ("Retire printing", "Keep printing"),
            CatalogReviewItemKind.UnmappedTCGProduct => (null, "Ignore product"),
            _ => (null, null)
        };

        private static JObject ParsePayload(string payloadJSON)
        {
            try
            {
                return JObject.Parse(string.IsNullOrWhiteSpace(payloadJSON) ? "{}" : payloadJSON);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return new JObject();
            }
        }
    }
}
