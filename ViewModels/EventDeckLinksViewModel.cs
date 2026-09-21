using CardCollector.Data.Models;

namespace CardCollector.ViewModels
{
    /// <summary>What the deck buttons on an event need: the event, and how many other events share its decklist URL and have no deck.</summary>
    public sealed record EventDeckLinksViewModel(Event Event, int OtherUnlinkedEventsWithSameURL);
}
