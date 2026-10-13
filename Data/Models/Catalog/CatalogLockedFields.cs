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
            && lockedFields.Split(',', StringSplitOptions.TrimEntries).Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }
}
