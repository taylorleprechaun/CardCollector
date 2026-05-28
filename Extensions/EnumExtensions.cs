using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CardCollector.Extensions
{
    public static class EnumExtensions
    {
        private static readonly ConcurrentDictionary<(Type, string), string> _cache = new();

        public static string GetDisplayName(this Enum value)
        {
            var key = (value.GetType(), value.ToString());
            return _cache.GetOrAdd(key, static k =>
            {
                var member = k.Item1.GetMember(k.Item2).FirstOrDefault();
                if (member is null)
                    return k.Item2;

                var display = member.GetCustomAttribute<DisplayAttribute>();
                if (display?.Name is not null)
                    return display.Name;

                var description = member.GetCustomAttribute<DescriptionAttribute>();
                return description?.Description ?? k.Item2;
            });
        }
    }
}
