using CardCollector.Data.Models;

namespace CardCollector.Data
{
    /// <summary>The rows one save must reconcile, split by which side of the printing key is authoritative.</summary>
    internal sealed class PrintingKeySyncPlan
    {
        /// <summary>Rows with no printing ID, or whose columns changed; their ID is looked up from the columns.</summary>
        public IList<IPrintingKeyedRow> FromColumns { get; } = new List<IPrintingKeyedRow>();

        /// <summary>Rows whose printing ID was set or changed; their columns are copied from the printing.</summary>
        public IList<IPrintingKeyedRow> FromID { get; } = new List<IPrintingKeyedRow>();

        public bool IsEmpty => FromID.Count == 0 && FromColumns.Count == 0;
    }
}
