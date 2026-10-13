using CardCollector.Data.Models.Catalog;
using CardCollector.DTO;
using CardCollector.Models.Catalog;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Applies what the card sources (yaml-yugi and YGOProDeck) report to the catalog: new cards and printings are added,
    /// card text is updated, and changes that need a decision (a new name or passcode) become review items instead.
    /// </summary>
    /// <remarks>
    /// Existing printings are never rewritten. A new printing's variant is decided once, when it is created: when TCGPlayer
    /// lists the (set code, rarity) only as variants, the printing takes the first variant instead of standing for a plain
    /// print that doesn't exist.
    /// </remarks>
    public static class CatalogCardDiff
    {
        /// <summary>Applies <paramref name="match"/> to <paramref name="state"/>, recording counts and items in <paramref name="result"/>.</summary>
        public static void Apply(
            CatalogState state,
            CatalogCardMatchResult match,
            IReadOnlyList<SourceTCGProduct> tcgProducts,
            CatalogSyncContext context,
            CatalogSetResolver setResolver,
            CatalogDiffResult result)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (match is null) throw new ArgumentNullException(nameof(match));
            if (tcgProducts is null) throw new ArgumentNullException(nameof(tcgProducts));
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (setResolver is null) throw new ArgumentNullException(nameof(setResolver));
            if (result is null) throw new ArgumentNullException(nameof(result));

            var productsByCodeAndRarity = tcgProducts.ToLookup(p => (p.SetCode.ToUpperInvariant(), p.RarityName.ToUpperInvariant()));

            foreach (var (observed, existing) in match.Matches)
            {
                if (existing is null)
                {
                    var card = CreateCard(observed, context);
                    state.Cards.Add(card);
                    result.NewCards.Add(card);
                    result.CardsAdded++;
                    AddPrintings(card, observed, productsByCodeAndRarity, context, setResolver, result, recordItems: false);

                    if (!context.IsBootstrap)
                        result.Items.Add(new DetectedReviewItem
                        {
                            Card = card,
                            Kind = CatalogReviewItemKind.CardAdded,
                            Payload = new { card.Name, card.KonamiID, card.Passcode },
                            Summary = $"New card {observed.Describe()}"
                        });
                    continue;
                }

                if (UpdateCard(existing, observed, context, result))
                    result.CardsUpdated++;
                AddPrintings(existing, observed, productsByCodeAndRarity, context, setResolver, result, recordItems: !context.IsBootstrap);
            }

            foreach (var (observed, claimedCard) in match.Conflicts)
            {
                result.Items.Add(new DetectedReviewItem
                {
                    ExternalSubject = $"observed:{observed.KonamiID?.ToString() ?? observed.PreferredPasscode?.ToString() ?? observed.Name}",
                    Kind = CatalogReviewItemKind.CardMatchConflict,
                    Payload = new
                    {
                        ObservedName = observed.Name,
                        ObservedKonamiID = observed.KonamiID,
                        ObservedAliases = observed.Aliases.Select(a => $"{a.Source} {a.Kind} {a.Value}").ToList(),
                        ClaimedCardID = claimedCard.ID,
                        ClaimedCardName = claimedCard.Name
                    },
                    Summary = $"{observed.Describe()} matches \"{claimedCard.Name}\", which another card already matched; it was not added"
                });
            }
        }

        /// <summary>
        /// The variant a new (set code, rarity) printing starts with: null when TCGPlayer lists a plain product for it (or
        /// nothing at all), otherwise the alphabetically first variant it lists.
        /// </summary>
        public static string? ChooseInitialVariant(string cardName, IEnumerable<SourceTCGProduct> productsForCodeAndRarity)
        {
            var products = productsForCodeAndRarity.ToList();
            var named = products.Where(p => string.Equals(p.CardName, cardName, StringComparison.OrdinalIgnoreCase)).ToList();
            var relevant = named.Count > 0 || products.Select(p => p.CardName).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
                ? named
                : products;

            if (relevant.Count == 0 || relevant.Any(p => p.PrintVariant is null))
                return null;

            return relevant.Select(p => p.PrintVariant!).OrderBy(v => v, StringComparer.Ordinal).First();
        }

        private static void AddPrintings(
            CatalogCard card,
            ObservedCard observed,
            ILookup<(string, string), SourceTCGProduct> productsByCodeAndRarity,
            CatalogSyncContext context,
            CatalogSetResolver setResolver,
            CatalogDiffResult result,
            bool recordItems)
        {
            foreach (var printing in observed.Printings)
            {
                var existing = card.Printings
                    .Where(p => string.Equals(p.SetCode, printing.SetCode, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.RarityName, printing.RarityName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (existing.Count > 0)
                {
                    foreach (var p in existing)
                        p.LastSeenUTC = context.NowUTC;
                    continue;
                }

                var variant = ChooseInitialVariant(
                    card.Name,
                    productsByCodeAndRarity[(printing.SetCode.ToUpperInvariant(), printing.RarityName.ToUpperInvariant())]);
                var created = CatalogPrintingFactory.Create(
                    card, printing.SetCode, printing.SetName, printing.RarityName, variant, setResolver.Resolve(printing.SetCode, printing.SetName), context.NowUTC);
                result.PrintingsAdded++;

                if (recordItems)
                    result.Items.Add(new DetectedReviewItem
                    {
                        Kind = CatalogReviewItemKind.PrintingAdded,
                        Payload = new { CardName = card.Name, created.SetCode, created.RarityName, created.PrintVariant },
                        Printing = created,
                        Summary = $"New printing of \"{card.Name}\": {CatalogPrintingFactory.Describe(created)}"
                    });
            }
        }

        /// <summary>Sets every unlocked text/stat field from the observation and returns the names of those that changed.</summary>
        private static IReadOnlyDictionary<string, (object? Before, object? After)> ApplyFields(CatalogCard card, ObservedCard observed, DateTime now)
        {
            var changes = new Dictionary<string, (object?, object?)>();

            void Set<T>(string field, T current, T observedValue, Action<T> assign)
            {
                if (EqualityComparer<T>.Default.Equals(current, observedValue) || card.IsLocked(field))
                    return;

                changes[field] = (current, observedValue);
                assign(observedValue);
            }

            Set(CatalogCardField.ARCHETYPE, card.Archetype, observed.Archetype, v => card.Archetype = v);
            Set(CatalogCardField.ATK, card.ATK, observed.ATK, v => card.ATK = v);
            Set(CatalogCardField.ATTRIBUTE, card.Attribute, observed.Attribute, v => card.Attribute = v);
            Set(CatalogCardField.CARD_TYPE, card.CardType, observed.CardType, v => card.CardType = v);
            Set(CatalogCardField.DEF, card.DEF, observed.DEF, v => card.DEF = v);
            Set(CatalogCardField.DESCRIPTION, card.Description, observed.Description, v => card.Description = v);
            Set(CatalogCardField.LEVEL, card.Level, observed.Level, v => card.Level = v);
            Set(CatalogCardField.LINK_MARKERS, card.LinkMarkers, observed.LinkMarkers, v => card.LinkMarkers = v);
            Set(CatalogCardField.LINK_RATING, card.LinkRating, observed.LinkRating, v => card.LinkRating = v);
            Set(CatalogCardField.TYPE, card.Type, observed.Type, v => card.Type = v);
            Set(CatalogCardField.TYPE_LINE, card.TypeLine, observed.TypeLine, v => card.TypeLine = v);

            if (changes.Count > 0)
                card.DateModified = now;

            return changes;
        }

        private static CatalogCard CreateCard(ObservedCard observed, CatalogSyncContext context)
        {
            var now = context.NowUTC;
            var card = new CatalogCard
            {
                DateCreated = now,
                DateModified = now,
                FirstSeenUTC = now,
                KonamiID = observed.KonamiID,
                LastSeenUTC = now,
                Name = observed.Name,
                Passcode = observed.PreferredPasscode,
                Status = CatalogStatus.Active
            };
            ApplyFields(card, observed, now);

            foreach (var (source, kind, value) in observed.Aliases)
                card.Aliases.Add(new CatalogCardAlias
                {
                    DateCreated = now,
                    DateModified = now,
                    FirstSeenUTC = now,
                    Kind = kind,
                    LastSeenUTC = now,
                    Source = source,
                    Value = value
                });

            SyncImages(card, observed.Images, now);
            return card;
        }
        private static void SyncAliases(CatalogCard card, ObservedCard observed, CatalogSyncContext context, CatalogDiffResult result)
        {
            var now = context.NowUTC;
            var added = new List<string>();
            foreach (var (source, kind, value) in observed.Aliases)
            {
                var alias = card.Aliases.FirstOrDefault(a => a.Source == source && a.Kind == kind && a.Value == value);
                if (alias is not null)
                {
                    alias.LastSeenUTC = now;
                    continue;
                }

                card.Aliases.Add(new CatalogCardAlias
                {
                    DateCreated = now,
                    DateModified = now,
                    FirstSeenUTC = now,
                    Kind = kind,
                    LastSeenUTC = now,
                    Source = source,
                    Value = value
                });
                added.Add($"{source} {kind} {value}");
            }

            if (added.Count > 0 && !context.IsBootstrap)
                result.Items.Add(new DetectedReviewItem
                {
                    Card = card,
                    Kind = CatalogReviewItemKind.AliasAdded,
                    Payload = new { CardName = card.Name, Added = added },
                    Summary = $"\"{card.Name}\" gained {string.Join(", ", added)}"
                });
        }

        private static void SyncImages(CatalogCard card, IReadOnlyList<Image> images, DateTime now)
        {
            var wanted = images
                .Select((image, index) => (Image: image, SortOrder: index))
                .GroupBy(x => x.Image.ID)
                .Select(g => g.First())
                .ToList();
            var wantedIDs = wanted.Select(x => x.Image.ID).ToHashSet();

            foreach (var stale in card.Images.Where(i => !wantedIDs.Contains(i.ImageID)).ToList())
                card.Images.Remove(stale);

            foreach (var (image, sortOrder) in wanted)
            {
                var existing = card.Images.FirstOrDefault(i => i.ImageID == image.ID);
                if (existing is null)
                {
                    card.Images.Add(new CatalogCardImage
                    {
                        DateCreated = now,
                        DateModified = now,
                        ImageID = image.ID,
                        ImageURL = image.ImageURL,
                        ImageURLSmall = image.ImageURLSmall,
                        SortOrder = sortOrder
                    });
                }
                else if (existing.ImageURL != image.ImageURL || existing.ImageURLSmall != image.ImageURLSmall || existing.SortOrder != sortOrder)
                {
                    existing.ImageURL = image.ImageURL;
                    existing.ImageURLSmall = image.ImageURLSmall;
                    existing.SortOrder = sortOrder;
                    existing.DateModified = now;
                }
            }
        }

        /// <summary>Returns true when any stored field of the card changed.</summary>
        private static bool UpdateCard(CatalogCard card, ObservedCard observed, CatalogSyncContext context, CatalogDiffResult result)
        {
            var now = context.NowUTC;
            card.LastSeenUTC = now;
            SyncAliases(card, observed, context, result);

            if (card.KonamiID is null && observed.KonamiID is not null)
            {
                card.KonamiID = observed.KonamiID;
                card.DateModified = now;
            }

            if (context.YGOProDeckSucceeded && observed.YGOProDeck.Count > 0)
                SyncImages(card, observed.Images, now);

            // Field values prefer yaml-yugi and fall back to YGOProDeck, so with either source missing a field would
            // appear to change to the other source's wording.
            if (!context.CardSourcesSucceeded)
                return false;

            var changed = false;
            var changes = ApplyFields(card, observed, now);
            if (changes.Count > 0)
            {
                changed = true;
                if (!context.IsBootstrap)
                    result.Items.Add(new DetectedReviewItem
                    {
                        Card = card,
                        Kind = CatalogReviewItemKind.CardTextChanged,
                        Payload = new
                        {
                            CardName = card.Name,
                            Changes = changes.OrderBy(c => c.Key, StringComparer.Ordinal)
                                .ToDictionary(c => c.Key, c => new { c.Value.Before, c.Value.After })
                        },
                        Summary = $"\"{card.Name}\" changed: {string.Join(", ", changes.Keys.OrderBy(k => k, StringComparer.Ordinal))}"
                    });
            }

            if (!string.Equals(card.Name, observed.Name, StringComparison.Ordinal) && !card.IsLocked(CatalogCardField.NAME))
                result.Items.Add(new DetectedReviewItem
                {
                    Card = card,
                    Kind = CatalogReviewItemKind.CardNameChanged,
                    Payload = new { Current = card.Name, Reported = observed.Name },
                    Summary = $"\"{card.Name}\" is now reported as \"{observed.Name}\""
                });

            var reportedPasscode = observed.PreferredPasscode;
            if (card.Passcode is null && reportedPasscode is not null)
            {
                card.Passcode = reportedPasscode;
                card.DateModified = now;
                changed = true;
                if (!context.IsBootstrap)
                    result.Items.Add(new DetectedReviewItem
                    {
                        Card = card,
                        Kind = CatalogReviewItemKind.PasscodeAssigned,
                        Payload = new { CardName = card.Name, Passcode = reportedPasscode },
                        Summary = $"\"{card.Name}\" now has passcode {reportedPasscode}"
                    });
            }
            else if (reportedPasscode is not null && card.Passcode != reportedPasscode && !card.IsLocked(CatalogCardField.PASSCODE))
            {
                result.Items.Add(new DetectedReviewItem
                {
                    Card = card,
                    Kind = CatalogReviewItemKind.PasscodeChanged,
                    Payload = new { CardName = card.Name, Current = card.Passcode, Reported = reportedPasscode },
                    Summary = $"Passcode of \"{card.Name}\" is reported as {reportedPasscode} instead of {card.Passcode}"
                });
            }

            return changed;
        }
    }
}
