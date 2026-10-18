using Newtonsoft.Json;

namespace CardCollector.DTO.YGOProDeck
{
    /// <summary>The subset of YGOProDeck's <c>misc_info</c> block the app reads.</summary>
    public sealed class YGOProDeckMiscInfo
    {
        [JsonProperty("beta_id")]
        public int? BetaID { get; set; }

        [JsonProperty("konami_id")]
        public int? KonamiID { get; set; }
    }
}
