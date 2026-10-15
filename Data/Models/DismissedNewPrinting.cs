using CardCollector.Data.Models.Catalog;

namespace CardCollector.Data.Models
{
    public sealed class DismissedNewPrinting
    {
        /// <summary>Lets queries reach the printing (and its card); not loaded unless a query includes it.</summary>
        public CatalogPrinting? CatalogPrinting { get; set; }

        /// <summary>The catalog printing this row refers to.</summary>
        public int CatalogPrintingID { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public int ID { get; set; }
    }
}
