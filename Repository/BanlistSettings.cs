namespace CardCollector.Repository
{
    /// <summary>Configuration for fetching and caching TCG Forbidden &amp; Limited list data from yaml-yugi-limit-regulation.</summary>
    public sealed class BanlistSettings
    {
        /// <summary>GitHub Pages base URL the dated and current list files are read from.</summary>
        public string BaseUrl { get; set; } = "https://dawnbrandbots.github.io/yaml-yugi-limit-regulation/";

        /// <summary>How long a downloaded banlist cache stays fresh before a warm-up or nightly refresh re-fetches it.</summary>
        public int CacheTtlDays { get; set; } = 7;

        /// <summary>Maps a list's published source date ("yyyy-MM-dd") to the AMER effective date to use instead, for the
        /// rare lists recorded under a regional date. An entry that doesn't parse or fall between its neighbours is ignored.
        /// The real entries live only in appsettings.json, so adding one there is the whole change.</summary>
        public IDictionary<string, string> EffectiveDateOverrides { get; set; } = new Dictionary<string, string>();

        /// <summary>GitHub contents-API URL listing the dated list files, used to discover which dates exist.</summary>
        public string IndexUrl { get; set; } = "https://api.github.com/repos/DawnbrandBots/yaml-yugi-limit-regulation/contents/data/tcg";
    }
}
