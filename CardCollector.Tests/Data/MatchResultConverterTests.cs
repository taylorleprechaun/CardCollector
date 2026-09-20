using CardCollector.Data;
using CardCollector.Data.Models;
using CardCollector.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Tests.Data
{
    [TestClass]
    public sealed class MatchResultConverterTests
    {
        [TestMethod]
        [DataRow("L", MatchResult.Loss, DisplayName = "L")]
        [DataRow("T", MatchResult.Tie, DisplayName = "T")]
        [DataRow("W", MatchResult.Win, DisplayName = "W")]
        public void ConvertFromProvider_Letter_ReturnsResult(string stored, MatchResult expected)
        {
            var converter = new MatchResultConverter();

            var result = converter.ConvertFromProvider(stored);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ConvertFromProvider_UnknownLetter_Throws()
        {
            var converter = new MatchResultConverter();

            Assert.ThrowsExactly<InvalidOperationException>(() => converter.ConvertFromProvider("X"));
        }

        [TestMethod]
        [DataRow(MatchResult.Loss, "L", DisplayName = "Loss")]
        [DataRow(MatchResult.Tie, "T", DisplayName = "Tie")]
        [DataRow(MatchResult.Win, "W", DisplayName = "Win")]
        public void ConvertToProvider_Result_ReturnsSingleLetter(MatchResult result, string expected)
        {
            var converter = new MatchResultConverter();

            var stored = converter.ConvertToProvider(result);

            Assert.AreEqual(expected, stored);
        }
        [TestMethod]
        public void ConvertToProvider_UndefinedResult_Throws()
        {
            var converter = new MatchResultConverter();

            Assert.ThrowsExactly<InvalidOperationException>(() => converter.ConvertToProvider((MatchResult)99));
        }

        [TestMethod]
        public async Task SaveChanges_EventType_IsStoredAsItsName()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = connection;
            using var __ = context;
            context.Events.Add(new Event { Date = new DateOnly(2024, 5, 4), DeckName = "Sample Deck", EventType = EventType.WinAMat, Location = "Test Hobby Shop" });
            await context.SaveChangesAsync();

            var stored = await context.Database
                .SqlQueryRaw<string>("SELECT \"EventType\" AS \"Value\" FROM \"Events\"")
                .SingleAsync();

            Assert.AreEqual("WinAMat", stored);
        }

        [TestMethod]
        public async Task SaveChanges_MatchResult_IsStoredAsSingleLetterInTheDatabase()
        {
            var (context, connection) = InMemoryDbContextFactory.CreateSqlite();
            using var _ = connection;
            using var __ = context;
            var tournamentEvent = new Event { Date = new DateOnly(2024, 5, 4), DeckName = "Sample Deck", Location = "Test Hobby Shop" };
            context.Events.Add(tournamentEvent);
            await context.SaveChangesAsync();
            context.Matches.Add(new Match { EventID = tournamentEvent.ID, OpponentDeck = "Sample Opponent", Result = MatchResult.Tie, Round = "1", Sequence = 1 });
            await context.SaveChangesAsync();

            var stored = await context.Database
                .SqlQueryRaw<string>("SELECT \"Result\" AS \"Value\" FROM \"Matches\"")
                .SingleAsync();

            Assert.AreEqual("T", stored);
        }
    }
}
