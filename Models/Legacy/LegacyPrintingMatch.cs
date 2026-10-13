namespace CardCollector.Models.Legacy
{
    /// <summary>The catalog printing a legacy row maps to, or why it maps to none.</summary>
    public sealed record LegacyPrintingMatch(LegacyPrintingTarget? Target, LegacyMappingRule Rule, string? Reason)
    {
        public static LegacyPrintingMatch Unmapped(string reason) => new(null, LegacyMappingRule.Unmapped, reason);
    }
}
