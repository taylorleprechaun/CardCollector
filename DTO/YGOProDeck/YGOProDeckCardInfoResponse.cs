using Newtonsoft.Json;

namespace CardCollector.DTO.YGOProDeck
{
    /// <summary>The envelope of YGOProDeck's <c>cardinfo.php</c> response.</summary>
    public sealed class YGOProDeckCardInfoResponse
    {
        [JsonProperty("data")]
        public IReadOnlyList<YGOProDeckCard>? Data { get; set; }
    }
}
