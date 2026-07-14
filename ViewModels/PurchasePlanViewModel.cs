namespace CardCollector.ViewModels
{
    public sealed class PurchasePlanViewModel
    {
        public IReadOnlyList<PurchasePriorityCandidateViewModel> Items { get; init; } = [];

        public decimal TotalCost { get; init; }
    }
}
