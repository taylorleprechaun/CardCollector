using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class PurchaseLineInput
    {
        public CardCondition? Condition { get; set; }

        public CardEdition? Edition { get; set; }

        public decimal? MarketPriceAtEntry { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchasePrice { get; set; }

        public int Quantity { get; set; } = 1;
    }
}
