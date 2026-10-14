using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;
using CardCollector.DTO.YGOProDeck;

namespace CardCollector.Models.Catalog
{
    /// <summary>What each source returned for one catalog sync.</summary>
    /// <param name="YamlYugi">yaml-yugi's card documents.</param>
    /// <param name="YGOProDeck">YGOProDeck's cards.</param>
    /// <param name="TCGPlayer">TCGPlayer's product rows, read through tcgcsv.</param>
    public sealed record CatalogSourceData(IReadOnlyList<YamlCard> YamlYugi, IReadOnlyList<YGOProDeckCard> YGOProDeck, IReadOnlyList<TCGPriceSet> TCGPlayer);
}
