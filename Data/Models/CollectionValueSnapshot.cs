namespace CardCollector.Data.Models
{
    public sealed class CollectionValueSnapshot : IValueSnapshotEntity
    {
        public int CardCount { get; set; }
        int IValueSnapshotEntity.Count
        {
            get => CardCount;
            set => CardCount = value;
        }

        public DateTime DateCreated { get; set; }
        public int ID { get; set; }
        public string SnapshotDate { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }
}
