using CardCollector.Data.Models;
using CardCollector.Rules;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class FormatRulesTests
    {
        [TestMethod]
        public void FindForDate_DateOnRangeBoundaries_IsInclusive()
        {
            var format = Build(1, "Alpha Era", "2024-01-01", "2024-03-31");

            Assert.AreSame(format, FormatRules.FindForDate([format], new DateOnly(2024, 1, 1)));
            Assert.AreSame(format, FormatRules.FindForDate([format], new DateOnly(2024, 3, 31)));
        }

        [TestMethod]
        public void FindForDate_DateOutsideEveryRange_ReturnsNull()
        {
            var format = Build(1, "Alpha Era", "2024-01-01", "2024-03-31");

            Assert.IsNull(FormatRules.FindForDate([format], new DateOnly(2023, 12, 31)));
            Assert.IsNull(FormatRules.FindForDate([format], new DateOnly(2024, 4, 1)));
        }

        [TestMethod]
        public void FindForDate_NullFormats_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => FormatRules.FindForDate(null!, new DateOnly(2024, 1, 1)));
        }

        [TestMethod]
        public void FindForDate_OpenEndedFormat_ContainsEveryLaterDate()
        {
            var ongoing = Build(1, "Current Era", "2025-01-01", null);

            Assert.AreSame(ongoing, FormatRules.FindForDate([ongoing], new DateOnly(2031, 6, 1)));
        }

        [TestMethod]
        public void FindForDate_OverlappingData_PicksLatestStartDate()
        {
            var earlier = Build(1, "Earlier", "2024-01-01", "2024-12-31");
            var later = Build(2, "Later", "2024-06-01", "2024-12-31");

            var result = FormatRules.FindForDate([earlier, later], new DateOnly(2024, 7, 1));

            Assert.AreSame(later, result);
        }
        [TestMethod]
        public void Normalize_BlankAndPaddedNotes_TrimsAndNullsEmpty()
        {
            var padded = FormatRules.Normalize(BuildWithNotes("  some note  "));
            var blank = FormatRules.Normalize(BuildWithNotes("   "));

            Assert.AreEqual("some note", padded.Notes);
            Assert.IsNull(blank.Notes);
        }

        [TestMethod]
        public void Normalize_Input_IsNotMutated()
        {
            var format = Build(0, "  Alpha Era ", "2024-01-01", null, " First ");

            FormatRules.Normalize(format);

            Assert.AreEqual("  Alpha Era ", format.Name);
            Assert.AreEqual(" First ", format.Strategies.Single().Name);
        }

        [TestMethod]
        public void Normalize_NullFormat_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => FormatRules.Normalize(null!));
        }

        [TestMethod]
        public void Normalize_PaddedName_TrimsName()
        {
            var result = FormatRules.Normalize(Build(0, "  Alpha Era  ", "2024-01-01", null));

            Assert.AreEqual("Alpha Era", result.Name);
        }

        [TestMethod]
        public void Normalize_StrategiesAfterDropping_RenumbersPositionsFromZero()
        {
            var format = Build(0, "Alpha Era", "2024-01-01", null, "", "First", "Second");

            var result = FormatRules.Normalize(format);

            CollectionAssert.AreEqual(new[] { 0, 1 }, result.Strategies.Select(s => s.Position).ToArray());
        }

        [TestMethod]
        public void Normalize_StrategiesWithBlanksAndDuplicates_TrimsDropsAndDedupesCaseInsensitively()
        {
            var format = Build(0, "Alpha Era", "2024-01-01", null, "  Alpha Deck ", "", "   ", "alpha deck", "Beta Deck", "ALPHA DECK");

            var result = FormatRules.Normalize(format);

            CollectionAssert.AreEqual(new[] { "Alpha Deck", "Beta Deck" }, result.Strategies.Select(s => s.Name).ToArray());
        }
        [TestMethod]
        public void Validate_EndBeforeStart_ReturnsError()
        {
            var format = Build(0, "Alpha Era", "2024-05-01", "2024-04-30");

            var errors = FormatRules.Validate(format, []);

            CollectionAssert.Contains(errors.ToArray(), "End date must be on or after the start date.");
        }

        [TestMethod]
        public void Validate_EndEqualsStart_IsValid()
        {
            var format = Build(0, "Alpha Era", "2024-05-01", "2024-05-01");

            Assert.AreEqual(0, FormatRules.Validate(format, []).Count);
        }

        [TestMethod]
        public void Validate_MaxStrategies_IsValid()
        {
            var names = Enumerable.Range(1, FormatRules.MAX_STRATEGIES).Select(i => $"Deck {i}").ToArray();
            var format = Build(0, "Alpha Era", "2024-01-01", null, names);

            Assert.AreEqual(0, FormatRules.Validate(format, []).Count);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null name")]
        [DataRow("", DisplayName = "Empty name")]
        [DataRow("   ", DisplayName = "Whitespace name")]
        public void Validate_MissingName_ReturnsNameRequired(string? name)
        {
            var format = Build(0, name!, "2024-01-01", null);

            var errors = FormatRules.Validate(format, []);

            CollectionAssert.Contains(errors.ToArray(), "Name is required.");
        }

        [TestMethod]
        public void Validate_NameAtMaxLength_IsValid()
        {
            var format = Build(0, new string('a', FormatRules.MAX_NAME_LENGTH), "2024-01-01", null);

            Assert.AreEqual(0, FormatRules.Validate(format, []).Count);
        }

        [TestMethod]
        public void Validate_NameOverMaxLength_ReturnsError()
        {
            var format = Build(0, new string('a', FormatRules.MAX_NAME_LENGTH + 1), "2024-01-01", null);

            var errors = FormatRules.Validate(format, []);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "Name must be");
        }
        [TestMethod]
        [DataRow("2024-04-01", "2024-06-30", DisplayName = "Adjacent after (no shared day)")]
        [DataRow("2023-10-01", "2023-12-31", DisplayName = "Adjacent before (no shared day)")]
        [DataRow("2025-01-01", null, DisplayName = "Open-ended starting after")]
        public void Validate_NonOverlappingRange_IsValid(string start, string? end)
        {
            var existing = Build(1, "Existing", "2024-01-01", "2024-03-31");
            var candidate = Build(0, "Candidate", start, end);

            Assert.AreEqual(0, FormatRules.Validate(candidate, [existing]).Count);
        }

        [TestMethod]
        public void Validate_NullArguments_ThrowArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => FormatRules.Validate(null!, []));
            Assert.ThrowsExactly<ArgumentNullException>(() => FormatRules.Validate(Build(0, "A", "2024-01-01", null), null!));
        }

        [TestMethod]
        public void Validate_OpenEndedExistingFormat_OverlapsEveryLaterRange()
        {
            var ongoing = Build(1, "Ongoing", "2024-01-01", null);
            var later = Build(0, "Later", "2030-01-01", "2030-06-30");

            var errors = FormatRules.Validate(later, [ongoing]);

            StringAssert.Contains(errors.Single(), "Ongoing");
        }

        [TestMethod]
        [DataRow("2024-03-31", "2024-06-30", DisplayName = "Shares the last day")]
        [DataRow("2023-10-01", "2024-01-01", DisplayName = "Shares the first day")]
        [DataRow("2024-01-01", "2024-03-31", DisplayName = "Identical")]
        [DataRow("2024-02-01", "2024-02-28", DisplayName = "Contained")]
        [DataRow("2023-01-01", "2025-01-01", DisplayName = "Containing")]
        [DataRow("2024-02-01", null, DisplayName = "Open-ended starting inside")]
        public void Validate_OverlappingRange_ReturnsOverlapError(string start, string? end)
        {
            var existing = Build(1, "Existing", "2024-01-01", "2024-03-31");
            var candidate = Build(0, "Candidate", start, end);

            var errors = FormatRules.Validate(candidate, [existing]);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "\"Existing\"");
        }

        [TestMethod]
        public void Validate_OverlapWithSelf_IsExcludedById()
        {
            var stored = Build(7, "Alpha Era", "2024-01-01", "2024-03-31");
            var edited = Build(7, "Alpha Era", "2024-01-01", "2024-04-30");

            Assert.AreEqual(0, FormatRules.Validate(edited, [stored]).Count);
        }

        [TestMethod]
        public void Validate_StrategyOverMaxLength_ReturnsError()
        {
            var format = Build(0, "Alpha Era", "2024-01-01", null, new string('a', FormatRules.MAX_STRATEGY_LENGTH + 1));

            var errors = FormatRules.Validate(format, []);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "Each strategy");
        }

        [TestMethod]
        public void Validate_TooManyStrategies_ReturnsError()
        {
            var names = Enumerable.Range(1, FormatRules.MAX_STRATEGIES + 1).Select(i => $"Deck {i}").ToArray();
            var format = Build(0, "Alpha Era", "2024-01-01", null, names);

            var errors = FormatRules.Validate(format, []);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains(errors[0], "at most");
        }
        private static Format Build(int id, string name, string start, string? end, params string[] strategies) =>
            new()
            {
                EndDate = end is null ? null : DateOnly.Parse(end),
                ID = id,
                Name = name,
                StartDate = DateOnly.Parse(start),
                Strategies = strategies.Select(s => new FormatStrategy { Name = s }).ToList()
            };

        private static Format BuildWithNotes(string notes)
        {
            var format = Build(0, "Alpha Era", "2024-01-01", null);
            format.Notes = notes;
            return format;
        }
    }
}
