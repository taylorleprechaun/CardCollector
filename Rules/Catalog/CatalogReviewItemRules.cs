using CardCollector.Data.Models.Catalog;
using CardCollector.Models.Catalog;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace CardCollector.Rules.Catalog
{
    /// <summary>
    /// Merges what a sync run detected into the stored review items, so the inbox accumulates instead of repeating:
    /// <list type="bullet">
    /// <item>An item detected again only gets a newer <see cref="CatalogReviewItem.LastDetectedUTC"/> and a higher count.</item>
    /// <item>A closed item reopens only when the source now reports something different (its payload hash changed), or it
    /// had been resolved by the source.</item>
    /// <item>An open actionable item the run no longer detects is resolved by the source, but only when every source its
    /// kind depends on succeeded this run.</item>
    /// </list>
    /// </summary>
    public static class CatalogReviewItemRules
    {
        public static string ComputeHash(string payloadJSON)
        {
            if (payloadJSON is null) throw new ArgumentNullException(nameof(payloadJSON));

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJSON)));
        }

        /// <summary>
        /// Updates <paramref name="existing"/> in place and returns the items to add. Read the detected items' subject keys
        /// only after the run's new catalog rows have ids.
        /// </summary>
        public static CatalogReviewReconcileResult Reconcile(
            IReadOnlyList<CatalogReviewItem> existing,
            IEnumerable<DetectedReviewItem> detected,
            IReadOnlySet<CatalogSource> succeededSources,
            int? syncRunID,
            DateTime nowUTC)
        {
            if (existing is null) throw new ArgumentNullException(nameof(existing));
            if (detected is null) throw new ArgumentNullException(nameof(detected));
            if (succeededSources is null) throw new ArgumentNullException(nameof(succeededSources));

            var byKey = existing.ToDictionary(i => (i.Kind, i.SubjectKey));
            var detectedKeys = new HashSet<(CatalogReviewItemKind, string)>();
            var newItems = new List<CatalogReviewItem>();
            var openedByKind = new Dictionary<CatalogReviewItemKind, int>();
            var actionableOpened = 0;

            void CountOpened(CatalogReviewItemKind kind)
            {
                openedByKind[kind] = openedByKind.GetValueOrDefault(kind) + 1;
                if (kind.IsActionable())
                    actionableOpened++;
            }

            // A subject reported twice in one run keeps its last report.
            var latestByKey = detected
                .GroupBy(d => (d.Kind, d.SubjectKey))
                .Select(g => g.Last());

            foreach (var item in latestByKey)
            {
                var key = (item.Kind, item.SubjectKey);
                detectedKeys.Add(key);
                var payloadJSON = JsonConvert.SerializeObject(item.Payload);
                var payloadHash = ComputeHash(payloadJSON);

                if (!byKey.TryGetValue(key, out var stored))
                {
                    stored = new CatalogReviewItem
                    {
                        CatalogCardID = item.Card?.ID ?? item.Printing?.CatalogCardID ?? item.LinkedCardID,
                        CatalogPrintingID = item.Printing?.ID ?? item.LinkedPrintingID,
                        DateCreated = nowUTC,
                        DateModified = nowUTC,
                        DetectionCount = 1,
                        FirstDetectedUTC = nowUTC,
                        Kind = item.Kind,
                        LastDetectedUTC = nowUTC,
                        LastSyncRunID = syncRunID,
                        PayloadHash = payloadHash,
                        PayloadJSON = payloadJSON,
                        Status = CatalogReviewItemStatus.Open,
                        SubjectKey = item.SubjectKey,
                        Summary = item.Summary
                    };
                    newItems.Add(stored);
                    byKey[key] = stored;
                    CountOpened(item.Kind);
                    continue;
                }

                stored.DetectionCount++;
                stored.LastDetectedUTC = nowUTC;
                stored.LastSyncRunID = syncRunID;
                stored.DateModified = nowUTC;

                var changed = !string.Equals(stored.PayloadHash, payloadHash, StringComparison.Ordinal);
                if (stored.Status == CatalogReviewItemStatus.Open)
                {
                    if (changed)
                        SetPayload(stored, item, payloadJSON, payloadHash);
                    continue;
                }

                if (changed || stored.Status == CatalogReviewItemStatus.ResolvedBySource)
                {
                    SetPayload(stored, item, payloadJSON, payloadHash);
                    stored.Status = CatalogReviewItemStatus.Open;
                    stored.Resolution = null;
                    stored.ResolvedUTC = null;
                    CountOpened(item.Kind);
                }
            }

            var autoResolved = 0;
            foreach (var stored in existing)
            {
                if (stored.Status != CatalogReviewItemStatus.Open || detectedKeys.Contains((stored.Kind, stored.SubjectKey)))
                    continue;

                var required = stored.Kind.SourcesRequiredToAutoResolve();
                if (required.Count == 0 || !required.All(succeededSources.Contains))
                    continue;

                stored.Status = CatalogReviewItemStatus.ResolvedBySource;
                stored.Resolution = "No longer reported by the sources";
                stored.ResolvedUTC = nowUTC;
                stored.DateModified = nowUTC;
                autoResolved++;
            }

            return new CatalogReviewReconcileResult
            {
                ActionableOpened = actionableOpened,
                AutoResolved = autoResolved,
                NewItems = newItems,
                OpenedByKind = openedByKind
            };
        }

        private static void SetPayload(CatalogReviewItem stored, DetectedReviewItem item, string payloadJSON, string payloadHash)
        {
            stored.PayloadJSON = payloadJSON;
            stored.PayloadHash = payloadHash;
            stored.Summary = item.Summary;
            stored.CatalogCardID = item.Card?.ID ?? item.Printing?.CatalogCardID ?? stored.CatalogCardID;
            stored.CatalogPrintingID = item.Printing?.ID ?? stored.CatalogPrintingID;
        }
    }
}
