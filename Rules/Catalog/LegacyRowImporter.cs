using System.Globalization;
using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using CardCollector.Models.Legacy;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Turns legacy rows into the app's entities, table by table, pointed at catalog cards and printings. Rows keep their
    /// legacy ids. A row whose printing (or card) can't be found becomes an actionable review item; a row mapped by
    /// anything but an exact match becomes an informational one. Rows that land on the same catalog printing as an earlier
    /// row in a table with a unique key are folded into it.
    /// </summary>
    public sealed class LegacyRowImporter
    {
        private const string DISMISSED_NEW_PRINTINGS = "DismissedNewPrintings";

        private readonly LegacyMappingIndex _index;
        private readonly ICollection<DetectedReviewItem> _items;
        private readonly LegacyMigrationReport _report;

        public LegacyRowImporter(LegacyMappingIndex index, ICollection<DetectedReviewItem> items, LegacyMigrationReport report)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _report = report ?? throw new ArgumentNullException(nameof(report));
        }

        /// <summary>Builds the CheckedOutCard for a legacy row, pointed at the catalog printing it maps to.</summary>
        public static CheckedOutCard BuildCheckedOutCard(LegacyRow row, LegacyPrintingTarget target) =>
            new()
            {
                CardID = target.CardID,
                CatalogPrintingID = target.PrintingID,
                CheckedOutDate = row.GetDateTime("CheckedOutDate"),
                DateCreated = row.GetDateTime("DateCreated"),
                DateModified = row.GetDateTime("DateModified"),
                ID = row.GetInt("ID"),
                PrintVariant = target.PrintVariant,
                Quantity = row.GetInt("Quantity"),
                RarityName = target.RarityName,
                SetCode = target.SetCode
            };

        /// <summary>Builds the CollectionEntry for a legacy row, pointed at the catalog printing it maps to.</summary>
        public static CollectionEntry BuildCollectionEntry(LegacyRow row, LegacyPrintingTarget target) =>
            new()
            {
                AcquisitionMethod = row.GetEnum<AcquisitionMethod>("AcquisitionMethod"),
                CardID = target.CardID,
                CatalogPrintingID = target.PrintingID,
                Condition = row.GetEnum<CardCondition>("Condition"),
                DateCreated = row.GetDateTime("DateCreated"),
                DateModified = row.GetDateTime("DateModified"),
                Edition = row.GetEnum<CardEdition>("Edition"),
                ID = row.GetInt("ID"),
                MarketPriceAtEntry = row.GetDecimal("MarketPriceAtEntry"),
                PrintVariant = target.PrintVariant,
                PurchaseDate = row.GetNullableDateTime("PurchaseDate"),
                PurchasePrice = row.GetDecimal("PurchasePrice"),
                Quantity = row.GetInt("Quantity"),
                RarityName = target.RarityName,
                SetCode = target.SetCode,
                Status = row.GetEnum<CollectionStatus>("Status") ?? throw new InvalidOperationException($"CollectionEntries #{row.GetInt("ID")} has no status.")
            };

        /// <summary>Builds the DismissedNewPrinting for a legacy row, pointed at the catalog printing it maps to.</summary>
        public static DismissedNewPrinting BuildDismissedNewPrinting(LegacyRow row, LegacyPrintingTarget target) =>
            new()
            {
                CardID = target.CardID,
                CatalogPrintingID = target.PrintingID,
                DateCreated = row.GetDateTime("DateCreated"),
                DateModified = row.GetDateTime("DateModified"),
                ID = row.GetInt("ID"),
                PrintVariant = target.PrintVariant,
                RarityName = target.RarityName,
                SetCode = target.SetCode
            };

        /// <summary>Builds the PendingOrderLine for a legacy row, pointed at the catalog printing it maps to.</summary>
        public static PendingOrderLine BuildPendingOrderLine(LegacyRow row, LegacyPrintingTarget target) =>
            new()
            {
                AcquisitionMethod = row.GetEnum<AcquisitionMethod>("AcquisitionMethod"),
                CardID = target.CardID,
                CatalogPrintingID = target.PrintingID,
                Condition = row.GetEnum<CardCondition>("Condition"),
                DateCreated = row.GetDateTime("DateCreated"),
                Edition = row.GetEnum<CardEdition>("Edition"),
                ID = row.GetInt("ID"),
                MarketPriceAtEntry = row.GetDecimal("MarketPriceAtEntry"),
                PrintVariant = target.PrintVariant,
                PurchaseDate = row.GetNullableDateTime("PurchaseDate"),
                PurchasePrice = row.GetDecimal("PurchasePrice"),
                Quantity = row.GetInt("Quantity"),
                RarityName = target.RarityName,
                SetCode = target.SetCode
            };

        /// <summary>Builds the PreferredVersion for a legacy row, pointed at the catalog printing it maps to.</summary>
        public static PreferredVersion BuildPreferredVersion(LegacyRow row, LegacyPrintingTarget target) =>
            new()
            {
                CardID = target.CardID,
                CatalogPrintingID = target.PrintingID,
                DateCreated = row.GetDateTime("DateCreated"),
                DateModified = row.GetDateTime("DateModified"),
                DesiredQuantity = row.GetNullableInt("DesiredQuantity") ?? 3,
                ID = row.GetInt("ID"),
                PrintVariant = target.PrintVariant,
                RarityName = target.RarityName,
                SetCode = target.SetCode
            };

        public IReadOnlyList<CheckedOutCard> ImportCheckedOutCards(IReadOnlyList<LegacyRow> rows) =>
            ImportPrintingRows(
                "CheckedOutCards",
                rows,
                BuildCheckedOutCard,
                c => c.CatalogPrintingID,
                (kept, folded) =>
                {
                    kept.Quantity += folded.Quantity;
                    kept.CheckedOutDate = kept.CheckedOutDate <= folded.CheckedOutDate ? kept.CheckedOutDate : folded.CheckedOutDate;
                });

        public IReadOnlyList<CollectionEntry> ImportCollectionEntries(IReadOnlyList<LegacyRow> rows) =>
            ImportPrintingRows(
                "CollectionEntries",
                rows,
                BuildCollectionEntry);

        /// <summary>Deck rows keep the passcode they were pasted with and gain the catalog card it resolves to, when one does.</summary>
        public IReadOnlyList<DeckCard> ImportDeckCards(IReadOnlyList<LegacyRow> rows)
        {
            var report = AddTableReport("DeckCards", rows.Count);
            var cards = rows
                .Select(row => new DeckCard
                {
                    CardID = row.GetInt("CardID"),
                    CatalogCardID = LegacyRowMapper.MapCard(_index, row.GetInt("CardID")),
                    DeckID = row.GetInt("DeckID"),
                    ID = row.GetInt("ID"),
                    Quantity = row.GetInt("Quantity"),
                    Section = row.GetEnum<DeckSection>("Section") ?? DeckSection.Main,
                    SortOrder = row.GetInt("SortOrder")
                })
                .ToList();

            report.Exact = cards.Count(c => c.CatalogCardID.HasValue);
            report.Written = cards.Count;
            return cards;
        }

        public IReadOnlyList<DismissedNewPrinting> ImportDismissedNewPrintings(IReadOnlyList<LegacyRow> rows) =>
            ImportPrintingRows(
                DISMISSED_NEW_PRINTINGS,
                rows,
                BuildDismissedNewPrinting,
                d => d.CatalogPrintingID,
                (_, _) => { });

        public IReadOnlyList<IgnoredCard> ImportIgnoredCards(IReadOnlyList<LegacyRow> rows)
        {
            var report = AddTableReport("IgnoredCards", rows.Count);
            var kept = new Dictionary<int, IgnoredCard>();

            foreach (var row in rows)
            {
                var legacyCardID = row.GetInt("CardID");
                if (LegacyRowMapper.MapCard(_index, legacyCardID) is not int cardID)
                {
                    AddUnmapped("IgnoredCards", row, $"card {legacyCardID}", $"No catalog card has passcode {legacyCardID}.", report);
                    continue;
                }

                if (kept.ContainsKey(cardID))
                {
                    report.Merged++;
                    continue;
                }

                report.Exact++;
                kept[cardID] = new IgnoredCard
                {
                    CardID = cardID,
                    DateCreated = row.GetDateTime("DateCreated"),
                    DateModified = row.GetDateTime("DateModified"),
                    ID = row.GetInt("ID")
                };
            }

            report.Written = kept.Count;
            return kept.Values.ToList();
        }

        public IReadOnlyList<PendingOrderLine> ImportPendingOrderLines(IReadOnlyList<LegacyRow> rows) =>
            ImportPrintingRows(
                "PendingOrderLines",
                rows,
                BuildPendingOrderLine);

        public IReadOnlyList<PreferredVersion> ImportPreferredVersions(IReadOnlyList<LegacyRow> rows) =>
            ImportPrintingRows(
                "PreferredVersions",
                rows,
                BuildPreferredVersion,
                p => p.CatalogPrintingID,
                (kept, folded) =>
                {
                    kept.DesiredQuantity = Math.Max(kept.DesiredQuantity, folded.DesiredQuantity);
                    kept.DateCreated = kept.DateCreated <= folded.DateCreated ? kept.DateCreated : folded.DateCreated;
                });

        private LegacyTableReport AddTableReport(string table, int rowCount)
        {
            var report = new LegacyTableReport { Read = rowCount, Table = table };
            _report.Tables.Add(report);
            return report;
        }

        private void AddUnmapped(string table, LegacyRow row, string description, string reason, LegacyTableReport report)
        {
            var id = row.GetInt("ID");
            report.Unmapped++;
            _report.UnmappedRows.Add(new LegacyUnmappedRow(table, id, description, reason));
            _items.Add(new DetectedReviewItem
            {
                ExternalSubject = $"legacy:{table}:{id}",
                Kind = table == DISMISSED_NEW_PRINTINGS ? CatalogReviewItemKind.LegacyDismissalUnmapped : CatalogReviewItemKind.LegacyRowUnmapped,
                Payload = new { Table = table, Row = row.Values, Reason = reason },
                Summary = $"{table} #{id} ({description}) could not be mapped: {reason}"
            });
        }

        private static void CountRule(LegacyTableReport report, LegacyMappingRule rule)
        {
            switch (rule)
            {
                case LegacyMappingRule.CardCorrected:
                    report.CardCorrected++;
                    break;
                case LegacyMappingRule.Exact:
                    report.Exact++;
                    break;
                case LegacyMappingRule.VariantFilled:
                    report.VariantFilled++;
                    break;
            }
        }

        private static string Describe(LegacyRow row) =>
            string.Join(" ", new[]
            {
                $"card {row.GetNullableInt("CardID")?.ToString(CultureInfo.InvariantCulture)}",
                row.GetString("SetCode"),
                row.GetString("RarityName"),
                row.GetString("PrintVariant") is { Length: > 0 } variant ? $"({variant})" : null
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

        /// <summary>
        /// Maps each row's printing and builds its entity. With <paramref name="uniqueKey"/>, a row whose key an earlier
        /// row already has is folded into that row by <paramref name="fold"/> instead of kept.
        /// </summary>
        private IReadOnlyList<TEntity> ImportPrintingRows<TEntity>(
            string table,
            IReadOnlyList<LegacyRow> rows,
            Func<LegacyRow, LegacyPrintingTarget, TEntity> build,
            Func<TEntity, int?>? uniqueKey = null,
            Action<TEntity, TEntity>? fold = null)
        {
            var report = AddTableReport(table, rows.Count);
            var entities = new List<TEntity>();
            var byKey = new Dictionary<int, TEntity>();

            foreach (var row in rows)
            {
                var match = LegacyRowMapper.MapPrinting(
                    _index, row.GetInt("CardID"), row.GetString("SetCode") ?? string.Empty, row.GetString("RarityName"), row.GetString("PrintVariant"));
                if (match.Target is not { } target)
                {
                    AddUnmapped(table, row, Describe(row), match.Reason ?? "No catalog printing matched.", report);
                    continue;
                }

                CountRule(report, match.Rule);
                if (match.Rule != LegacyMappingRule.Exact)
                    _items.Add(new DetectedReviewItem
                    {
                        ExternalSubject = $"legacy:{table}:{row.GetInt("ID")}",
                        Kind = CatalogReviewItemKind.LegacyRowAutoMapped,
                        Payload = new
                        {
                            Table = table,
                            Row = row.Values,
                            Rule = match.Rule.ToString(),
                            MappedTo = new { target.CardID, target.SetCode, target.RarityName, target.PrintVariant }
                        },
                        Summary = $"{table} #{row.GetInt("ID")} ({Describe(row)}) mapped by {match.Rule} to {target.SetCode} {target.RarityName}{(target.PrintVariant is null ? string.Empty : $" ({target.PrintVariant})")}"
                    });

                var entity = build(row, target);
                if (uniqueKey?.Invoke(entity) is int key)
                {
                    if (byKey.TryGetValue(key, out var kept))
                    {
                        fold!(kept, entity);
                        report.Merged++;
                        continue;
                    }

                    byKey[key] = entity;
                }

                entities.Add(entity);
            }

            report.Written = entities.Count;
            return entities;
        }
    }
}
