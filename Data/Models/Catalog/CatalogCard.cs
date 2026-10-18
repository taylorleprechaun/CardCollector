namespace CardCollector.Data.Models.Catalog
{
    /// <summary>
    /// A card in the app's own catalog. <see cref="ID"/> is permanent and never reused; outside identifiers (passcodes,
    /// YGOProDeck ids) are recorded as <see cref="Aliases"/> because the sources reassign them.
    /// </summary>
    public sealed class CatalogCard
    {
        public ICollection<CatalogCardAlias> Aliases { get; set; } = new List<CatalogCardAlias>();

        public string? Archetype { get; set; }

        public int? ATK { get; set; }

        public string? Attribute { get; set; }

        public string? CardType { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int? DEF { get; set; }

        public string? Description { get; set; }

        public DateTime FirstSeenUTC { get; set; }

        public int ID { get; set; }

        public ICollection<CatalogCardImage> Images { get; set; } = new List<CatalogCardImage>();

        /// <summary>Konami's database id. Shared by every source and never reassigned, so it is the first thing a sync matches on.</summary>
        public int? KonamiID { get; set; }

        public DateTime LastSeenUTC { get; set; }

        public int? Level { get; set; }

        /// <summary>Comma-separated link arrow names in YGOProDeck's wording (e.g. "Left,Bottom-Right").</summary>
        public string? LinkMarkers { get; set; }

        public int? LinkRating { get; set; }

        /// <summary>
        /// Comma-separated names of the fields edited by hand (see <see cref="CatalogCardField"/>). A sync never
        /// overwrites a locked field.
        /// </summary>
        public string? LockedFields { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>The passcode shown for the card. Set by the sync when the card is first seen; changed only by the user.</summary>
        public int? Passcode { get; set; }

        public ICollection<CatalogPrinting> Printings { get; set; } = new List<CatalogPrinting>();

        public CatalogStatus Status { get; set; }

        /// <summary>The monster type or spell/trap property (YGOProDeck's "race").</summary>
        public string? Type { get; set; }

        /// <summary>Comma-separated type line in YGOProDeck's wording (e.g. "Dragon,Synchro,Effect").</summary>
        public string? TypeLine { get; set; }

        public bool IsLocked(string fieldName) => CatalogLockedFields.Contains(LockedFields, fieldName);
    }
}
