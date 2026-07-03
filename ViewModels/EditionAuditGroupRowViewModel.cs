namespace CardCollector.ViewModels
{
    public sealed class EditionAuditGroupRowViewModel
    {
        public required string FilterParams { get; init; }

        public required EditionAuditGroupViewModel Group { get; init; }
    }
}
