using System.Globalization;

namespace SoTF.Editor.Data.Helpers;

public static class Formatter
{
    // One Date Format Everywhere, Sortable And Language Neutral
    private const string DisplayDateTimeFormat = "yyyy-MM-dd HH:mm";

    public static string FormatDateTime(DateTime value)
    {
        return value.ToString(DisplayDateTimeFormat, CultureInfo.InvariantCulture);
    }
}
