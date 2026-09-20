namespace CardCollector.ViewModels
{
    /// <summary>A win-loss-tie tally, displayed as <c>W-L-T</c>.</summary>
    public sealed record WinLossTie(int Wins, int Losses, int Ties)
    {
        public int Total => Wins + Losses + Ties;

        public override string ToString() => $"{Wins}-{Losses}-{Ties}";
    }
}
