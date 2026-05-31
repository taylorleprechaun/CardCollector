namespace CardCollector.Data.Models
{
    public sealed class CollectionValueSnapshot
    {
        public int CardCount { get; set; }
        public DateTime DateCreated { get; set; }
        public int ID { get; set; }
        public string SnapshotDate { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }
}
