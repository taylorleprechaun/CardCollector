namespace CardCollector.Data.Models.Catalog
{
    /// <summary>Names of the <see cref="CatalogCard"/> fields a user can lock against sync changes.</summary>
    public static class CatalogCardField
    {
        public const string ARCHETYPE = nameof(CatalogCard.Archetype);
        public const string ATK = nameof(CatalogCard.ATK);
        public const string ATTRIBUTE = nameof(CatalogCard.Attribute);
        public const string CARD_TYPE = nameof(CatalogCard.CardType);
        public const string DEF = nameof(CatalogCard.DEF);
        public const string DESCRIPTION = nameof(CatalogCard.Description);
        public const string LEVEL = nameof(CatalogCard.Level);
        public const string LINK_MARKERS = nameof(CatalogCard.LinkMarkers);
        public const string LINK_RATING = nameof(CatalogCard.LinkRating);
        public const string NAME = nameof(CatalogCard.Name);
        public const string PASSCODE = nameof(CatalogCard.Passcode);
        public const string TYPE = nameof(CatalogCard.Type);
        public const string TYPE_LINE = nameof(CatalogCard.TypeLine);
    }
}
