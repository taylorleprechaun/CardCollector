using CardCollector.Data.Models;
using System.Text.Json;

namespace CardCollector.ViewModels
{
    public sealed class CollectionStatsViewModel
    {
        public IReadOnlyList<(string Label, int Count)> AcquisitionBreakdown { get; set; } = [];
        public string AcquisitionCountsJson => JsonSerializer.Serialize(AcquisitionBreakdown.Select(x => x.Count));
        public string AcquisitionLabelsJson => JsonSerializer.Serialize(AcquisitionBreakdown.Select(x => x.Label));
        public IReadOnlyList<(string Label, int Count)> RarityBreakdown { get; set; } = [];
        public string RarityCountsJson => JsonSerializer.Serialize(RarityBreakdown.Select(x => x.Count));
        public string RarityLabelsJson => JsonSerializer.Serialize(RarityBreakdown.Select(x => x.Label));
        public IReadOnlyList<(string Label, int Count)> SetBreakdown { get; set; } = [];
        public string SetCountsJson => JsonSerializer.Serialize(SetBreakdown.Take(20).Select(x => x.Count));
        public string SetLabelsJson => JsonSerializer.Serialize(SetBreakdown.Take(20).Select(x => x.Label));
        public IReadOnlyList<(string Label, decimal Value)> SetValueBreakdown { get; set; } = [];
        public string SetValueDataJson => JsonSerializer.Serialize(SetValueBreakdown.Select(x => x.Value));
        public string SetValueLabelsJson => JsonSerializer.Serialize(SetValueBreakdown.Select(x => x.Label));
        public IReadOnlyList<CollectionValueSnapshot> ValueHistory { get; set; } = [];
        public string ValueHistoryDatesJson => JsonSerializer.Serialize(ValueHistory.Select(x => x.SnapshotDate));
        public string ValueHistoryValuesJson => JsonSerializer.Serialize(ValueHistory.Select(x => x.TotalValue));
    }
}
