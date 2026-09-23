using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CardCollector.Services
{
    /// <summary>Pure parsing of yaml-yugi-limit-regulation data: one banlist file and the GitHub directory listing that indexes them.</summary>
    public static partial class BanlistParser
    {
        /// <summary>Parses one list payload. Null if the JSON is malformed or has no parseable date; a bad regulation entry is skipped and logged.</summary>
        public static Banlist? ParseList(string json, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            BanlistFile? file;
            try
            {
                file = JsonConvert.DeserializeObject<BanlistFile>(json);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse banlist JSON");
                return null;
            }

            if (file?.Date is not { } dateText ||
                !DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return null;

            var limits = new Dictionary<int, BanlistLimit>();
            foreach (var (key, value) in file.Regulation ?? [])
            {
                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var konamiID))
                {
                    logger?.LogWarning("Skipping non-numeric Konami ID {Key} in banlist dated {Date}", key, dateText);
                    continue;
                }

                if (value is < 0 or > 2)
                {
                    logger?.LogWarning(
                        "Skipping out-of-range regulation value {Value} for Konami ID {KonamiID} in banlist dated {Date}",
                        value, konamiID, dateText);
                    continue;
                }

                limits[konamiID] = (BanlistLimit)value;
            }

            return new Banlist { EffectiveDate = date, LimitsByKonamiID = limits };
        }

        /// <summary>Filters file names down to dated TCG lists ("YYYY-MM-DD.vector.json"), ignoring "current", "options" and ".raw" siblings.</summary>
        public static IReadOnlyList<string> FilterDatedListNames(IEnumerable<string> fileNames)
        {
            if (fileNames is null) throw new ArgumentNullException(nameof(fileNames));

            return fileNames.Where(name => DatedListNameRegex().IsMatch(name)).ToList();
        }

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}\.vector\.json$")]
        private static partial Regex DatedListNameRegex();

        private sealed class BanlistFile
        {
            [JsonProperty("date")]
            public string? Date { get; set; }

            [JsonProperty("regulation")]
            public Dictionary<string, int>? Regulation { get; set; }
        }
    }
}
