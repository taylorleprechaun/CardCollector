using CardCollector.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.ViewModels
{
    [TestClass]
    public sealed class WishlistItemViewModelTests
    {
        [TestMethod]
        public void IsInCart_CartQuantityIsPositive_ReturnsTrue()
        {
            var item = WishlistItemViewModel.From(new CardPrinting(), cartQuantity: 1);

            Assert.IsTrue(item.IsInCart);
        }

        [TestMethod]
        public void IsInCart_CartQuantityIsZero_ReturnsFalse()
        {
            var item = WishlistItemViewModel.From(new CardPrinting(), cartQuantity: 0);

            Assert.IsFalse(item.IsInCart);
        }

        [TestMethod]
        public void IsOrdered_OrderedQuantityIsPositive_ReturnsTrue()
        {
            var item = WishlistItemViewModel.From(new CardPrinting(), orderedQuantity: 1);

            Assert.IsTrue(item.IsOrdered);
        }

        [TestMethod]
        public void IsOrdered_OrderedQuantityIsZero_ReturnsFalse()
        {
            var item = WishlistItemViewModel.From(new CardPrinting(), orderedQuantity: 0);

            Assert.IsFalse(item.IsOrdered);
        }

        [TestMethod]
        [DataRow(0, 0, 0, 3, DisplayName = "Nothing owned, ordered, or in cart needs full threshold")]
        [DataRow(1, 1, 0, 1, DisplayName = "Partial progress reduces quantity needed")]
        [DataRow(2, 1, 0, 0, DisplayName = "Owned plus ordered meets threshold exactly")]
        [DataRow(3, 2, 2, 0, DisplayName = "Overshooting threshold clamps to zero instead of going negative")]
        public void QuantityNeeded_ClampsAtZeroAndNeverNegative(int quantityOwned, int orderedQuantity, int cartQuantity, int expected)
        {
            var item = WishlistItemViewModel.From(new CardPrinting(), quantityOwned, cartQuantity, orderedQuantity);

            Assert.AreEqual(expected, item.QuantityNeeded);
        }
    }
}
