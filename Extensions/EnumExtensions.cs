using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CardCollector.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            if (member is null)
                return value.ToString();

            var display = member.GetCustomAttribute<DisplayAttribute>();
            if (display?.Name is not null)
                return display.Name;

            var description = member.GetCustomAttribute<DescriptionAttribute>();
            if (description?.Description is not null)
                return description.Description;

            return value.ToString();
        }
    }
}
