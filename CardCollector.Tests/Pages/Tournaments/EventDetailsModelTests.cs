using System.Text.Json;
using CardCollector.Data.Models;
using CardCollector.Models;
using CardCollector.Pages.Tournaments.Events;
using CardCollector.Rules;
using CardCollector.Services;
using CardCollector.Tests.TestHelpers;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using Match = CardCollector.Data.Models.Match;

namespace CardCollector.Tests.Pages.Tournaments
{
    [TestClass]
    public sealed class EventDetailsModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_DecksExist_ExposesThemAsDeckOptions()
        {
            var options = new[] { new DeckOption(3, "Sample Deck") };
            var context = CreateModel(5, BuildDetail(5), deckOptions: options);

            await context.Model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual("Sample Deck", context.Model.DeckOptions.Single().Name);
        }

        [TestMethod]
        public async Task OnGetAsync_EventExists_ReturnsPageWithDetailAndOpponents()
        {
            var detail = BuildDetail(5, new Match { Result = MatchResult.Win, Round = "1" });
            var context = CreateModel(5, detail);
            context.Matches.Setup(s => s.GetOpponentDecksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["Test Opponent"]);

            var result = await context.Model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreSame(detail, context.Model.Detail);
            CollectionAssert.AreEqual(new[] { "Test Opponent" }, context.Model.OpponentDecks.ToArray());
        }
        [TestMethod]
        public async Task OnGetAsync_EventMissing_ReturnsNotFound()
        {
            var context = CreateModel(999, null);

            var result = await context.Model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
            Assert.IsNull(context.Model.Detail);
        }

