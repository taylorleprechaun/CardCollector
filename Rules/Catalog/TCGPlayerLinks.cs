using System.Globalization;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Links to TCGPlayer's own pages for the products the catalog tracks.</summary>
    public static class TCGPlayerLinks
    {
        private const string PRODUCT_URL_PREFIX = "https://www.tcgplayer.com/product/";

        public static string ProductURL(int productID) => PRODUCT_URL_PREFIX + productID.ToString(CultureInfo.InvariantCulture);

        /// <summary>The product page for a TCGPlayer source's external key; null when the key isn't a product ID.</summary>
        public static string? ProductURL(string? externalKey) =>
            int.TryParse(externalKey, NumberStyles.None, CultureInfo.InvariantCulture, out var productID) ? ProductURL(productID) : null;
    }
}
