using CardCollector.Data.Models.Catalog;

namespace CardCollector.ViewModels
{
    /// <summary>An outside identifier recorded against a catalog card.</summary>
    public sealed record CatalogCardAliasViewModel(CatalogSource Source, CatalogAliasKind Kind, int Value, DateTime FirstSeenUTC, DateTime LastSeenUTC);
}
