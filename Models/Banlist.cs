namespace CardCollector.Models
{
    /// <summary>One TCG Forbidden &amp; Limited list, keyed by Konami ID. As parsed and cached, <see cref="EffectiveDate"/>
    /// is the source's date; the lists <see cref="BanlistCollection"/> serves carry the corrected AMER date.</summary>
    public sealed class Banlist
    {
        public DateOnly EffectiveDate { get; init; }

        public IReadOnlyDictionary<int, BanlistLimit> LimitsByKonamiID { get; init; } = new Dictionary<int, BanlistLimit>();

        /// <summary>A card's restriction by Konami ID. Absent ⇒ Unlimited.</summary>
        public BanlistLimit GetLimit(int konamiID) =>
            LimitsByKonamiID.GetValueOrDefault(konamiID, BanlistLimit.Unlimited);
    }
}
