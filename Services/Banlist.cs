namespace CardCollector.Services
{
    /// <summary>One TCG Forbidden &amp; Limited list, keyed by Konami ID. <see cref="EffectiveDate"/> is the source
    /// date before any override — see <see cref="BanlistCollection"/>.</summary>
    public sealed class Banlist
    {
        public DateOnly EffectiveDate { get; set; }

        public IReadOnlyDictionary<int, BanlistLimit> LimitsByKonamiID { get; set; } = new Dictionary<int, BanlistLimit>();

        /// <summary>A card's restriction by Konami ID. Absent ⇒ Unlimited.</summary>
        public BanlistLimit GetLimit(int konamiID) =>
            LimitsByKonamiID.GetValueOrDefault(konamiID, BanlistLimit.Unlimited);
    }
}
