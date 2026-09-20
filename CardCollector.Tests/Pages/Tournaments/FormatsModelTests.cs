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
    public sealed class FormatsModelTests
    {
        [TestMethod]
        public async Task OnGetAsync_EndedFormat_HasNoCurrentFormat()
        {
            var (model, _) = CreateModel(BuildFormat(1, "Ended", new DateOnly(2000, 1, 1), new DateOnly(2000, 12, 31)));

            await model.OnGetAsync(CancellationToken.None);

            Assert.IsNull(model.CurrentFormatID);
        }

        [TestMethod]
        public async Task OnGetAsync_FutureFormat_HasNoCurrentFormat()
        {
            var (model, _) = CreateModel(BuildFormat(1, "Future", new DateOnly(2999, 1, 1), null));

            await model.OnGetAsync(CancellationToken.None);

            Assert.IsNull(model.CurrentFormatID);
        }

        [TestMethod]
        public async Task OnGetAsync_OpenEndedFormatAlreadyStarted_IsCurrent()
        {
            var (model, _) = CreateModel(
                BuildFormat(1, "Ended", new DateOnly(2000, 1, 1), new DateOnly(2000, 12, 31)),
                BuildFormat(2, "Ongoing", new DateOnly(2001, 1, 1), null));

            await model.OnGetAsync(CancellationToken.None);

            Assert.AreEqual(2, model.CurrentFormatID);
            Assert.AreEqual(2, model.Formats.Count);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_FormatExists_SetsSuccessAndRedirects()
        {
            var (model, service) = CreateModel();
            service.Setup(s => s.DeleteAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await model.OnPostDeleteAsync(5, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Format deleted.", model.TempData["Success"]);
        }

        [TestMethod]
        public async Task OnPostDeleteAsync_FormatMissing_SetsErrorAndRedirects()
        {
            var (model, service) = CreateModel();
            service.Setup(s => s.DeleteAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await model.OnPostDeleteAsync(5, CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("That format no longer exists.", model.TempData["Error"]);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_EditingExistingFormat_CallsUpdate()
        {
            var (model, service) = CreateModel();
            service.Setup(s => s.UpdateAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>())).ReturnsAsync(FormatSaveResult.Success());
            model.Input = new FormatInputModel { ID = 9, Name = "Alpha Era", StartDate = new DateOnly(2024, 1, 1) };

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Format updated.", model.TempData["Success"]);
            service.Verify(s => s.UpdateAsync(It.Is<Format>(f => f.ID == 9), It.IsAny<CancellationToken>()), Times.Once);
            service.Verify(s => s.AddAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_InvalidEndDateBinding_ReturnsErrorWithoutSaving()
        {
            var (model, service) = CreateModel();
            model.Input = new FormatInputModel { Name = "Alpha Era", StartDate = new DateOnly(2024, 1, 1) };
            model.ModelState.AddModelError("Input.EndDate", "not a date");

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            CollectionAssert.Contains(model.Errors.ToArray(), "End date is not a valid date.");
            Assert.IsTrue(model.ShowFormModal);
            service.Verify(s => s.AddAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_InvalidStartDateBinding_ReportsInvalidNotRequired()
        {
            var (model, _) = CreateModel();
            model.Input = new FormatInputModel { Name = "Alpha Era" };
            model.ModelState.AddModelError("Input.StartDate", "not a date");

            await model.OnPostSaveAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "Start date is not a valid date." }, model.Errors.ToArray());
        }

        [TestMethod]
        public async Task OnPostSaveAsync_MissingStartDate_ReturnsRequiredErrorWithoutSaving()
        {
            var (model, service) = CreateModel();
            model.Input = new FormatInputModel { Name = "Alpha Era" };

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            CollectionAssert.AreEqual(new[] { "Start date is required." }, model.Errors.ToArray());
            service.Verify(s => s.AddAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_NewFormat_CallsAddAndRedirects()
        {
            var (model, service) = CreateModel();
            service.Setup(s => s.AddAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>())).ReturnsAsync(FormatSaveResult.Success());
            model.Input = new FormatInputModel { Name = "Alpha Era", StartDate = new DateOnly(2024, 1, 1), Strategies = ["First", null, "Second"] };

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<RedirectToPageResult>(result);
            Assert.AreEqual("Format added.", model.TempData["Success"]);
            service.Verify(s => s.AddAsync(
                It.Is<Format>(f => f.Name == "Alpha Era" && f.Strategies.Count() == 3),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_OngoingChecked_DiscardsEndDate()
        {
            var (model, service) = CreateModel();
            Format? saved = null;
            service.Setup(s => s.AddAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>()))
                .Callback<Format, CancellationToken>((f, _) => saved = f)
                .ReturnsAsync(FormatSaveResult.Success());
            model.Input = new FormatInputModel
            {
                EndDate = new DateOnly(2024, 6, 1),
                IsOngoing = true,
                Name = "Alpha Era",
                StartDate = new DateOnly(2024, 1, 1)
            };

            await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsNull(saved!.EndDate);
        }

        [TestMethod]
        public async Task OnPostSaveAsync_ServiceRejects_ReturnsPageWithErrorsAndReopensModal()
        {
            var (model, service) = CreateModel(BuildFormat(1, "Existing", new DateOnly(2024, 1, 1), null));
            service.Setup(s => s.AddAsync(It.IsAny<Format>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FormatSaveResult.Failure(["Dates overlap with \"Existing\"."]));
            model.Input = new FormatInputModel { Name = "Clashing", StartDate = new DateOnly(2024, 2, 1) };

            var result = await model.OnPostSaveAsync(CancellationToken.None);

            Assert.IsInstanceOfType<PageResult>(result);
            Assert.AreEqual(1, model.Errors.Count);
            Assert.IsTrue(model.ShowFormModal);
            Assert.AreEqual(1, model.Formats.Count);
            Assert.AreEqual("Clashing", model.Input.Name);
        }

        private static Format BuildFormat(int id, string name, DateOnly start, DateOnly? end) =>
            new() { EndDate = end, ID = id, Name = name, StartDate = start };

        private static (FormatsModel Model, Mock<IFormatService> Service) CreateModel(params Format[] formats)
        {
            var service = new Mock<IFormatService>();
            service.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(formats);

            var model = new FormatsModel(service.Object);
            PageContextFactory.Attach(model);
            return (model, service);
        }
    }
}
