namespace CardCollector.Data.Models
{
    public sealed class CollectionEntryValueSnapshot
    {
        public string CardName { get; set; } = string.Empty;
        public int CollectionEntryID { get; set; }
        public DateTime DateCreated { get; set; }
        public int ID { get; set; }
        public decimal MarketValue { get; set; }
        public string RarityName { get; set; } = string.Empty;
        public string SetCode { get; set; } = string.Empty;
        public string SetName { get; set; } = string.Empty;
        public string SnapshotDate { get; set; } = string.Empty;
    }
}
