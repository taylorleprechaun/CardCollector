namespace CardCollector.Data.Models
{
    /// <summary>A metagame period: a named date range with its top strategies.</summary>
    public class Format
    {
        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        /// <summary>Null means the format is ongoing.</summary>
        public DateOnly? EndDate { get; set; }

        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public DateOnly StartDate { get; set; }

        public IReadOnlyList<FormatStrategy> Strategies { get; set; } = new List<FormatStrategy>();
    }
}
