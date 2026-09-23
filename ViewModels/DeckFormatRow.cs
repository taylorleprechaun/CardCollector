namespace CardCollector.ViewModels
{
    /// <summary>The events and match record for one deck name within one format.</summary>
    /// <param name="FormatStartDate">Null for events outside every format.</param>
    public sealed record DeckFormatRow(string DeckName, string FormatName, DateOnly? FormatStartDate, int EventCount, WinLossTie Record);
}
