using CardCollector.DTO;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace CardCollector.Repository.Sources
{
    /// <summary>
    /// Loads the curated table of known-bad (set code, rarity) pairs in <c>Config/RarityCorrections.json</c> and the rarity
    /// each should read as instead.
    /// </summary>
    public static class RarityCorrectionLoader
    {
        /// <summary>Loads the table from its standard location; empty when the file is missing or malformed.</summary>
        [ExcludeFromCodeCoverage(Justification = "Reads the corrections file from disk; the parsing is covered through Parse.")]
        public static IReadOnlyDictionary<(string SetCode, string RarityName), string> Load(ILogger logger)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Config", "RarityCorrections.json");
            return File.Exists(path)
                ? Parse(File.ReadAllText(path), logger)
                : new Dictionary<(string, string), string>();
        }

        /// <summary>
        /// Parses the table, keyed by upper-cased (set code, old rarity name) for case-insensitive lookup. Entries missing a
        /// field are skipped; malformed JSON yields an empty table.
        /// </summary>
        public static IReadOnlyDictionary<(string SetCode, string RarityName), string> Parse(string json, ILogger? logger = null)
        {
            try
            {
                var entries = JsonConvert.DeserializeObject<List<RarityCorrectionEntry>>(json) ?? [];
                return entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.SetCode) && !string.IsNullOrWhiteSpace(e.OldRarityName) && !string.IsNullOrWhiteSpace(e.NewRarityName))
                    .GroupBy(e => (e.SetCode!.ToUpperInvariant(), e.OldRarityName!.ToUpperInvariant()))
                    .ToDictionary(g => g.Key, g => g.First().NewRarityName!);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse rarity correction table — no corrections will be applied");
                return new Dictionary<(string, string), string>();
            }
        }
    }
}
