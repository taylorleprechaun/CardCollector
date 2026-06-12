using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class AdvancedFiltersViewModel
    {
        public static readonly IReadOnlyList<string> CardTypes = ["Monster", "Spell", "Trap"];

        public IReadOnlyList<AcquisitionMethod> AvailableAcquisitionMethods { get; init; } = [];
        public IReadOnlyList<CardCondition> AvailableConditions { get; init; } = [];
        public IReadOnlyList<CardEdition> AvailableEditions { get; init; } = [];
        public IReadOnlyList<string> AvailableRarityNames { get; init; } = [];
        public IReadOnlyList<string> AvailableSetNames { get; init; } = [];

        public string? CurrentAcquisitionMethod { get; init; }
        public string? CurrentCardType { get; init; }
        public string? CurrentCheckedOutFilter { get; init; }
        public string? CurrentCollectionFilter { get; init; }
        public string? CurrentCondition { get; init; }
        public string? CurrentEdition { get; init; }
        public string? CurrentOrderedFilter { get; init; }
        public string? CurrentRarityName { get; init; }
        public string? CurrentSetName { get; init; }
        public string? CurrentWishlistFilter { get; init; }

        public bool ShowAcquisitionMethod { get; init; }
        public bool ShowCheckedOutFilter { get; init; }
        public bool ShowCollectionFilters { get; init; }
        public bool ShowCondition { get; init; }
        public bool ShowEdition { get; init; }
    }
}
