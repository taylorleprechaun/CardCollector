namespace CardCollector.Data.Models
{
    public sealed class WishlistValueSnapshot : IValueSnapshotEntity
    {
        public DateTime DateCreated { get; set; }
        public int ID { get; set; }
        public int RemainingCount { get; set; }
        int IValueSnapshotEntity.Count
        {
            get => RemainingCount;
            set => RemainingCount = value;
        }

        public string SnapshotDate { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }
}
