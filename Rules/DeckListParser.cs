using CardCollector.Models;
using System.Buffers.Binary;
using System.Globalization;

namespace CardCollector.Rules
{
    /// <summary>Pure parser for a DuelingBook YDKe code or YDK file.</summary>
    public static class DeckListParser
    {
        public const int MAX_CARDS = 200;
        public const int MAX_INPUT_LENGTH = 20_000;

        private const int MAX_ECHOED_LINE_LENGTH = 30;
        private const int PASSCODE_BYTES = 4;
        private const string YDKE_SCHEME = "ydke://";
        private const int YDKE_SECTION_COUNT = 3;

        /// <summary>
        /// Detects the format (a <c>ydke://</c> code, or a YDK file with a <c>#main</c> section) and returns the
        /// passcodes per section. Anything else, an empty main deck, or an oversized paste is a failure.
        /// </summary>
        public static DeckListParseResult Parse(string? text)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return DeckListParseResult.Failure("Paste a YDKe code or the contents of a YDK file.");

            if (trimmed.Length > MAX_INPUT_LENGTH)
                return DeckListParseResult.Failure("That is too long to be a deck list.");

            if (trimmed.StartsWith(YDKE_SCHEME, StringComparison.OrdinalIgnoreCase))
                return ParseYDKe(trimmed[YDKE_SCHEME.Length..]);

            if (trimmed.Contains("#main", StringComparison.OrdinalIgnoreCase))
                return ParseYDK(trimmed);

            return DeckListParseResult.Failure("That doesn't look like a YDKe code or a YDK file. A YDKe code starts with ydke:// and a YDK file has a #main section.");
        }

        private static DeckListParseResult Complete(List<int> main, List<int> extra, List<int> side, DeckListFormat format)
        {
            if (main.Count == 0)
                return DeckListParseResult.Failure("The main deck is empty.");

            var total = main.Count + extra.Count + side.Count;
            if (total > MAX_CARDS)
                return DeckListParseResult.Failure($"That deck has {total} cards; the limit is {MAX_CARDS}.");

            return DeckListParseResult.Success(new ParsedDeckList
            {
                Extra = extra,
                Format = format,
                Main = main,
                Side = side
            });
        }

        private static (List<int>? Passcodes, string? Error) DecodeSection(string section, string sectionName)
        {
            // Base64 from a clipboard picks up line breaks; some tools also use the URL-safe alphabet or drop padding.
            var cleaned = string.Concat(section.Where(c => !char.IsWhiteSpace(c)))
                .Replace('-', '+')
                .Replace('_', '/')
                .TrimEnd('=');
            cleaned = cleaned.PadRight(cleaned.Length + ((4 - (cleaned.Length % 4)) % 4), '=');

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(cleaned);
            }
            catch (FormatException)
            {
                return (null, $"The {sectionName} section of the YDKe code isn't valid.");
            }

            if (bytes.Length % PASSCODE_BYTES != 0)
                return (null, $"The {sectionName} section of the YDKe code is the wrong length.");

            var passcodes = new List<int>(bytes.Length / PASSCODE_BYTES);
            for (var offset = 0; offset < bytes.Length; offset += PASSCODE_BYTES)
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, PASSCODE_BYTES));
                if (value == 0 || value > int.MaxValue)
                    return (null, $"The {sectionName} section of the YDKe code has an invalid card number.");

                passcodes.Add((int)value);
            }

            return (passcodes, null);
        }

        private static string Echo(string line) =>
            line.Length <= MAX_ECHOED_LINE_LENGTH ? line : line[..MAX_ECHOED_LINE_LENGTH] + "…";

        private static DeckListParseResult ParseYDK(string text)
        {
            var main = new List<int>();
            var extra = new List<int>();
            var side = new List<int>();
            List<int>? current = null;

            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.Length == 0)
                    continue;

                var header = line.ToLowerInvariant() switch
                {
                    "#main" => main,
                    "#extra" => extra,
                    "!side" or "#side" => side,
                    _ => null
                };

                if (header is not null)
                {
                    current = header;
                    continue;
                }

                if (line[0] == '#')
                    continue;

                if (!int.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var passcode) || passcode <= 0)
                    return DeckListParseResult.Failure($"Line {index + 1} isn't a card number: \"{Echo(line)}\".");

                if (current is null)
                    return DeckListParseResult.Failure($"Line {index + 1} comes before the #main section.");

                current.Add(passcode);
            }

            return Complete(main, extra, side, DeckListFormat.Ydk);
        }

        private static DeckListParseResult ParseYDKe(string body)
        {
            // "main!extra!side!" splits into four parts with an empty last one; the trailing "!" is optional.
            var parts = body.Split('!');
            var hasExtraText = parts.Length > YDKE_SECTION_COUNT && parts.Skip(YDKE_SECTION_COUNT).Any(p => !string.IsNullOrWhiteSpace(p));
            if (parts.Length < YDKE_SECTION_COUNT || hasExtraText)
                return DeckListParseResult.Failure("A YDKe code has three sections (main, extra and side) separated by !.");

            string[] names = ["main", "extra", "side"];
            var sections = new List<int>[YDKE_SECTION_COUNT];
            for (var index = 0; index < YDKE_SECTION_COUNT; index++)
            {
                var (passcodes, error) = DecodeSection(parts[index], names[index]);
                if (passcodes is null)
                    return DeckListParseResult.Failure(error!);

                sections[index] = passcodes;
            }

            return Complete(sections[0], sections[1], sections[2], DeckListFormat.Ydke);
        }
    }
}
