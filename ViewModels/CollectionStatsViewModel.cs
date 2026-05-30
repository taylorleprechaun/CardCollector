using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public class CollectionStatsViewModel
    {
        public List<(string Label, int Count)> AcquisitionBreakdown { get; set; } = [];
        public List<(string Label, int Count)> RarityBreakdown { get; set; } = [];
        public List<(string Label, int Count)> SetBreakdown { get; set; } = [];
        public List<(string Label, decimal Value)> SetValueBreakdown { get; set; } = [];
        public List<CollectionValueSnapshot> ValueHistory { get; set; } = [];
    }
}
