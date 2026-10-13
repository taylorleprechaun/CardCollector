using Newtonsoft.Json;

namespace CardCollector.DTO
{
    /// <summary>
    /// The app's card shape, deserialized from both yaml-yugi (after mapping) and YGOProDeck. Not sealed so
    /// <see cref="YGOProDeck.YGOProDeckCard"/> can add the YGOProDeck-only fields to the same parse.
    /// </summary>
    public class Card
    {
        [JsonProperty("archetype")]
        public string? Archetype { get; set; }

        [JsonProperty("atk")]
        public int? ATK { get; set; }

        [JsonProperty("attribute")]
        public string? Attribute { get; set; }

        [JsonProperty("card_images")]
        public IReadOnlyList<Image>? CardImages { get; set; }

        [JsonProperty("card_sets")]
        public IReadOnlyList<Set>? CardSets { get; set; }

        [JsonProperty("type")]
        public string? CardType { get; set; }

        [JsonProperty("def")]
        public int? DEF { get; set; }

        [JsonProperty("desc")]
        public string? Description { get; set; }

        /// <summary>The catalog card id: permanent, and unrelated to any passcode.</summary>
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("konami_id")]
        public int? KonamiID { get; set; }

        [JsonProperty("level")]
        public int? Level { get; set; }

        [JsonProperty("linkmarkers")]
        public IReadOnlyList<string>? LinkMarkers { get; set; }

        [JsonProperty("linkval")]
        public int? LinkRating { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>The passcode to show for the card. <see cref="ID"/> is the catalog card id, which is not a passcode.</summary>
        [JsonIgnore]
        public int? Passcode { get; set; }

        [JsonProperty("race")]
        public string? Type { get; set; }

        [JsonProperty("typeline")]
        public IReadOnlyList<string>? TypeLine { get; set; }
    }
}
