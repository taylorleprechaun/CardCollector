namespace CardCollector.ViewModels
{
    public class DashboardStats
    {
        public int IncompleteSetCount { get; set; }

        public decimal? MarketValueAtEntry { get; set; }

        public int OrderedCount { get; set; }

        public int OwnedCount { get; set; }

        public double PercentOwned => TotalArtworks == 0 ? 0 : (double)OwnedCount / TotalArtworks * 100;

        public int PlaceholderSetCount { get; set; }

        public int RemainingCount => TotalArtworks - OwnedCount - OrderedCount;

        public int TotalArtworks { get; set; }

        public int TotalCardQuantity { get; set; }

        public decimal? TotalSpent { get; set; }

        public int WishlistCount { get; set; }
    }
}
