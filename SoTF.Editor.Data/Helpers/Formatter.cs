using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SoTF.Editor.Data.Helpers;

public static partial class Formatter
{
    static Formatter()
    {
        Debug.Assert(SelfTestPasses());
    }

    // One Date Format Everywhere, Day First
    private const string DisplayDateTimeFormat = "dd-MM-yyyy HH:mm";
    private const string DisplayDateTimeSecondsFormat = "dd-MM-yyyy HH:mm:ss";

    // Keeps An Axis Letter With Its Number
    private const char NoBreakSpace = '\u00A0';

    public static bool Matches(string text, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    public static (string Place, string Name) ReadableId(string id)
    {
        // CaveA.BoardsA Reads Cave A, Then Boards A
        var parts = id.Split('.').Select(part => WordBreak().Replace(part, " ")).ToArray();
        return parts.Length > 1 ? (parts[0], string.Join(" · ", parts[1..])) : ("", parts[0]);
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=[0-9])")]
    private static partial Regex WordBreak();

    private static bool SelfTestPasses()
    {
        return ReadableId("CaveA.BoardsA") == ("Cave A", "Boards A")
            && ReadableId("BunkerEntertainment.AmmoCase.AAD")
                == ("Bunker Entertainment", "Ammo Case · AAD")
            && ReadableId("LightningPOI.12") == ("Lightning POI", "12")
            && ReadableId("RaftTacticalFound") == ("", "Raft Tactical Found");
    }

    public static string FormatDateTime(DateTime value)
    {
        return value.ToString(DisplayDateTimeFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatDateTimeWithSeconds(DateTime value)
    {
        // Backups Taken In The Same Minute Stay Apart
        return value.ToString(DisplayDateTimeSecondsFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatPosition(float x, float? y, float z)
    {
        // Dots Between Axes, A Line Only Breaks Between Them
        return JoinParts(
            $"X{NoBreakSpace}{x:F2}",
            y is { } height ? $"Y{NoBreakSpace}{height:F2}" : null,
            $"Z{NoBreakSpace}{z:F2}"
        );
    }

    public static string FormatSize(long bytes)
    {
        return $"{bytes / 1048576.0:0.0} MB";
    }

    public static string FormatDistance(float metres)
    {
        // Whole Metres In The Windows Number Format
        return $"{metres:N0} m";
    }

    public static string JoinParts(params string?[] parts)
    {
        // A Missing Part Leaves No Separator And No Dash
        return string.Join(" · ", parts.Where(part => part is { Length: > 0 } and not "-"));
    }

    public static string Normalize(string text)
    {
        // Accents Dropped, So Searches Match Either Spelling
        return string.Concat(
            text.Normalize(NormalizationForm.FormD)
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                )
        );
    }
}
