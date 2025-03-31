using System.Globalization;

namespace VirtoCommerce.XCatalog.Core.Extensions;
public static class FormatExtensions
{
    public static string FormatDecimal(this decimal value, string cultureName)
    {
        var cultureInfo = GetCultureInfo(cultureName) ?? CultureInfo.InvariantCulture;
        var stringValue = value.ToString("N29", cultureInfo).TrimEnd(['0']).TrimEnd(['.', ',']);
        return stringValue;
    }


    private static CultureInfo GetCultureInfo(string languageCode)
    {
        try
        {
            return !string.IsNullOrEmpty(languageCode) ? CultureInfo.CreateSpecificCulture(languageCode) : null;
        }
        catch
        {
            return null;
        }
    }
}
