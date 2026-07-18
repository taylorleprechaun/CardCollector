namespace CardCollector.Extensions;

public static class SetCodeExtensions
{
    public static string ToTCGPlayerSetCode(this string setCode)
    {
        var hyphenIndex = setCode.IndexOf('-');
        return hyphenIndex < 0 ? setCode : setCode[..hyphenIndex];
    }
}
