using System.Reflection;
using System.Runtime.Serialization;

namespace CardCollector.DTO
{
    public static class RarityExtensions
    {
        private static readonly IReadOnlyDictionary<string, Rarity> _map = BuildMap();

        // Some providers label plain Common cards as "Short Print"/"Super Short Print" instead.
        // Program.cs's startup migration calls NormalizeRarityName directly to fix historical data,
        // so this is the single place to add a newly discovered bad variant string.
        private static readonly IReadOnlySet<string> _shortPrintVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Short Print",
            "Super Short Print"
        };

        public static string? GetRarityCode(string? rarityName) => rarityName switch
        {
            "Common" => "(C)",
            "Rare" => "(R)",
            "Super Rare" => "(SR)",
            "Ultra Rare" => "(UR)",
            "Secret Rare" => "(ScR)",
            "Ultimate Rare" => "(UtR)",
            "Gold Rare" => "(GUR)",
            "Ghost Rare" => "(GHR)",
            "Ghost/Gold Rare" => "(GGR)",
            "Grand Master Rare" => "(GMR)",
            "Starlight Rare" => "(StR)",
            "Collector's Rare" => "(CR)",
            "Prismatic Collector's Rare" => "(PCR)",
            "Prismatic Secret Rare" => "(PScR)",
            "Prismatic Ultimate Rare" => "(PUR)",
            "Quarter Century Secret Rare" => "(QCSCR)",
            "Platinum Secret Rare" => "(PlScR)",
            "Platinum Rare" => "(PR)",
            "Short Print" => "(SP)",
            "Super Short Print" => "(SSP)",
            "Normal Parallel Rare" => "(NPR)",
            "Parallel Rare" => "(ParR)",
            "Super Parallel Rare" => "(SPR)",
            "Ultra Parallel Rare" => "(UPR)",
            "10000 Secret Rare" => "(10000ScR)",
            "Extra Secret Rare" => "(EScR)",
            "Gold Secret Rare" => "(GScR)",
            "Mosaic Rare" => "(MSR)",
            "Premium Gold Rare" => "(PGR)",
            "Shatterfoil Rare" => "(SHR)",
            "Starfoil Rare" => "(SFR)",
            "Ultra Secret Rare" => "(UScR)",
            "Secret Rare Pharaoh's Rare" => "(SCR-PhaR)",
            "Ultra Rare Pharaoh's Rare" => "(UR-PhaR)",
            "Duel Terminal Normal Parallel Rare" => "(DTNPR)",
            "Duel Terminal Normal Rare Parallel Rare" => "(DTNRPR)",
            "Duel Terminal Rare Parallel Rare" => "(DTRPR)",
            "Duel Terminal Super Parallel Rare" => "(DTSPR)",
            "Duel Terminal Ultra Parallel Rare" => "(DTUPR)",
            "Duel Terminal Technology Common" => "(DTTC)",
            "Duel Terminal Technology Ultra Rare" => "(DTTUR)",
            "Emblazoned Secret Rare" => "(EmScR)",
            "Emblazoned Ultra Rare" => "(EmUR)",
            "Secret Pharaoh’s Rare" => "(SCR-PhaR)",
            "Ultra Pharaoh’s Rare" => "(UR-PhaR)",
            _ => null
        };

        public static string? NormalizeRarityName(string? rarityName) =>
            rarityName is not null && _shortPrintVariants.Contains(rarityName) ? "Common" : rarityName;

        public static Rarity ParseRarity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Rarity.Error;

            return _map.TryGetValue(value, out var rarity) ? rarity : Rarity.Error;
        }

        private static IReadOnlyDictionary<string, Rarity> BuildMap()
        {
            var map = new Dictionary<string, Rarity>(StringComparer.OrdinalIgnoreCase);

            // Enumerate fields directly rather than Enum.GetValues()+ToString(): aliases sharing another
            // member's value (e.g. Cr = CollectorsRare) would have their own EnumMemberAttribute silently
            // skipped otherwise, since ToString() on a shared value always resolves to one declared name.
            foreach (var field in typeof(Rarity).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var enumMember = field.GetCustomAttribute<EnumMemberAttribute>();
                if (enumMember?.Value is not null)
                    map.TryAdd(enumMember.Value, (Rarity)field.GetValue(null)!);
            }

            return map;
        }
    }
}
