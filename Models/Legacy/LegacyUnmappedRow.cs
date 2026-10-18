namespace CardCollector.Models.Legacy
{
    /// <summary>A legacy row the migration could not map, as listed in the migration report.</summary>
    public sealed record LegacyUnmappedRow(string Table, int ID, string Description, string Reason);
}
