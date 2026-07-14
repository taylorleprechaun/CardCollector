namespace CardCollector.Services
{
    /// <summary>
    /// A preferred printing flagged by <see cref="PurchasePriorityAnalyzer"/> as at risk of a price run before its next reprint.
    /// </summary>
    public sealed class PurchasePriorityCandidate
    {
        public int CardID { get; init; }

        public string CardName { get; init; } = string.Empty;

        public string DebutDate { get; init; } = string.Empty;

        public int FoilCount { get; init; }

        public string PrintingDate { get; init; } = string.Empty;
    }
}
