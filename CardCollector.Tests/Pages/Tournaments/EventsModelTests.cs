using CardCollector.Data.Models;
using CardCollector.Pages.Tournaments;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace CardCollector.Tests.Pages.Tournaments
{
    [TestClass]
    public sealed class EventsModelTests
    {
        [TestMethod]
        public void GetFilterParams_ActiveFilters_ContainsOnlyPresentValues()
        {
            var (model, _, _) = CreateModel();
            model.DateFrom = new DateOnly(2024, 1, 5);
            model.Deck = " Sample ";
            model.Type = EventType.WinAMat;
            model.Location = "   ";

            var values = model.GetFilterParams();

            Assert.AreEqual(3, values.Count);
            Assert.AreEqual("2024-01-05", values["dateFrom"]);
            Assert.AreEqual("Sample", values["deck"]);
            Assert.AreEqual("WinAMat", values["type"]);
        }

        [TestMethod]
        public void GetReturnRouteData_ActiveFilters_AddsCurrentPage()
        {
            var (model, _, _) = CreateModel();
            model.FormatID = 4;
            model.PageNumber = 3;
            model.PageSize = 50;

            var values = model.GetReturnRouteData();

            Assert.AreEqual("4", values["formatID"]);
            Assert.AreEqual("3", values["pageNumber"]);
            Assert.AreEqual("50", values["pageSize"]);
        }

        [TestMethod]
        public void HasActiveFilters_AnyFilterSet_IsTrue()
        {
            var (model, _, _) = CreateModel();
            model.FormatID = 2;

            Assert.IsTrue(model.HasActiveFilters);
        }

        [TestMethod]
        public void HasActiveFilters_NoFilters_IsFalse()
        {
            var (model, _, _) = CreateModel();

            Assert.IsFalse(model.HasActiveFilters);
        }

        [TestMethod]
        public async Task OnGetAsync_DecksExist_ExposesThemAsDeckOptions()
        {
            var options = new[] { new DeckListItemViewModel { EventCount = 1, ExtraCount = 15, ID = 3, MainCount = 60, Name = "Sample Deck", SideCount = 15 } };
            var (model, _, _) = CreateModel(options);

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual("Sample Deck", model.DeckOptions.Single().Name);
        }
        [TestMethod]
        public async Task OnGetAsync_FiltersBound_PassesThemToTheService()
        {
            var (model, events, _) = CreateModel();
            model.DateFrom = new DateOnly(2024, 1, 1);
            model.DateTo = new DateOnly(2024, 12, 31);
            model.Deck = "Sample";
            model.FormatID = 7;
            model.Location = "Hobby";
            model.PageNumber = 2;
            model.PageSize = 50;
            model.Type = EventType.Regional;

            await model.OnGetAsync(CancellationToken.None);

            events.Verify(s => s.SearchAsync(
                It.Is<EventSearchCriteria>(c =>
                    c.DateFrom == new DateOnly(2024, 1, 1)
                    && c.DateTo == new DateOnly(2024, 12, 31)
                    && c.DeckName == "Sample"
                    && c.EventType == EventType.Regional
                    && c.FormatID == 7
                    && c.Location == "Hobby"
                    && c.Page == 2
                    && c.PageSize == 50),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task OnGetAsync_InvalidPaging_FallsBackToDefaults()
        {
            var (model, _, _) = CreateModel();
            model.PageNumber = -4;
            model.PageSize = 7;

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual(1, model.PageNumber);
            Assert.AreEqual(25, model.PageSize);
        }

        [TestMethod]
        public async Task OnGetAsync_NoFilters_LoadsFormatsSuggestionsAndResults()
        {
            var (model, events, formats) = CreateModel();
            events.Setup(s => s.SearchAsync(It.IsAny<EventSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildPage(1, BuildItem(1)));
            events.Setup(s => s.GetDeckNamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["Sample Deck"]);
            events.Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["Test Hobby Shop"]);
            formats.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new Format { ID = 1, Name = "Alpha Era" }]);

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual(1, model.Results.Items.Count);
            CollectionAssert.AreEqual(new[] { "Sample Deck" }, model.DeckNames.ToArray());
            CollectionAssert.AreEqual(new[] { "Test Hobby Shop" }, model.Locations.ToArray());
            Assert.AreEqual(1, model.Formats.Count);
        }

        [TestMethod]
        public async Task OnGetAsync_PageBeyondTheLastPage_LoadsTheLastPage()
        {
            var (model, events, _) = CreateModel();
            model.PageNumber = 3;
            events.SetupSequence(s => s.SearchAsync(It.IsAny<EventSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<EventListItemViewModel> { Page = 3, PageSize = 25, TotalCount = 30 })
                .ReturnsAsync(BuildPage(2, BuildItem(1)));

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual(2, model.PageNumber);
            Assert.AreEqual(1, model.Results.Items.Count);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_EventExists_SetsSuccessAndRedirectsKeepingFilters()
        {
            var (model, events, _) = CreateModel();
            events.Setup(s => s.DeleteAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            model.Deck = "Sample";
            model.PageNumber = 2;

            var result = await model.OnPostDeleteAsync(5, CancellationToken.None);

            var redirect = (RedirectToPageResult)result;
            Assert.AreEqual("Event deleted.", model.TempData["Success"]);
            Assert.AreEqual("Sample", redirect.RouteValues!["deck"]);
            Assert.AreEqual("2", redirect.RouteValues["pageNumber"]);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_EventMissing_SetsErrorAndRedirects()
        {
            var (model, events, _) = CreateModel();
            events.Setup(s => s.DeleteAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await model.OnPostDeleteAsync(5, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("That event no longer exists.", model.TempData["Error"]);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_EditingExistingEvent_CallsUpdate()
        {
            var (model, events, _) = CreateModel();
            events.Setup(s => s.UpdateAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>())).ReturnsAsync(EventSaveResult.Success());
            model.Input = BuildInput(id: 9);

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Event updated.", model.TempData["Success"]);
            events.Verify(s => s.UpdateAsync(It.Is<Event>(e => e.ID == 9), It.IsAny<CancellationToken>()), Times.Once);
            events.Verify(s => s.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_InvalidDateBinding_ReportsInvalidNotRequired()
        {
            var (model, events, _) = CreateModel();
            model.Input = BuildInput(hasDate: false);
            model.ModelState.AddModelError("Input.Date", "not a date");

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            CollectionAssert.AreEqual(new[] { "Date is not a valid date." }, model.Errors.ToArray());
            events.Verify(s => s.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_InvalidEventTypeBinding_ReportsInvalidNotRequired()
        {
            var (model, _, _) = CreateModel();
            model.Input = BuildInput(eventType: null);
            model.ModelState.AddModelError("Input.EventType", "bad value");

            await model.OnPostSaveAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "Event type is not valid." }, model.Errors.ToArray());
        }

        [TestMethod]
        public async Task OnPostSaveAsync_InvalidNumberBindings_ReportBothErrors()
        {
            var (model, _, _) = CreateModel();
            model.Input = BuildInput();
            model.ModelState.AddModelError("Input.Finish", "not a number");
            model.ModelState.AddModelError("Input.Players", "not a number");

            await model.OnPostSaveAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "Finish must be a whole number.", "Players must be a whole number." },
                model.Errors.ToArray());
        }
        [TestMethod]
        public async Task OnPostSaveAsync_MissingDateAndType_ReturnsRequiredErrorsWithoutSaving()
        {
            var (model, events, _) = CreateModel();
            model.Input = BuildInput(hasDate: false, eventType: null);

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            CollectionAssert.AreEqual(new[] { "Date is required.", "Event type is required." }, model.Errors.ToArray());
            events.Verify(s => s.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_NewEvent_CallsAddWithTheSubmittedValues()
        {
            var (model, events, _) = CreateModel();
            Event? saved = null;
            events.Setup(s => s.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
                .Callback<Event, CancellationToken>((e, _) => saved = e)
                .ReturnsAsync(EventSaveResult.Success());
            model.Input = BuildInput();
            model.Input.DecklistURL = "https://example.test/deck";
            model.Input.Finish = 2;
            model.Input.FinishNote = "note";
            model.Input.Notes = "line one\nline two";
            model.Input.Players = 16;
            model.Input.TopCut = "Top 8";

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Event added.", model.TempData["Success"]);
            Assert.AreEqual(new DateOnly(2024, 5, 4), saved!.Date);
            Assert.AreEqual("Sample Deck", saved.DeckName);
            Assert.AreEqual("https://example.test/deck", saved.DecklistURL);
            Assert.AreEqual(EventType.Regional, saved.EventType);
            Assert.AreEqual(2, saved.Finish);
            Assert.AreEqual("note", saved.FinishNote);
            Assert.AreEqual("Test Hobby Shop", saved.Location);
            Assert.AreEqual("line one\nline two", saved.Notes);
            Assert.AreEqual(16, saved.Players);
            Assert.AreEqual("Top 8", saved.TopCut);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_ServiceRejects_ReturnsPageWithErrorsAndReopensModal()
        {
            var (model, events, _) = CreateModel();
            events.Setup(s => s.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EventSaveResult.Failure(["Location is required."]));
            model.Input = BuildInput();

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            CollectionAssert.AreEqual(new[] { "Location is required." }, model.Errors.ToArray());
            Assert.IsTrue(model.ShowEventModal);
            Assert.AreEqual("Sample Deck", model.Input.DeckName);
        }

                private static EventInputModel BuildInput(
                    bool hasDate = true,
                    EventType? eventType = EventType.Regional,
                    int id = 0) =>
                    new()
                    {
                        Date = hasDate ? new DateOnly(2024, 5, 4) : null,
                        DeckName = "Sample Deck",
                        EventType = eventType,
                        ID = id,
                        Location = "Test Hobby Shop"
                    };

                private static EventListItemViewModel BuildItem(int id) =>
                    new()
            {
                Event = new Event { Date = new DateOnly(2024, 5, 4), DeckName = "Sample Deck", ID = id, Location = "Test Hobby Shop" },
                FormatName = "Alpha Era",
                Record = new WinLossTie(1, 0, 0)
            };
        private static PagedResult<EventListItemViewModel> BuildPage(int page, params EventListItemViewModel[] items) =>
            new() { Items = items, Page = page, PageSize = 25, TotalCount = items.Length };

        private static (EventsModel Model, Mock<IEventService> Events, Mock<IFormatService> Formats) CreateModel(IReadOnlyList<DeckListItemViewModel>? deckOptions = null)
        {
            var decks = new Mock<IDeckService>();
            decks.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deckOptions ?? []);

            var events = new Mock<IEventService>();
            events.Setup(s => s.SearchAsync(It.IsAny<EventSearchCriteria>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<EventListItemViewModel> { Page = 1, PageSize = 25 });
            events.Setup(s => s.GetDeckNamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            events.Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var formats = new Mock<IFormatService>();
            formats.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var model = new EventsModel(decks.Object, events.Object, formats.Object);
            PageContextFactory.Attach(model);
            return (model, events, formats);
        }
    }
}
