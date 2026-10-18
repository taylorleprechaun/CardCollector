using CardCollector.Data.Models.Catalog;
using CardCollector.ViewModels;

namespace CardCollector.Rules.Catalog
{
    /// <summary>Applies a hand edit to a catalog card and locks every field it changed, so the nightly sync leaves them alone.</summary>
    /// <remarks>
    /// Text is compared after trimming and normalizing line breaks: a textarea posts "\r\n", and treating that as an edit
    /// would lock a field the user never touched.
    /// </remarks>
    public static class CatalogCardEditor
    {
        /// <summary>Copies the edited values onto <paramref name="card"/> and returns the names of the fields that changed.</summary>
        public static IReadOnlyList<string> Apply(CatalogCard card, CatalogCardEditInput input, DateTime nowUTC)
        {
            if (card is null) throw new ArgumentNullException(nameof(card));
            if (input is null) throw new ArgumentNullException(nameof(input));

            var changed = new List<string>();

            void SetText(string field, string? current, string? edited, Action<string?> assign)
            {
                var cleaned = Clean(edited);
                if (string.Equals(Clean(current), cleaned, StringComparison.Ordinal))
                    return;

                assign(cleaned);
                changed.Add(field);
            }

            void SetNumber(string field, int? current, int? edited, Action<int?> assign)
            {
                if (current == edited)
                    return;

                assign(edited);
                changed.Add(field);
            }

            SetText(CatalogCardField.ARCHETYPE, card.Archetype, input.Archetype, v => card.Archetype = v);
            SetNumber(CatalogCardField.ATK, card.ATK, input.ATK, v => card.ATK = v);
            SetText(CatalogCardField.ATTRIBUTE, card.Attribute, input.Attribute, v => card.Attribute = v);
            SetText(CatalogCardField.CARD_TYPE, card.CardType, input.CardType, v => card.CardType = v);
            SetNumber(CatalogCardField.DEF, card.DEF, input.DEF, v => card.DEF = v);
            SetText(CatalogCardField.DESCRIPTION, card.Description, input.Description, v => card.Description = v);
            SetNumber(CatalogCardField.LEVEL, card.Level, input.Level, v => card.Level = v);
            SetText(CatalogCardField.LINK_MARKERS, card.LinkMarkers, input.LinkMarkers, v => card.LinkMarkers = v);
            SetNumber(CatalogCardField.LINK_RATING, card.LinkRating, input.LinkRating, v => card.LinkRating = v);
            SetText(CatalogCardField.NAME, card.Name, input.Name, v => card.Name = v ?? card.Name);
            SetNumber(CatalogCardField.PASSCODE, card.Passcode, input.Passcode, v => card.Passcode = v);
            SetText(CatalogCardField.TYPE, card.Type, input.Type, v => card.Type = v);
            SetText(CatalogCardField.TYPE_LINE, card.TypeLine, input.TypeLine, v => card.TypeLine = v);

            foreach (var field in changed)
                card.LockedFields = CatalogLockedFields.Add(card.LockedFields, field);
            if (changed.Count > 0)
                card.DateModified = nowUTC;

            return changed;
        }

        /// <summary>Returns what is wrong with the edit; empty when it can be saved.</summary>
        public static IReadOnlyList<string> Validate(CatalogCardEditInput input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            var errors = new List<string>();
            if (Clean(input.Name) is null)
                errors.Add("The card needs a name.");
            if (input.Passcode is <= 0)
                errors.Add("A passcode must be a positive number; leave it empty for none.");
            if (input.ATK is < 0 || input.DEF is < 0)
                errors.Add("ATK and DEF can't be negative; leave them empty for \"?\" or none.");
            if (input.Level is < 0 || input.LinkRating is < 0)
                errors.Add("Level and link rating can't be negative.");

            return errors;
        }

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
