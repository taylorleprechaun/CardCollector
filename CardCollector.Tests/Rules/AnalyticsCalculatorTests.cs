using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Rules;
using CardCollector.ViewModels;

namespace CardCollector.Tests.Rules
{
    [TestClass]
    public sealed class AnalyticsCalculatorTests
    {
        private static readonly Format EarlyFormat = BuildFormat(1, "Early Format", "2024-01-01", "2024-03-31");
        private static readonly Format LateFormat = BuildFormat(2, "Late Format", "2024-04-01", null);

        // A property rather than a field so it never depends on static field initialization order.
        private static IReadOnlyList<Format> Formats => [LateFormat, EarlyFormat];

        [TestMethod]
        public void Calculate_ByDeck_GroupsIgnoringCaseAndOrdersByEventCount()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Alpha Deck"),
                BuildEvent(2, "2024-02-02", "Beta Deck"),
                BuildEvent(3, "2024-05-01", "beta deck "));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(2, report.ByDeck.Count);
            Assert.AreEqual("Beta Deck", report.ByDeck[0].Label);
            Assert.AreEqual(2, report.ByDeck[0].EventCount);
        }

        [TestMethod]
        public void Calculate_ByeStoredAsTie_CountsAsTie()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck",
                matches: [Bye(MatchResult.Tie), Round(MatchResult.Win)]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(new WinLossTie(1, 0, 1), report.Summary.MatchRecord);
        }

        [TestMethod]
        public void Calculate_ByEventType_UsesDisplayNameAndOrdersByEventCount()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Sample Deck", eventType: EventType.WinAMat, matches: [Round(MatchResult.Win)]),
                BuildEvent(2, "2024-02-02", "Sample Deck", eventType: EventType.WinAMat, matches: [Round(MatchResult.Loss)]),
                BuildEvent(3, "2024-02-03", "Sample Deck", eventType: EventType.Locals));

            var report = AnalyticsCalculator.Calculate(events, 1);

            var winAMat = report.ByEventType[0];
            Assert.AreEqual("Win-A-Mat", winAMat.Label);
            Assert.AreEqual(2, winAMat.EventCount);
            Assert.AreEqual(new WinLossTie(1, 1, 0), winAMat.Record);
            Assert.AreEqual("Locals", report.ByEventType[1].Label);
        }

        [TestMethod]
        public void Calculate_ByFormat_OldestFormatFirstAndNoFormatLast()
        {
            var events = Prepare(
                BuildEvent(1, "2024-05-01", "Sample Deck"),
                BuildEvent(2, "2023-06-01", "Sample Deck"),
                BuildEvent(3, "2024-02-01", "Sample Deck"));

            var report = AnalyticsCalculator.Calculate(events, 1);

            CollectionAssert.AreEqual(
                new[] { "Early Format", "Late Format", FormatRules.NO_FORMAT_NAME },
                report.ByFormat.Select(r => r.Label).ToArray());
        }
        [TestMethod]
        public void Calculate_DeckFormats_GroupsDeckNamesIgnoringCaseWithinEachFormat()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Sample Deck", matches: [Round(MatchResult.Win), Round(MatchResult.Loss)]),
                BuildEvent(2, "2024-03-01", "SAMPLE deck", matches: [Round(MatchResult.Tie)]),
                BuildEvent(3, "2024-05-01", "Sample Deck", matches: [Round(MatchResult.Win)]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(2, report.DeckFormats.Count);
            var early = report.DeckFormats.Single(r => r.FormatName == "Early Format");
            Assert.AreEqual("Sample Deck", early.DeckName);
            Assert.AreEqual(2, early.EventCount);
            Assert.AreEqual(new WinLossTie(1, 1, 1), early.Record);
        }

        [TestMethod]
        public void Calculate_DeckFormats_NewestFormatFirstThenDeckNameWithNoFormatLast()
        {
            var events = Prepare(
                BuildEvent(1, "2023-06-01", "Old Deck"),
                BuildEvent(2, "2024-02-01", "Beta Deck"),
                BuildEvent(3, "2024-02-02", "Alpha Deck"),
                BuildEvent(4, "2024-05-01", "Gamma Deck"));

            var report = AnalyticsCalculator.Calculate(events, 1);

            CollectionAssert.AreEqual(
                new[] { "Gamma Deck", "Alpha Deck", "Beta Deck", "Old Deck" },
                report.DeckFormats.Select(r => r.DeckName).ToArray());
            Assert.AreEqual(FormatRules.NO_FORMAT_NAME, report.DeckFormats[^1].FormatName);
            Assert.IsNull(report.DeckFormats[^1].FormatStartDate);
        }

        [TestMethod]
        public void Calculate_DeckFormatTotal_SumsEveryEventAndRound()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Alpha Deck", matches: [Round(MatchResult.Win), Bye()]),
                BuildEvent(2, "2024-05-01", "Beta Deck", matches: [Round(MatchResult.Loss), Round(MatchResult.Tie)]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(AnalyticsCalculator.TOTAL_LABEL, report.DeckFormatTotal.Label);
            Assert.AreEqual(2, report.DeckFormatTotal.EventCount);
            Assert.AreEqual(new WinLossTie(2, 1, 1), report.DeckFormatTotal.Record);
        }
        [TestMethod]
        public void Calculate_DiceRolls_BucketsNonByeRoundsByRoll()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck", matches:
            [
                Round(MatchResult.Win, wonDiceRoll: true),
                Round(MatchResult.Loss, wonDiceRoll: true),
                Round(MatchResult.Loss, wonDiceRoll: false),
                Round(MatchResult.Win),
                Bye()
            ]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            CollectionAssert.AreEqual(
                new[] { AnalyticsCalculator.DICE_WON_LABEL, AnalyticsCalculator.DICE_LOST_LABEL, AnalyticsCalculator.DICE_NOT_RECORDED_LABEL },
                report.Dice.Select(r => r.Label).ToArray());
            Assert.AreEqual(new WinLossTie(1, 1, 0), report.Dice[0].Record);
            Assert.AreEqual(new WinLossTie(0, 1, 0), report.Dice[1].Record);
            Assert.AreEqual(new WinLossTie(1, 0, 0), report.Dice[2].Record);
        }

        [TestMethod]
        public void Calculate_DiceRollsAllUnrecorded_WonAndLostBucketsAreEmpty()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck", matches: [Round(MatchResult.Win), Round(MatchResult.Loss)]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(0, report.Dice[0].Record.Total);
            Assert.IsNull(report.Dice[0].Record.WinRate);
            Assert.AreEqual(0, report.Dice[1].Record.Total);
            Assert.AreEqual(2, report.Dice[2].Record.Total);
        }
        [TestMethod]
        public void Calculate_EmptyInput_ReturnsZeroedReport()
        {
            var report = AnalyticsCalculator.Calculate([], AnalyticsCriteria.DEFAULT_MIN_MATCHES);

            Assert.IsTrue(report.IsEmpty);
            Assert.AreEqual(0, report.Summary.EventCount);
            Assert.IsNull(report.Summary.MatchRecord.WinRate);
            Assert.IsNull(report.Summary.GameRecord.WinRate);
            Assert.AreEqual(0, report.Summary.FirstPlaceCount);
            Assert.AreEqual(0, report.DeckFormats.Count);
            Assert.AreEqual(0, report.Matchups.Count);
            Assert.AreEqual(3, report.Dice.Count);
            Assert.IsNull(report.DeckFormatTotal.Record.WinRate);
        }

        [TestMethod]
        public void Calculate_EventWithNoRounds_CountsTheEventWithAnEmptyRecord()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck"));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(1, report.Summary.EventCount);
            Assert.AreEqual(0, report.Summary.MatchRecord.Total);
            Assert.AreEqual(1, report.DeckFormats.Single().EventCount);
        }

        [TestMethod]
        public void Calculate_FirstPlaces_CountsOnlyEventsFinishedFirst()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Sample Deck", finish: 1),
                BuildEvent(2, "2024-02-02", "Sample Deck", finish: 1),
                BuildEvent(3, "2024-02-03", "Sample Deck", finish: 2),
                BuildEvent(4, "2024-02-04", "Sample Deck"));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(2, report.Summary.FirstPlaceCount);
        }

        [TestMethod]
        public void Calculate_GameRecord_SumsGamesAcrossRounds()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck", matches:
            [
                Round(MatchResult.Win, won: 2, lost: 1),
                Round(MatchResult.Tie, won: 1, lost: 1, tied: 1)
            ]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(new WinLossTie(3, 2, 1), report.Summary.GameRecord);
            Assert.AreEqual(0.5833, report.Summary.GameRecord.WinRate!.Value, 1e-4);
        }

        [TestMethod]
        public void Calculate_Matchups_ExcludesByesGroupsIgnoringCaseAndOrdersByRounds()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Sample Deck", matches:
                [
                    Round(MatchResult.Win, opponent: "Rare Opponent"),
                    Round(MatchResult.Loss, opponent: "Common Opponent"),
                    Bye(),
                    Bye()
                ]),
                BuildEvent(2, "2024-02-02", "Sample Deck", matches:
                [
                    Round(MatchResult.Win, opponent: "common opponent"),
                    Round(MatchResult.Tie, opponent: "Common Opponent")
                ]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            CollectionAssert.AreEqual(new[] { "Common Opponent", "Rare Opponent" }, report.Matchups.Select(r => r.Label).ToArray());
            Assert.AreEqual(new WinLossTie(1, 1, 1), report.Matchups[0].Record);
            Assert.AreEqual(2, report.Matchups[0].EventCount);
        }

        [TestMethod]
        [DataRow(2, 1, DisplayName = "Default minimum drops single-round opponents")]
        [DataRow(1, 2, DisplayName = "Minimum of 1 keeps everyone")]
        [DataRow(0, 2, DisplayName = "Minimum below 1 counts as 1")]
        [DataRow(3, 0, DisplayName = "Minimum above every count leaves nothing")]
        public void Calculate_MatchupsMinimum_KeepsOpponentsWithEnoughRounds(int minMatches, int expectedRows)
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck", matches:
            [
                Round(MatchResult.Win, opponent: "Frequent Opponent"),
                Round(MatchResult.Loss, opponent: "Frequent Opponent"),
                Round(MatchResult.Win, opponent: "One-Off Opponent")
            ]));

            var report = AnalyticsCalculator.Calculate(events, minMatches);

            Assert.AreEqual(expectedRows, report.Matchups.Count);
        }
        [TestMethod]
        public void Calculate_NullEvents_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => AnalyticsCalculator.Calculate(null!, 1));
        }

        [TestMethod]
        public void Calculate_SameDateEvents_CountsBoth()
        {
            var events = Prepare(
                BuildEvent(1, "2024-02-01", "Sample Deck", location: "Test Arena Hall A", matches: [Round(MatchResult.Win)]),
                BuildEvent(2, "2024-02-01", "Sample Deck", location: "Test Arena Hall B", matches: [Round(MatchResult.Loss)]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(2, report.Summary.EventCount);
            Assert.AreEqual(new WinLossTie(1, 1, 0), report.Summary.MatchRecord);
        }

        [TestMethod]
        public void Calculate_StoredResultDisagreesWithScore_UsesStoredResult()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck", matches: [Round(MatchResult.Loss, won: 1, lost: 1)]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(new WinLossTie(0, 1, 0), report.Summary.MatchRecord);
        }

        [TestMethod]
        public void Calculate_TiesInRecord_CountAsHalfAWin()
        {
            var events = Prepare(BuildEvent(1, "2024-02-01", "Sample Deck", matches:
            [
                Round(MatchResult.Win), Round(MatchResult.Win), Round(MatchResult.Win),
                Round(MatchResult.Loss), Round(MatchResult.Loss),
                Round(MatchResult.Tie), Round(MatchResult.Tie), Round(MatchResult.Tie)
            ]));

            var report = AnalyticsCalculator.Calculate(events, 1);

            Assert.AreEqual(0.5625, report.Summary.MatchRecord.WinRate!.Value, 1e-9);
        }

        [TestMethod]
        public void Filter_CombinedCriteria_KeepsOnlyEventsMatchingEveryFilter()
        {
            var events = new[]
            {
                BuildEvent(1, "2024-02-01", "Sample Deck", eventType: EventType.Regional, location: "Test Convention Center"),
                BuildEvent(2, "2024-02-02", "Sample Deck", eventType: EventType.Locals, location: "Test Convention Center"),
                BuildEvent(3, "2024-05-01", "Sample Deck", eventType: EventType.Regional, location: "Test Convention Center"),
                BuildEvent(4, "2024-02-03", "Other Deck", eventType: EventType.Regional, location: "Test Convention Center")
            };
            var criteria = new AnalyticsCriteria { DeckName = "sample", EventType = EventType.Regional, FormatID = EarlyFormat.ID, Location = "convention" };

            var filtered = AnalyticsCalculator.Filter(events, Formats, criteria);

            Assert.AreEqual(1, filtered.Single().Event.ID);
        }

        [TestMethod]
        public void Filter_DateRange_IsInclusive()
        {
            var events = new[]
            {
                BuildEvent(1, "2024-01-31", "Sample Deck"),
                BuildEvent(2, "2024-02-01", "Sample Deck"),
                BuildEvent(3, "2024-02-29", "Sample Deck"),
                BuildEvent(4, "2024-03-01", "Sample Deck")
            };
            var criteria = new AnalyticsCriteria { DateFrom = new DateOnly(2024, 2, 1), DateTo = new DateOnly(2024, 2, 29) };

            var filtered = AnalyticsCalculator.Filter(events, Formats, criteria);

            CollectionAssert.AreEqual(new[] { 2, 3 }, filtered.Select(e => e.Event.ID).ToArray());
        }

        [TestMethod]
        [DataRow("SAMPLE", DisplayName = "Different case")]
        [DataRow("  ple De  ", DisplayName = "Padded partial text")]
        public void Filter_DeckName_MatchesContainsIgnoringCase(string deckName)
        {
            var events = new[] { BuildEvent(1, "2024-02-01", "Sample Deck"), BuildEvent(2, "2024-02-02", "Other Deck") };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { DeckName = deckName });

            Assert.AreEqual(1, filtered.Single().Event.ID);
        }

        [TestMethod]
        public void Filter_EventType_KeepsOnlyThatType()
        {
            var events = new[]
            {
                BuildEvent(1, "2024-02-01", "Sample Deck", eventType: EventType.YCS),
                BuildEvent(2, "2024-02-02", "Sample Deck", eventType: EventType.Locals)
            };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { EventType = EventType.YCS });

            Assert.AreEqual(1, filtered.Single().Event.ID);
        }

        [TestMethod]
        public void Filter_ExcludeByes_DropsByeRoundsButKeepsTheEvent()
        {
            var events = new[] { BuildEvent(1, "2024-02-01", "Sample Deck", matches: [Bye(), Round(MatchResult.Loss)]) };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { ExcludeByes = true });

            var single = filtered.Single();
            Assert.AreEqual(1, single.Matches.Count);
            Assert.IsFalse(single.Matches[0].IsBye);
        }

        [TestMethod]
        public void Filter_FormatID_UsesTheFormatDerivedFromEachEventsDate()
        {
            var events = new[]
            {
                BuildEvent(1, "2024-03-31", "Sample Deck"),
                BuildEvent(2, "2024-04-01", "Sample Deck"),
                BuildEvent(3, "2023-06-01", "Sample Deck")
            };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { FormatID = EarlyFormat.ID });

            var single = filtered.Single();
            Assert.AreEqual(1, single.Event.ID);
            Assert.AreSame(EarlyFormat, single.Format);
        }

        [TestMethod]
        public void Filter_IncludeByes_KeepsEveryRound()
        {
            var events = new[] { BuildEvent(1, "2024-02-01", "Sample Deck", matches: [Bye(), Round(MatchResult.Loss)]) };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria());

            Assert.AreEqual(2, filtered.Single().Matches.Count);
        }

        [TestMethod]
        public void Filter_Location_MatchesContainsIgnoringCase()
        {
            var events = new[]
            {
                BuildEvent(1, "2024-02-01", "Sample Deck", location: "Test Hobby Shop"),
                BuildEvent(2, "2024-02-02", "Sample Deck", location: "Test Arena")
            };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { Location = "hobby" });

            Assert.AreEqual(1, filtered.Single().Event.ID);
        }

        [TestMethod]
        public void Filter_NoCriteria_KeepsEveryEventWithItsFormat()
        {
            var events = new[] { BuildEvent(1, "2024-02-01", "Sample Deck"), BuildEvent(2, "2023-06-01", "Sample Deck") };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria());

            Assert.AreEqual(2, filtered.Count);
            Assert.AreSame(EarlyFormat, filtered[0].Format);
            Assert.IsNull(filtered[1].Format);
        }

        [TestMethod]
        public void Filter_NullArguments_ThrowArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => AnalyticsCalculator.Filter(null!, Formats, new AnalyticsCriteria()));
            Assert.ThrowsExactly<ArgumentNullException>(() => AnalyticsCalculator.Filter([], null!, new AnalyticsCriteria()));
            Assert.ThrowsExactly<ArgumentNullException>(() => AnalyticsCalculator.Filter([], Formats, null!));
        }

        [TestMethod]
        public void Filter_Opponent_KeepsMatchingRoundsAndOnlyEventsThatHaveOne()
        {
            var events = new[]
            {
                BuildEvent(1, "2024-02-01", "Sample Deck", matches:
                [
                    Round(MatchResult.Win, opponent: "Target Opponent"),
                    Round(MatchResult.Loss, opponent: "Someone Else")
                ]),
                BuildEvent(2, "2024-02-02", "Sample Deck", matches: [Round(MatchResult.Loss, opponent: "Someone Else")]),
                BuildEvent(3, "2024-02-03", "Sample Deck")
            };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { Opponent = "target" });

            var single = filtered.Single();
            Assert.AreEqual(1, single.Event.ID);
            Assert.AreEqual("Target Opponent", single.Matches.Single().OpponentDeck);
        }

        [TestMethod]
        public void Filter_UnknownFormatID_ReturnsNothing()
        {
            var events = new[] { BuildEvent(1, "2024-02-01", "Sample Deck") };

            var filtered = AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria { FormatID = 999 });

            Assert.AreEqual(0, filtered.Count);
        }

                private static Event BuildEvent(
                    int id,
                    string date,
                    string deckName,
                    int? finish = null,
                    EventType eventType = EventType.Locals,
                    string location = "Test Hobby Shop",
                    IReadOnlyList<Match>? matches = null) =>
                    new()
                    {
                        Date = DateOnly.Parse(date),
                        DeckName = deckName,
                        EventType = eventType,
                        Finish = finish,
                        ID = id,
                        Location = location,
                        Matches = matches ?? []
                    };

                private static Format BuildFormat(int id, string name, string start, string? end) =>
    new()
    {
        EndDate = end is null ? null : DateOnly.Parse(end),
        ID = id,
        Name = name,
        StartDate = DateOnly.Parse(start)
    };

                private static Match Bye(MatchResult result = MatchResult.Win) =>
                            new() { IsBye = true, OpponentDeck = MatchRules.BYE_OPPONENT_NAME, Result = result, Round = "1" };
        private static IReadOnlyList<AnalyticsEvent> Prepare(params Event[] events) =>
            AnalyticsCalculator.Filter(events, Formats, new AnalyticsCriteria());

        private static Match Round(
            MatchResult result,
            string opponent = "Sample Opponent",
            int won = 0,
            int lost = 0,
            int tied = 0,
            bool? wonDiceRoll = null) =>
            new()
            {
                GamesLost = lost,
                GamesTied = tied,
                GamesWon = won,
                OpponentDeck = opponent,
                Result = result,
                Round = "1",
                WonDiceRoll = wonDiceRoll
            };
    }
}
