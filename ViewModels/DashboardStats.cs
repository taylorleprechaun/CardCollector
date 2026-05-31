namespace CardCollector.ViewModels
{
    public sealed class DashboardStats
    {
        public int CompletedCount { get; set; }

        public decimal? CurrentMarketValue { get; set; }

        public string? CurrentMarketValueDate { get; set; }

        public int IncompleteSetCount { get; set; }

        public int OrderedCount { get; set; }

        public double PercentCompleted => TotalArtworks == 0 ? 0 : (double)CompletedCount / TotalArtworks * 100;

        public int PlaceholderSetCount { get; set; }

        public int RemainingCount => TotalArtworks - CompletedCount - OrderedCount;

        public int TotalArtworks { get; set; }

        public int TotalCardQuantity { get; set; }

        public decimal? TotalSpent { get; set; }

        public int WishlistCount { get; set; }
    }
}
