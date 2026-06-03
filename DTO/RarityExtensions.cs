using System.Reflection;
using System.Runtime.Serialization;

namespace CardCollector.DTO
{
    public static class RarityExtensions
    {
        private static readonly IReadOnlyDictionary<string, Rarity> _map = BuildMap();

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
            "Starlight Rare" => "(StR)",
            "Collector's Rare" => "(CR)",
            "Prismatic Secret Rare" => "(PScR)",
            "Quarter Century Secret Rare" => "(QCSCR)",
            "Platinum Secret Rare" => "(PlScR)",
            "Platinum Rare" => "(PR)",
            "Short Print" => "(SP)",
            "Super Short Print" => "(SSP)",
            "Normal Parallel Rare" => "(NPR)",
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
            _ => null
        };

        public static Rarity ParseRarity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Rarity.Error;

            return _map.TryGetValue(value, out var rarity) ? rarity : Rarity.Error;
        }

        private static IReadOnlyDictionary<string, Rarity> BuildMap()
        {
            var map = new Dictionary<string, Rarity>(StringComparer.OrdinalIgnoreCase);

            foreach (Rarity rarity in Enum.GetValues<Rarity>())
            {
                var member = typeof(Rarity).GetMember(rarity.ToString()).FirstOrDefault();
                if (member is null)
                    continue;

                var enumMember = member.GetCustomAttribute<EnumMemberAttribute>();
                if (enumMember?.Value is not null)
                    map.TryAdd(enumMember.Value, rarity);
            }

            return map;
        }
    }
}
