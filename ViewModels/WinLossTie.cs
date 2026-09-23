namespace CardCollector.ViewModels
{
    /// <summary>A win-loss-tie tally, displayed as <c>W-L-T</c>.</summary>
    public sealed record WinLossTie(int Wins, int Losses, int Ties)
    {
        public int Total => Wins + Losses + Ties;

        /// <summary>A tie counts as half a win: <c>(W + T/2) / (W + L + T)</c>. Null when there is nothing to tally.</summary>
        public double? WinRate => Total == 0 ? null : (Wins + (Ties / 2.0)) / Total;

        public override string ToString() => $"{Wins}-{Losses}-{Ties}";
    }
}