        [TestMethod]
        public async Task OnGetAsync_NoIDSupplied_ReturnsNotFound()
        {
            var context = CreateModel(0, null);

            var result = await context.Model.OnGetAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
            context.Events.Verify(s => s.GetAsync(0, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task OnGetAsync_RoundsInOrder_DoesNotFlagThem()
        {
            var detail = BuildDetail(5, new Match { Round = "1" }, new Match { Round = "2" }, new Match { Round = "Top 8" });
            var context = CreateModel(5, detail);

            await context.Model.OnGetAsync(CancellationToken.None);

            Assert.IsFalse(context.Model.AreRoundsOutOfOrder);
        }

        [TestMethod]
        public async Task OnGetAsync_RoundsOutOfOrder_FlagsThem()
        {
            var detail = BuildDetail(5, new Match { Round = "2" }, new Match { Round = "1" });
            var context = CreateModel(5, detail);

            await context.Model.OnGetAsync(CancellationToken.None);

            Assert.IsTrue(context.Model.AreRoundsOutOfOrder);
        }
        [TestMethod]
        public async Task OnPostAddMatchAsync_AjaxBindingError_ReturnsBadRequestWithoutCallingTheService()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Model.ModelState.AddModelError("Input.GamesWon", "not a number");

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            var badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            Assert.AreEqual("Games won must be a whole number.", JsonSerializer.SerializeToElement(badRequest.Value).GetProperty("errors")[0].GetString());
            context.Matches.Verify(s => s.AddAsync(It.IsAny<int>(), It.IsAny<Match>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostAddMatchAsync_AjaxEventMissing_ReturnsNotFound()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Missing("Event not found."));

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OnPostAddMatchAsync_AjaxSuccess_ReturnsRowSummaryAndNextRound()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Model.Input = new MatchInputModel { GamesWon = 2, OpponentDeck = "Test Opponent", Result = MatchResult.Win, Round = "1" };
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Success(new Match { ID = 7 }, BuildSummary(roundCount: 1, nextRound: "2"), previousMatchID: 3));

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            var json = ToJson(result);
            Assert.AreEqual(3, json.GetProperty("previousMatchId").GetInt32());
            Assert.AreEqual("<row/>", json.GetProperty("rowHtml").GetString());
            Assert.AreEqual("<summary/>", json.GetProperty("summaryHtml").GetString());
            Assert.AreEqual("2", json.GetProperty("nextRound").GetString());
            Assert.AreEqual(1, json.GetProperty("roundCount").GetInt32());
        }

        [TestMethod]
        public async Task OnPostAddMatchAsync_AjaxValidationFailure_ReturnsBadRequestWithErrors()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Failure(["Opponent deck is required."]));

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            var badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            var errors = JsonSerializer.SerializeToElement(badRequest.Value).GetProperty("errors");
            Assert.AreEqual("Opponent deck is required.", errors[0].GetString());
        }
        [TestMethod]
        public async Task OnPostAddMatchAsync_BlankGames_AreSentAsZero()
        {
            var context = CreateModel(5, null);
            Match? sent = null;
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>()))
                .Callback<int, Match, CancellationToken>((_, match, _) => sent = match)
                .ReturnsAsync(MatchSaveResult.Success(new Match { ID = 7 }, BuildSummary(roundCount: 1, nextRound: "2")));
            context.Model.Input = new MatchInputModel { OpponentDeck = "Test Opponent", Round = "1" };

            await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            Assert.AreEqual(0, sent!.GamesWon + sent.GamesLost + sent.GamesTied);
            Assert.AreEqual(5, sent.EventID);
        }

        [TestMethod]
        public async Task OnPostAddMatchAsync_NonAjaxEventMissing_SetsErrorAndRedirects()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Missing("Event not found."));

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Event not found.", context.Model.TempData["Error"]);
        }

        [TestMethod]
        public async Task OnPostAddMatchAsync_NonAjaxSuccess_SetsSuccessAndRedirectsToRounds()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Success(new Match { ID = 7 }, BuildSummary(roundCount: 1, nextRound: "2")));

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            var redirect = Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("rounds", redirect.Fragment);
            Assert.AreEqual("Round added.", context.Model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostAddMatchAsync_NonAjaxValidationFailure_SetsErrorAndRedirects()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Failure(["Round is required.", "Opponent deck is required."]));

            var result = await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Round is required. Opponent deck is required.", context.Model.TempData["Error"]);
        }
        [TestMethod]
        public async Task OnPostAddMatchAsync_NoResultChosen_UsesTheSuggestionFromTheScore()
        {
            var context = CreateModel(5, null);
            Match? sent = null;
            context.Matches.Setup(s => s.AddAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>()))
                .Callback<int, Match, CancellationToken>((_, match, _) => sent = match)
                .ReturnsAsync(MatchSaveResult.Success(new Match { ID = 7 }, BuildSummary(roundCount: 1, nextRound: "2")));
            context.Model.Input = new MatchInputModel { GamesLost = 2, GamesWon = 0, OpponentDeck = "Test Opponent", Round = "1" };

            await context.Model.OnPostAddMatchAsync(CancellationToken.None);

            Assert.AreEqual(MatchResult.Loss, sent!.Result);
        }
        [TestMethod]
        public async Task OnPostDeleteMatchAsync_AjaxRoundMissing_ReturnsNotFound()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Matches.Setup(s => s.DeleteAsync(5, 7, It.IsAny<CancellationToken>())).ReturnsAsync((MatchSummaryViewModel?)null);

            var result = await context.Model.OnPostDeleteMatchAsync(7, CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OnPostDeleteMatchAsync_AjaxSuccess_ReturnsSummaryWithoutARow()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Matches.Setup(s => s.DeleteAsync(5, 7, It.IsAny<CancellationToken>())).ReturnsAsync(BuildSummary(roundCount: 0, nextRound: "1"));

            var result = await context.Model.OnPostDeleteMatchAsync(7, CancellationToken.None);

            var json = ToJson(result);
            Assert.AreEqual(0, json.GetProperty("roundCount").GetInt32());
            Assert.AreEqual("1", json.GetProperty("nextRound").GetString());
            Assert.AreEqual("<summary/>", json.GetProperty("summaryHtml").GetString());
            Assert.IsFalse(json.TryGetProperty("rowHtml", out _));
        }
        [TestMethod]
        public async Task OnPostDeleteMatchAsync_NonAjaxRoundMissing_SetsErrorAndRedirects()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.DeleteAsync(5, 7, It.IsAny<CancellationToken>())).ReturnsAsync((MatchSummaryViewModel?)null);

            var result = await context.Model.OnPostDeleteMatchAsync(7, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("That round no longer exists.", context.Model.TempData["Error"]);
        }

        [TestMethod]
        public async Task OnPostDeleteMatchAsync_NonAjaxSuccess_SetsSuccessAndRedirects()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.DeleteAsync(5, 7, It.IsAny<CancellationToken>())).ReturnsAsync(BuildSummary(roundCount: 0, nextRound: "1"));

            var result = await context.Model.OnPostDeleteMatchAsync(7, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Round deleted.", context.Model.TempData["Success"]);
        }
        [TestMethod]
        public async Task OnPostEditMatchAsync_AjaxRoundBelongsToAnotherEvent_ReturnsNotFound()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Model.Input = new MatchInputModel { ID = 7, OpponentDeck = "Test Opponent", Round = "1" };
            context.Matches.Setup(s => s.UpdateAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Missing("Round not found."));

            var result = await context.Model.OnPostEditMatchAsync(CancellationToken.None);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditMatchAsync_AjaxSuccess_ReturnsRowAndSummary()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Model.Input = new MatchInputModel { ID = 7, OpponentDeck = "Test Opponent", Result = MatchResult.Loss, Round = "1" };
            context.Matches.Setup(s => s.UpdateAsync(5, It.Is<Match>(m => m.ID == 7), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Success(new Match { ID = 7 }, BuildSummary(roundCount: 3, nextRound: "4")));

            var result = await context.Model.OnPostEditMatchAsync(CancellationToken.None);

            var json = ToJson(result);
            Assert.AreEqual("<row/>", json.GetProperty("rowHtml").GetString());
            Assert.AreEqual("4", json.GetProperty("nextRound").GetString());
        }
        [TestMethod]
        public async Task OnPostEditMatchAsync_AjaxValidationFailure_ReturnsBadRequest()
        {
            var context = CreateModel(5, null, ajax: true);
            context.Matches.Setup(s => s.UpdateAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Failure(["Round is required."]));

            var result = await context.Model.OnPostEditMatchAsync(CancellationToken.None);

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        }

        [TestMethod]
        public async Task OnPostEditMatchAsync_NonAjaxSuccess_SetsSuccessAndRedirects()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.UpdateAsync(5, It.IsAny<Match>(), It.IsAny<CancellationToken>())).ReturnsAsync(MatchSaveResult.Success(new Match { ID = 7 }, BuildSummary(roundCount: 1, nextRound: "2")));

            var result = await context.Model.OnPostEditMatchAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Round updated.", context.Model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostSortMatchesAsync_RoundsAlreadyInOrder_SaysSo()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.SortAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await context.Model.OnPostSortMatchesAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Rounds were already in order.", context.Model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostSortMatchesAsync_RoundsWereOutOfOrder_SetsSuccessAndRedirectsToRounds()
        {
            var context = CreateModel(5, null);
            context.Matches.Setup(s => s.SortAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await context.Model.OnPostSortMatchesAsync(CancellationToken.None);

            var redirect = Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("rounds", redirect.Fragment);
            Assert.AreEqual("Rounds sorted.", context.Model.TempData["Success"]);
        }
        private static EventDetailViewModel BuildDetail(int id, params Match[] rounds) =>
            new()
            {
                Event = new Event { ID = id, Location = "Test Hobby Shop", Matches = rounds },
                Summary = MatchRules.Summarize(rounds)
            };

        private static MatchSummaryViewModel BuildSummary(int roundCount, string nextRound) =>
            new()
            {
                DiceRecord = new DiceRecord(0, 0),
                GameRecord = new WinLossTie(0, 0, 0),
                MatchRecord = new WinLossTie(0, 0, 0),
                NextRound = nextRound,
                RoundCount = roundCount
            };

        private static PageTestContext CreateModel(int id, EventDetailViewModel? detail, bool ajax = false, IReadOnlyList<DeckOption>? deckOptions = null)
        {
            var decks = new Mock<IDeckService>();
            decks.Setup(s => s.GetOptionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deckOptions ?? []);

            var events = new Mock<IEventService>();
            events.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

            var matches = new Mock<IMatchService>();
            matches.Setup(s => s.GetOpponentDecksAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var renderer = new Mock<IRazorPartialRenderer>();
            renderer.Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_MatchRow", It.IsAny<Match>())).ReturnsAsync("<row/>");
            renderer.Setup(r => r.RenderPartialAsync(It.IsAny<PageModel>(), "_MatchSummary", It.IsAny<MatchSummaryViewModel>())).ReturnsAsync("<summary/>");

            var model = new DetailsModel(decks.Object, events.Object, matches.Object, renderer.Object) { ID = id };
            PageContextFactory.Attach(model, http =>
            {
                if (ajax)
                    http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            });

            return new PageTestContext(model, events, matches);
        }

        private static JsonElement ToJson(IActionResult result)
        {
            var json = Assert.IsInstanceOfType<JsonResult>(result);
            return JsonSerializer.SerializeToElement(json.Value);
        }

        private sealed record PageTestContext(DetailsModel Model, Mock<IEventService> Events, Mock<IMatchService> Matches);
    }
}
