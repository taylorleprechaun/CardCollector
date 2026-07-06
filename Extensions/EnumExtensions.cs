using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CardCollector.Data.Models;

namespace CardCollector.Extensions
{
    public static class EnumExtensions
    {
        private static readonly ConcurrentDictionary<(Type, string), string> _cache = new();

        public static string GetBadgeClass(this CollectionCompletionStatus status) => status switch
        {
            CollectionCompletionStatus.Complete => "bg-success",
            CollectionCompletionStatus.Incomplete => "bg-warning text-dark",
            CollectionCompletionStatus.Owned => "bg-info",
            _ => "bg-secondary"
        };

        public static string GetBadgeClass(this CollectionStatus status) => status switch
        {
            CollectionStatus.Ordered => "bg-primary",
            CollectionStatus.Owned => "bg-info",
            _ => "bg-secondary"
        };

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
