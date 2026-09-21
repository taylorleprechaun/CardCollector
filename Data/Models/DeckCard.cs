namespace CardCollector.Data.Models
{
    /// <summary>One distinct card in one section of a <see cref="Deck"/>, with how many copies it holds.</summary>
    public class DeckCard
    {
        /// <summary>The app's <c>Card.ID</c>. A passcode the card data doesn't know is stored as it was pasted.</summary>
        public int CardID { get; set; }

        public int DeckID { get; set; }

        public int ID { get; set; }

        public int Quantity { get; set; }

        public DeckSection Section { get; set; }

        /// <summary>Where the card first appeared in its section, so the order the deck was arranged in survives.</summary>
        public int SortOrder { get; set; }
    }
}
