using CardCollector.Data.Models;
using CardCollector.Rules;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class EventRulesTests
    {
        [TestMethod]
        public void Normalize_BlankOptionalText_BecomesNull()
        {
            var normalized = EventRules.Normalize(BuildEvent(decklistURL: "   ", finishNote: "", notes: "  ", topCut: " "));

            Assert.IsNull(normalized.DecklistURL);
            Assert.IsNull(normalized.FinishNote);
            Assert.IsNull(normalized.Notes);
            Assert.IsNull(normalized.TopCut);
        }

        [TestMethod]
        public void Normalize_DeckLinkAndRounds_AreNotCopied()
        {
            var source = BuildEvent();
            source.DeckID = 7;
            source.Matches = [new Match()];

            var normalized = EventRules.Normalize(source);

            Assert.IsNull(normalized.DeckID);
            Assert.AreEqual(0, normalized.Matches.Count);
        }

        [TestMethod]
        public void Normalize_NullEvent_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => EventRules.Normalize(null!));
        }

        [TestMethod]
        public void Normalize_NullRequiredText_BecomesEmpty()
        {
            var source = BuildEvent();
            source.DeckName = null!;
            source.Location = null!;

            var normalized = EventRules.Normalize(source);

            Assert.AreEqual(string.Empty, normalized.DeckName);
            Assert.AreEqual(string.Empty, normalized.Location);
        }

        [TestMethod]
        public void Normalize_PaddedText_IsTrimmed()
        {
            var normalized = EventRules.Normalize(BuildEvent(location: "  Test Hobby Shop ", deckName: " Sample Deck ", notes: " note "));

            Assert.AreEqual("Test Hobby Shop", normalized.Location);
            Assert.AreEqual("Sample Deck", normalized.DeckName);
            Assert.AreEqual("note", normalized.Notes);
        }
        [TestMethod]
        [DataRow("http://example.test/deck", DisplayName = "http URL")]
        [DataRow("https://example.test/deck?id=1", DisplayName = "https URL")]
        [DataRow(null, DisplayName = "No URL")]
        public void Validate_AcceptableDecklistURL_ReturnsNoErrors(string? url)
        {
            var errors = EventRules.Validate(BuildEvent(decklistURL: url));

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_DecklistURLTooLong_ReturnsLengthError()
        {
            var url = "https://example.test/" + new string('a', EventRules.MAX_URL_LENGTH);

            var errors = EventRules.Validate(BuildEvent(decklistURL: url));

            CollectionAssert.Contains(errors.ToArray(), $"Decklist URL must be {EventRules.MAX_URL_LENGTH} characters or fewer.");
        }

        [TestMethod]
        public void Validate_DeckNameAtLimit_ReturnsNoErrors()
        {
            var errors = EventRules.Validate(BuildEvent(deckName: new string('d', EventRules.MAX_DECK_NAME_LENGTH)));

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_DeckNameTooLong_ReturnsError()
        {
            var errors = EventRules.Validate(BuildEvent(deckName: new string('d', EventRules.MAX_DECK_NAME_LENGTH + 1)));

            CollectionAssert.Contains(errors.ToArray(), $"Deck name must be {EventRules.MAX_DECK_NAME_LENGTH} characters or fewer.");
        }
        [TestMethod]
        public void Validate_DefaultDate_ReturnsDateRequired()
        {
            var tournamentEvent = BuildEvent();
            tournamentEvent.Date = default;

            var errors = EventRules.Validate(tournamentEvent);

            CollectionAssert.Contains(errors.ToArray(), "Date is required.");
        }

        [TestMethod]
        public void Validate_FinishAbovePlayers_ReturnsError()
        {
            var errors = EventRules.Validate(BuildEvent(finish: 17, players: 16));

            CollectionAssert.Contains(errors.ToArray(), "Finish can't be higher than the number of players.");
        }

        [TestMethod]
        [DataRow(0, DisplayName = "Zero finish")]
        [DataRow(-3, DisplayName = "Negative finish")]
        public void Validate_FinishBelowOne_ReturnsError(int finish)
        {
            var errors = EventRules.Validate(BuildEvent(finish: finish, players: 16));

            CollectionAssert.Contains(errors.ToArray(), "Finish must be at least 1.");
        }
        [TestMethod]
        public void Validate_FinishEqualToPlayers_ReturnsNoErrors()
        {
            var errors = EventRules.Validate(BuildEvent(finish: 16, players: 16));

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_FinishWithoutPlayers_ReturnsNoErrors()
        {
            var errors = EventRules.Validate(BuildEvent(finish: 40, players: null));

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_LocationTooLong_ReturnsError()
        {
            var errors = EventRules.Validate(BuildEvent(location: new string('l', EventRules.MAX_LOCATION_LENGTH + 1)));

            CollectionAssert.Contains(errors.ToArray(), $"Location must be {EventRules.MAX_LOCATION_LENGTH} characters or fewer.");
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null deck name")]
        [DataRow("", DisplayName = "Empty deck name")]
        [DataRow("   ", DisplayName = "Whitespace deck name")]
        public void Validate_MissingDeckName_ReturnsError(string? deckName)
        {
            var errors = EventRules.Validate(BuildEvent(deckName: deckName!));

            CollectionAssert.Contains(errors.ToArray(), "Deck name is required.");
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null location")]
        [DataRow("", DisplayName = "Empty location")]
        [DataRow("   ", DisplayName = "Whitespace location")]
        public void Validate_MissingLocation_ReturnsError(string? location)
        {
            var errors = EventRules.Validate(BuildEvent(location: location!));

            CollectionAssert.Contains(errors.ToArray(), "Location is required.");
        }
        [TestMethod]
        public void Validate_NullEvent_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => EventRules.Validate(null!));
        }

        [TestMethod]
        [DataRow(0, DisplayName = "Zero players")]
        [DataRow(-1, DisplayName = "Negative players")]
        public void Validate_PlayersBelowOne_ReturnsError(int players)
        {
            var errors = EventRules.Validate(BuildEvent(players: players));

            CollectionAssert.Contains(errors.ToArray(), "Players must be at least 1.");
        }

        [TestMethod]
        [DataRow("javascript:alert(1)", DisplayName = "javascript scheme")]
        [DataRow("ftp://example.test/deck", DisplayName = "ftp scheme")]
        [DataRow("data:text/html,hello", DisplayName = "data scheme")]
        [DataRow("/relative/deck", DisplayName = "relative path")]
        [DataRow("example.test/deck", DisplayName = "no scheme")]
        [DataRow("not a url", DisplayName = "free text")]
        public void Validate_UnacceptableDecklistURL_ReturnsError(string url)
        {
            var errors = EventRules.Validate(BuildEvent(decklistURL: url));

            CollectionAssert.Contains(errors.ToArray(), "Decklist URL must be a full http or https address.");
        }

        [TestMethod]
        public void Validate_UndefinedEventType_ReturnsError()
        {
            var errors = EventRules.Validate(BuildEvent(eventType: (EventType)99));

            CollectionAssert.Contains(errors.ToArray(), "Event type is not valid.");
        }
        [TestMethod]
        public void Validate_ValidEvent_ReturnsNoErrors()
        {
            var errors = EventRules.Validate(BuildEvent());

            Assert.AreEqual(0, errors.Count);
        }

        private static Event BuildEvent(
            string deckName = "Sample Deck",
            string? decklistURL = null,
            EventType eventType = EventType.Locals,
            int? finish = null,
            string? finishNote = null,
            string location = "Test Hobby Shop",
            string? notes = null,
            int? players = null,
            string? topCut = null) =>
            new()
            {
                Date = new DateOnly(2024, 5, 4),
                DeckName = deckName,
                DecklistURL = decklistURL,
                EventType = eventType,
                Finish = finish,
                FinishNote = finishNote,
                Location = location,
                Notes = notes,
                Players = players,
                TopCut = topCut
            };

    }
}
