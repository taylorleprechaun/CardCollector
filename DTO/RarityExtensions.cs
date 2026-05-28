using System.Reflection;
using System.Runtime.Serialization;

namespace CardCollector.DTO
{
    public static class RarityExtensions
    {
        private static readonly Dictionary<string, Rarity> _map = BuildMap();

        public static Rarity ParseRarity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Rarity.Error;

            return _map.TryGetValue(value, out var rarity) ? rarity : Rarity.Error;
        }

        private static Dictionary<string, Rarity> BuildMap()
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
