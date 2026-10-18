namespace CardCollector.ViewModels
{
    /// <summary>The hand-editable fields of a catalog card, as the admin form posts them.</summary>
    public sealed class CatalogCardEditInput
    {
        public string? Archetype { get; set; }

        public int? ATK { get; set; }

        public string? Attribute { get; set; }

        public string? CardType { get; set; }

        public int? DEF { get; set; }

        public string? Description { get; set; }

        public int? Level { get; set; }

        /// <summary>Comma-separated link arrow names (e.g. "Left,Bottom-Right").</summary>
        public string? LinkMarkers { get; set; }

        public int? LinkRating { get; set; }

        public string? Name { get; set; }

        public int? Passcode { get; set; }

        /// <summary>The monster type or spell/trap property.</summary>
        public string? Type { get; set; }

        /// <summary>Comma-separated type line (e.g. "Dragon,Synchro,Effect").</summary>
        public string? TypeLine { get; set; }
    }
}
