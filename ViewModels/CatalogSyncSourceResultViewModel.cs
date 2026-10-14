using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>How one source fared in a sync run: its status (null when the run didn't read it) and how many records it returned.</summary>
    public sealed record CatalogSyncSourceResultViewModel(string Label, CatalogSourceStatus? Status, int Count);
}
