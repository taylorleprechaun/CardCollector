namespace CardCollector.ViewModels
{
    public sealed class DashboardStats
    {
        public int CompletedCount { get; set; }

        public decimal? CurrentMarketValue { get; set; }

        public string? CurrentMarketValueDate { get; set; }

        public int IncompleteSetCount { get; set; }

        public int OrderedCount { get; set; }

        public double PercentCompleted => TotalCards == 0 ? 0 : (double)CompletedCount / TotalCards * 100;

        public int PlaceholderSetCount { get; set; }

        public int RemainingCount => TotalCards - CompletedCount - OrderedCount;

        public int TotalCards { get; set; }

        public int TotalCardQuantity { get; set; }

        public decimal? TotalSpent { get; set; }

        public int WishlistCount { get; set; }
    }
}
