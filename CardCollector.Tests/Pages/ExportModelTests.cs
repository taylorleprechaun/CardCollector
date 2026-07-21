using System.Text;
using CardCollector.Data.Models;
using CardCollector.Pages;
using CardCollector.Services;
using CardCollector.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CardCollector.Tests.Pages
{
    [TestClass]
    public sealed class ExportModelTests
    {
        private Mock<ICardService> _cardServiceMock = null!;
        private ExportModel _page = null!;

        [TestMethod]
        public async Task OnGetCollectionAsync_AllOptionalEntryFieldsPopulated_IncludesThemInRow()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOwnedAsync()).ReturnsAsync(
            [
                new OrderEntryViewModel
                {
                    CardName = "Dark Magician", SetCode = "LOB-EN001", SetName = "Set", RarityName = "Ultra Rare",
                    Condition = CardCondition.NearMint, Edition = CardEdition.FirstEdition, AcquisitionMethod = AcquisitionMethod.Purchased,
                    PurchasePrice = 9.99m, MarketPriceAtEntry = 12.50m, PurchaseDate = new DateTime(2026, 1, 15),
                    DateCreated = new DateTime(2026, 1, 16)
                }
            ]);

            var result = await _page.OnGetCollectionAsync() as FileContentResult;

            var csv = Encoding.UTF8.GetString(result!.FileContents);
            StringAssert.Contains(csv, "9.99,12.50,2026-01-15,2026-01-16");
        }

        [TestMethod]
        public async Task OnGetCollectionAsync_BuildsCsvWithHeaderAndRowPerEntry()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOwnedAsync()).ReturnsAsync(
            [
                new OrderEntryViewModel
                {
                    CardName = "Dark Magician", SetCode = "LOB-EN001", SetName = "Legend of Blue Eyes White Dragon",
                    RarityName = "Ultra Rare", Quantity = 2, DateCreated = new DateTime(2026, 1, 1)
                }
            ]);

            var result = await _page.OnGetCollectionAsync() as FileContentResult;

            var csv = Encoding.UTF8.GetString(result!.FileContents);
            StringAssert.Contains(csv, "Card Name,Set Code,Set Name,Rarity");
            StringAssert.Contains(csv, "Dark Magician,LOB-EN001,Legend of Blue Eyes White Dragon,Ultra Rare");
        }

        [TestMethod]
        public async Task OnGetCollectionAsync_CardNameContainsComma_IsQuoted()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOwnedAsync()).ReturnsAsync(
            [
                new OrderEntryViewModel { CardName = "Card, With Comma", SetCode = "LOB-EN001", SetName = "Set", RarityName = "Common", DateCreated = DateTime.UtcNow }
            ]);

            var result = await _page.OnGetCollectionAsync() as FileContentResult;

            var csv = Encoding.UTF8.GetString(result!.FileContents);
            StringAssert.Contains(csv, "\"Card, With Comma\"");
        }

        [TestMethod]
        public async Task OnGetCollectionAsync_RarityNameIsBlank_FallsBackToRarityCode()
        {
            _cardServiceMock.Setup(s => s.GetEnrichedOwnedAsync()).ReturnsAsync(
            [
                new OrderEntryViewModel { CardName = "Dark Magician", SetCode = "LOB-EN001", SetName = "Set", RarityName = "", RarityCode = "(UR)", DateCreated = DateTime.UtcNow }
            ]);

            var result = await _page.OnGetCollectionAsync() as FileContentResult;

            var csv = Encoding.UTF8.GetString(result!.FileContents);
            StringAssert.Contains(csv, "(UR)");
        }

        [TestMethod]
        public async Task OnGetWishlistAsync_BuildsCsvWithHeaderAndRowPerItem()
        {
            _cardServiceMock.Setup(s => s.GetWishlistAsync()).ReturnsAsync(
            [
                new WishlistItemViewModel { CardName = "Blue-Eyes White Dragon", SetCode = "LOB-EN001", SetName = "Legend", RarityName = "Ultra Rare", Price = 25.5m }
            ]);

            var result = await _page.OnGetWishlistAsync() as FileContentResult;

            var csv = Encoding.UTF8.GetString(result!.FileContents);
            StringAssert.Contains(csv, "Card Name,Set Code,Set Name,Rarity,Market Price");
            StringAssert.Contains(csv, "Blue-Eyes White Dragon,LOB-EN001,Legend,Ultra Rare,25.50");
        }

        [TestMethod]
        public async Task OnGetWishlistAsync_PriceIsNull_OmitsPriceInRow()
        {
            _cardServiceMock.Setup(s => s.GetWishlistAsync()).ReturnsAsync(
            [
                new WishlistItemViewModel { CardName = "Blue-Eyes White Dragon", SetCode = "LOB-EN001", SetName = "Legend", RarityName = "Ultra Rare", Price = null }
            ]);

            var result = await _page.OnGetWishlistAsync() as FileContentResult;

            var csv = Encoding.UTF8.GetString(result!.FileContents);
            StringAssert.Contains(csv, $"Blue-Eyes White Dragon,LOB-EN001,Legend,Ultra Rare,{Environment.NewLine}");
        }

        [TestMethod]
        public void OnGet_RedirectsToCollectionPage()
        {
            var result = _page.OnGet() as RedirectToPageResult;

            Assert.AreEqual("/Collection", result!.PageName);
        }

        [TestInitialize]
        public void Setup()
        {
            _cardServiceMock = new Mock<ICardService>();
            _page = new ExportModel(_cardServiceMock.Object);
        }
    }
}
