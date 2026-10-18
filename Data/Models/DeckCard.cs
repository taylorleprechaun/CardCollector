namespace CardCollector.Data.Models
{
    /// <summary>One distinct card in one section of a <see cref="Deck"/>, with how many copies it holds.</summary>
    public sealed class DeckCard
    {
        /// <summary>
        /// The catalog card the passcode resolved to on import. Null when the card data didn't know the passcode; such a row is
        /// resolved again from <see cref="PastedPasscode"/> each time the deck is read.
        /// </summary>
        public int? CatalogCardID { get; set; }

        public int DeckID { get; set; }

        public int ID { get; set; }

        /// <summary>The passcode as pasted (the first one, when several passcodes of one card were collapsed into this row).</summary>
        public int PastedPasscode { get; set; }

        public int Quantity { get; set; }

        public DeckSection Section { get; set; }

        /// <summary>Where the card first appeared in the pasted list. Kept as pasted; the viewer sorts by type and name instead.</summary>
        public int SortOrder { get; set; }
    }
}
