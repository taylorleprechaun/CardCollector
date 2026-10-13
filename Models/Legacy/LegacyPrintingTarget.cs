namespace CardCollector.Models.Legacy
{
    /// <summary>A catalog printing as the legacy row mapper compares it.</summary>
    public sealed record LegacyPrintingTarget(int PrintingID, int CardID, string SetCode, string RarityName, string? PrintVariant);
}
