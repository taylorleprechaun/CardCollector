namespace CardCollector.Data.Models.Catalog
{
    /// <summary>One artwork of a catalog card, taken from the YGOProDeck card matched by Konami ID rather than by passcode.</summary>
    public sealed class CatalogCardImage
    {
        public int CatalogCardID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }

        /// <summary>YGOProDeck's id for the artwork; the image URLs are built from it.</summary>
        public int ImageID { get; set; }

        public string? ImageURL { get; set; }

        public string? ImageURLSmall { get; set; }

        /// <summary>0 for the card's primary artwork.</summary>
        public int SortOrder { get; set; }
    }
}
