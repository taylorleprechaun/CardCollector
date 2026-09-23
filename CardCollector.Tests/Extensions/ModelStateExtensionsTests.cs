using CardCollector.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CardCollector.Tests.Extensions
{
    [TestClass]
    public sealed class ModelStateExtensionsTests
    {
        [TestMethod]
        public void IsFieldInvalid_FieldHasBindingError_ReturnsTrue()
        {
            var modelState = new ModelStateDictionary();
            modelState.SetModelValue("Input.Finish", "abc", "abc");
            modelState.AddModelError("Input.Finish", "The value 'abc' is not valid.");

            var result = modelState.IsFieldInvalid("Input", "Finish");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFieldInvalid_FieldBoundCleanly_ReturnsFalse()
        {
            var modelState = new ModelStateDictionary();
            modelState.SetModelValue("Input.Finish", "3", "3");
            modelState.MarkFieldValid("Input.Finish");

            var result = modelState.IsFieldInvalid("Input", "Finish");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsFieldInvalid_OtherFieldHasError_ReturnsFalse()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Input.Players", "The value 'abc' is not valid.");

            var result = modelState.IsFieldInvalid("Input", "Finish");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsFieldInvalid_NullModelState_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => ((ModelStateDictionary)null!).IsFieldInvalid("Input", "Finish"));
        }
    }
}
