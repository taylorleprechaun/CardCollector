namespace CardCollector.ViewModels
{
    public class WishlistItemViewModel
    {
        public int CardID { get; set; }

        public string CardName { get; set; } = string.Empty;

        public int ImageID { get; set; }

        public string ImageURLSmall { get; set; } = string.Empty;

        public IReadOnlyList<string> AvailableRarities { get; init; } = [];

        public decimal? Price { get; set; }

        public string RarityCode { get; set; } = string.Empty;

        public string RarityName { get; set; } = string.Empty;

        public string SetCode { get; set; } = string.Empty;

        public string SetName { get; set; } = string.Empty;
    }
}
