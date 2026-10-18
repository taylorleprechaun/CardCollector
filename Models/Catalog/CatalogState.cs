using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// The whole catalog in memory for one sync run: every card (with its aliases, images, and printings and their
    /// sources) and every set. The sync rules change these objects in place and add new ones to the collections.
    /// </summary>
    public sealed class CatalogState
    {
        public ICollection<CatalogCard> Cards { get; init; } = new List<CatalogCard>();

        public bool IsEmpty => Cards.Count == 0;

        public ICollection<CatalogSet> Sets { get; init; } = new List<CatalogSet>();
    }
}
