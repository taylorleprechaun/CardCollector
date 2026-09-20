using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>
    /// Posted values of the Add/Edit Event form. Everything is nullable so MVC's implicit "required"
    /// handling for non-nullable reference types never fires; EventRules owns validation.
    /// </summary>
    public sealed class EventInputModel
    {
        public DateOnly? Date { get; set; }

        public string? DecklistURL { get; set; }

        public string? DeckName { get; set; }

        public EventType? EventType { get; set; }

        public int? Finish { get; set; }

        public string? FinishNote { get; set; }

        /// <summary>Zero means a new event.</summary>
        public int ID { get; set; }

        public string? Location { get; set; }

        public string? Notes { get; set; }

        public int? Players { get; set; }

        public string? TopCut { get; set; }
    }
}
