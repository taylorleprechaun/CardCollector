namespace CardCollector.Data.Models
{
    /// <summary>
    /// Common shape for a day-level aggregate value snapshot (e.g. collection market value,
    /// wishlist cost-to-complete), enabling shared persistence logic across snapshot tables.
    /// </summary>
    public interface IValueSnapshotEntity
    {
        int Count { get; set; }

        DateTime DateCreated { get; set; }

        int ID { get; set; }

        string SnapshotDate { get; set; }

        decimal TotalValue { get; set; }
    }
}
