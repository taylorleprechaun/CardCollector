namespace CardCollector.Data.Models.Catalog
{
    /// <summary>Reads and edits the comma-separated locked-field lists stored on catalog cards and printings.</summary>
    public static class CatalogLockedFields
    {
        /// <summary>Returns the list with <paramref name="fieldName"/> added, unchanged if it is already present.</summary>
        public static string Add(string? lockedFields, string fieldName)
        {
            if (Contains(lockedFields, fieldName))
                return lockedFields!;

            return string.IsNullOrWhiteSpace(lockedFields) ? fieldName : $"{lockedFields},{fieldName}";
        }

        public static bool Contains(string? lockedFields, string fieldName) =>
            !string.IsNullOrWhiteSpace(lockedFields)
            && Split(lockedFields).Contains(fieldName, StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns the list without <paramref name="fieldName"/>; null once nothing is left locked.</summary>
        public static string? Remove(string? lockedFields, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(lockedFields))
                return null;

            var remaining = Split(lockedFields).Where(f => !string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase)).ToList();
            return remaining.Count == 0 ? null : string.Join(',', remaining);
        }

        /// <summary>The locked field names, in the order they were locked.</summary>
        public static IReadOnlyList<string> Split(string? lockedFields) =>
            string.IsNullOrWhiteSpace(lockedFields)
                ? []
                : lockedFields.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
