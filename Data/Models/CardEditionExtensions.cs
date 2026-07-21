namespace CardCollector.Data.Models
{
    public static class CardEditionExtensions
    {
        // The YGOProDeck API's set_edition values ("Limited", "1st Edition", "Unlimited") don't
        // match the enum's [Display] names (e.g. "Limited Edition"), so they need their own mapping.
        public static string GetTCGAPIEditionName(this CardEdition edition) => edition switch
        {
            CardEdition.FirstEdition => "1st Edition",
            CardEdition.LimitedEdition => "Limited",
            CardEdition.Unlimited => "Unlimited",
            _ => edition.ToString()
        };

        public static bool TryParseTCGAPIEditionName(string? apiEditionName, out CardEdition edition)
        {
            switch (apiEditionName)
            {
                case "1st Edition":
                    edition = CardEdition.FirstEdition;
                    return true;
                case "Limited":
                    edition = CardEdition.LimitedEdition;
                    return true;
                case "Unlimited":
                    edition = CardEdition.Unlimited;
                    return true;
                default:
                    edition = default;
                    return false;
            }
        }
    }
}
