using CardCollector.Data.Models;
using CardCollector.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class EnumExtensionsTests
    {
        private enum DescriptionOnlyEnum
        {
            [System.ComponentModel.Description("Described Value")]
            SomeValue
        }

        private enum NoAttributeEnum
        {
            SomeValue
        }

        [TestMethod]
        [DataRow(CollectionStatus.Ordered, "bg-primary", DisplayName = "Ordered")]
        [DataRow(CollectionStatus.Owned, "bg-info", DisplayName = "Owned")]
        public void GetBadgeClass_CollectionStatus_ReturnsExpectedClass(CollectionStatus status, string expected)
        {
            var result = status.GetBadgeClass();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(CollectionCompletionStatus.Complete, "bg-success", DisplayName = "Complete")]
        [DataRow(CollectionCompletionStatus.Incomplete, "bg-warning text-dark", DisplayName = "Incomplete")]
        [DataRow(CollectionCompletionStatus.Owned, "bg-info", DisplayName = "Owned")]
        [DataRow(CollectionCompletionStatus.Placeholder, "bg-secondary", DisplayName = "Placeholder falls back to default")]
        public void GetBadgeClass_CompletionStatus_ReturnsExpectedClass(CollectionCompletionStatus status, string expected)
        {
            var result = status.GetBadgeClass();

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetDisplayName_CalledTwiceForSameValue_ReturnsSameCachedResult()
        {
            var first = CardEdition.FirstEdition.GetDisplayName();
            var second = CardEdition.FirstEdition.GetDisplayName();

            Assert.AreEqual("1st Edition", first);
            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void GetDisplayName_MemberHasDisplayAttribute_ReturnsDisplayName()
        {
            var result = CardCondition.NearMint.GetDisplayName();

            Assert.AreEqual("Near Mint", result);
        }

        [TestMethod]
        public void GetDisplayName_MemberHasNoAttributes_FallsBackToEnumName()
        {
            var result = NoAttributeEnum.SomeValue.GetDisplayName();

            Assert.AreEqual("SomeValue", result);
        }

        [TestMethod]
        public void GetDisplayName_MemberHasOnlyDescriptionAttribute_ReturnsDescription()
        {
            var result = DescriptionOnlyEnum.SomeValue.GetDisplayName();

            Assert.AreEqual("Described Value", result);
        }
    }
}
