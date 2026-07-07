namespace CardCollector.ViewModels
{
    public record StatTileViewModel(string Label, string Value, StatTileStatus Status = StatTileStatus.Neutral, string? AccentColor = null);
}
