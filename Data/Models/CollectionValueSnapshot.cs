namespace CardCollector.Data.Models
{
    public class CollectionValueSnapshot
    {
        public int ID { get; set; }
        public int CardCount { get; set; }
        public DateTime DateCreated { get; set; }
        public string SnapshotDate { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }
}
