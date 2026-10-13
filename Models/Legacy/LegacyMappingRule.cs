namespace CardCollector.Models.Legacy
{
    /// <summary>How a legacy row found its catalog printing or card.</summary>
    public enum LegacyMappingRule
    {
        /// <summary>
        /// The row's card id pointed at the wrong card (or no card), but only one card has a printing with the row's set
        /// code, rarity, and variant, so the row moved to that card.
        /// </summary>
        CardCorrected,

        /// <summary>The row's card id is a known alias of the card that owns a printing with the row's set code, rarity, and variant.</summary>
        Exact,

        /// <summary>No rule found a single printing; the row waits in the review inbox.</summary>
        Unmapped,

        /// <summary>The row had no variant and its card has exactly one printing with that set code and rarity (a variant one).</summary>
        VariantFilled
    }
}
