using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CardCollector.Extensions
{
    public static class ModelStateExtensions
    {
        /// <summary>True when the posted value for <c>prefix.field</c> couldn't be bound, such as text in a number box.</summary>
        public static bool IsFieldInvalid(this ModelStateDictionary modelState, string prefix, string field)
        {
            if (modelState is null) throw new ArgumentNullException(nameof(modelState));

            return modelState.GetFieldValidationState($"{prefix}.{field}") == ModelValidationState.Invalid;
        }
    }
}
