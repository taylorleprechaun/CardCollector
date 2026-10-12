using Newtonsoft.Json;

namespace CardCollector.DTO.YGOProDeck
{
    /// <summary>
    /// A card from YGOProDeck's <c>cardinfo.php?misc=yes</c> response: the app's <see cref="Card"/> fields plus the
    /// identifiers that link it to other sources without relying on its passcode.
    /// </summary>
    public sealed class YGOProDeckCard : Card
    {
        /// <summary>The temporary id YGOProDeck used before the card had a real passcode, if it ever did.</summary>
        [JsonIgnore]
        public int? BetaID => MiscInfo?.FirstOrDefault()?.BetaID;

        [JsonProperty("misc_info")]
        public IReadOnlyList<YGOProDeckMiscInfo>? MiscInfo { get; set; }
    }
}
