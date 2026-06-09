using CardCollector.Data.Models;
using System.Text.Json;

namespace CardCollector.ViewModels
{
    public sealed class CollectionStatsViewModel
    {
        public IReadOnlyList<(string Label, int Count)> AcquisitionBreakdown { get; set; } = [];
        public IReadOnlyList<(string Label, int Count)> RarityBreakdown { get; set; } = [];
        public IReadOnlyList<(string Label, int Count)> SetBreakdown { get; set; } = [];
        public string SetCountsJson => JsonSerializer.Serialize(SetBreakdown.Take(20).Select(x => x.Count));
        public string SetLabelsJson => JsonSerializer.Serialize(SetBreakdown.Take(20).Select(x => x.Label));
        public IReadOnlyList<(string Label, decimal Value)> SetValueBreakdown { get; set; } = [];
        public string SetValueDataJson => JsonSerializer.Serialize(SetValueBreakdown.Select(x => x.Value));
        public string SetValueLabelsJson => JsonSerializer.Serialize(SetValueBreakdown.Select(x => x.Label));
        public IReadOnlyList<(string CardName, string SetName, string RarityName, decimal Value)> TopValueCards { get; set; } = [];
        public IReadOnlyList<CollectionValueSnapshot> ValueHistory { get; set; } = [];
        public string ValueHistoryCardCountsJson => JsonSerializer.Serialize(ValueHistory.Select(x => x.CardCount));
        public string ValueHistoryDatesJson => JsonSerializer.Serialize(ValueHistory.Select(x => x.SnapshotDate));
        public string ValueHistoryValuesJson => JsonSerializer.Serialize(ValueHistory.Select(x => x.TotalValue));
    }
}
