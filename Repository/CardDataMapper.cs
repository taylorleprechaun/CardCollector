using CardCollector.DTO;
using CardCollector.DTO.YamlYugi;

namespace CardCollector.Repository
{
    /// <summary>
    /// Pure helpers the catalog uses to shape card data: set prefixes and the set-name index, card type and race from
    /// yaml-yugi fields, Speed Duel set detection, and the fallback image URL for a passcode.
    /// </summary>
    public static class CardDataMapper
    {
        public static Image BuildFallbackImage(int cardID) => new()
        {
            ID = cardID,
            ImageURL = $"https://images.ygoprodeck.com/images/cards/{cardID}.jpg",
            ImageURLSmall = $"https://images.ygoprodeck.com/images/cards_small/{cardID}.jpg"
        };

        public static (IReadOnlyDictionary<string, string> namesByCode, IReadOnlyDictionary<string, string> prefixByName) BuildSetNameIndex(IReadOnlyList<Card> cards)
        {
            // Step 1: prefix → shortest canonical name (shorter names are the base set, not promo variants)
            var prefixToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
                foreach (var set in card.CardSets ?? [])
                    if (!string.IsNullOrEmpty(set.Code) && !string.IsNullOrEmpty(set.Name))
                    {
                        var prefix = GetSetPrefix(set.Code!);
                        if (!prefixToName.TryGetValue(prefix, out var existing) || set.Name!.Length < existing.Length)
                            prefixToName[prefix] = set.Name!;
                    }

            // Step 2: full card code → canonical name
            var namesByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in cards)
                foreach (var set in card.CardSets ?? [])
                    if (!string.IsNullOrEmpty(set.Code) && prefixToName.TryGetValue(GetSetPrefix(set.Code!), out var canonicalName))
                        namesByCode.TryAdd(set.Code!, canonicalName);

            // Step 3: canonical name → prefix (reverse of step 1)
            var prefixByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (prefix, name) in prefixToName)
                prefixByName.TryAdd(name, prefix);

            return (namesByCode, prefixByName);
        }

        public static string DeriveCardType(YamlCard y) => y.CardType switch
        {
            "Spell" => "Spell Card",
            "Trap" => "Trap Card",
            _ => DeriveMonsterType(y.MonsterTypeLine)
        };

        public static string DeriveMonsterType(string? typeLine)
        {
            if (typeLine is null) return "Normal Monster";
            if (typeLine.Contains("Fusion")) return "Fusion Monster";
            if (typeLine.Contains("Synchro")) return "Synchro Monster";
            if (typeLine.Contains("Xyz")) return "Xyz Monster";
            if (typeLine.Contains("Link")) return "Link Monster";
            if (typeLine.Contains("Ritual")) return "Ritual Monster";
            if (typeLine.Contains("Effect")) return "Effect Monster";
            return "Normal Monster";
        }

        public static string? DeriveRace(YamlCard y)
        {
            if (y.MonsterTypeLine is not null)
                return y.MonsterTypeLine.Split(" / ").First();
            return y.Property;
        }

        public static string GetSetPrefix(string code)
        {
            var hyphen = code.IndexOf('-');
            return hyphen > 0 ? code[..hyphen] : code;
        }

        public static bool IsSpeedDuelSet(string? setName) =>
            setName?.Contains("Speed Duel", StringComparison.OrdinalIgnoreCase) == true;
    }
}
