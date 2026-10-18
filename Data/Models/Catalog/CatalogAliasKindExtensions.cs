namespace CardCollector.Data.Models.Catalog
{
    public static class CatalogAliasKindExtensions
    {
        /// <summary>
        /// Lower wins when one value is an alias of more than one card: a yaml-yugi passcode is the strongest claim and an
        /// alternate-artwork id the weakest.
        /// </summary>
        public static int ResolutionPriority(this CatalogAliasKind kind) => kind switch
        {
            CatalogAliasKind.Passcode => 0,
            CatalogAliasKind.FakePassword => 1,
            CatalogAliasKind.YGOProDeckID => 2,
            CatalogAliasKind.BetaID => 3,
            _ => 4
        };
    }
}
