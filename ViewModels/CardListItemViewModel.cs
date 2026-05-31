using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    public sealed class CardListItemViewModel
    {
        public string Attribute { get; set; } = string.Empty;
        public int CardID { get; set; }
        public string CardType { get; set; } = string.Empty;
        public int ImageID { get; set; }
        public string ImageURLSmall { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
        public string Name { get; set; } = string.Empty;
        public CollectionStatus? Status { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
