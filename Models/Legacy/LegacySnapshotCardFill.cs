namespace CardCollector.Models.Legacy
{
    /// <summary>How the migration linked copied value snapshots to catalog cards.</summary>
    /// <param name="ByEntry">Snapshots linked through their collection entry's printing.</param>
    /// <param name="ByName">Snapshots whose entry is gone, linked to the only active card with the snapshot's card name.</param>
    /// <param name="Unlinked">Snapshots no card could be found for; they drop out of the Stats price history.</param>
    public sealed record LegacySnapshotCardFill(int ByEntry, int ByName, int Unlinked);
}
