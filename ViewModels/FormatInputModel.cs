namespace CardCollector.ViewModels
{
    /// <summary>
    /// Posted values of the Add/Edit Format form. Everything is nullable so MVC's implicit "required"
    /// handling for non-nullable reference types never fires; FormatRules owns validation.
    /// </summary>
    public sealed class FormatInputModel
    {
        public DateOnly? EndDate { get; set; }

        /// <summary>Zero means a new format.</summary>
        public int ID { get; set; }

        public bool IsOngoing { get; set; }

        public string? Name { get; set; }

        public string? Notes { get; set; }

        public DateOnly? StartDate { get; set; }

        public IReadOnlyList<string?>? Strategies { get; set; }
    }
}
