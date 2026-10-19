using System.Globalization;

namespace SoTF.Editor.Data.Helpers;

public static class Formatter
{
    // One Date Format Everywhere, Sortable And Language Neutral
    private const string DisplayDateTimeFormat = "yyyy-MM-dd HH:mm";
    private const string DisplayDateTimeSecondsFormat = "yyyy-MM-dd HH:mm:ss";

    public static string FormatDateTime(DateTime value)
    {
        return value.ToString(DisplayDateTimeFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatSize(long bytes)
    {
        return $"{bytes / 1048576.0:0.0} MB";
    }

    public static string FormatDateTimeWithSeconds(DateTime value)
    {
        // Backups Taken In The Same Minute Stay Apart
        return value.ToString(DisplayDateTimeSecondsFormat, CultureInfo.InvariantCulture);
    }
}
