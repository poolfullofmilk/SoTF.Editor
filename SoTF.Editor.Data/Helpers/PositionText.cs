using System.Diagnostics;
using System.Globalization;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.Helpers;

public static class PositionText
{
    // The Shown Format Joins Each Axis Letter With One
    private const char NoBreakSpace = '\u00A0';

    private static readonly char[] s_separators = [.. " \t\r\n;·|", NoBreakSpace];

    private static readonly char[] s_digits = [.. "0123456789"];

    private static readonly char[] s_numberStarts = [.. "0123456789-+."];

    static PositionText()
    {
        Debug.Assert(SelfTestPasses());
    }

    public static string Format(Position position)
    {
        // Invariant Numbers, The Area Mask Last Underground
        var numbers = string.Create(
            CultureInfo.InvariantCulture,
            $"{position.X:0.##} {position.Y:0.##} {position.Z:0.##}"
        );
        return position.Area.IsUnderground()
            ? string.Create(CultureInfo.InvariantCulture, $"{numbers} {position.Area.AreaMask}")
            : numbers;
    }

    public static Position? Parse(string? text, AreaMaskManager areas)
    {
        // Three Numbers Are Surface, A Fourth Names The Area
        var numbers = new List<float>();
        foreach (
            var token in (text ?? "").Split(s_separators, StringSplitOptions.RemoveEmptyEntries)
        )
        {
            foreach (var number in NumbersOf(token))
            {
                if (
                    !float.TryParse(
                        number,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var value
                    ) || !float.IsFinite(value)
                )
                {
                    return null;
                }

                numbers.Add(value);
            }
        }

        return numbers switch
        {
            [var x, var y, var z] => new Position(x, y, z),
            [var x, var y, var z, var mask]
                when mask >= 0
                    && mask == MathF.Floor(mask)
                    && areas.KnownArea((int)mask) is { } area => new Position(x, y, z)
            {
                Area = area,
            },
            _ => null,
        };
    }

    private static string[] NumbersOf(string token)
    {
        // A Label Before The Number Goes, Like X Or Y=
        var start = token.IndexOfAny(s_numberStarts);
        var end = token.LastIndexOfAny(s_digits);
        if (start < 0 || end < start)
        {
            return [];
        }

        // A Lone Comma Without A Dot Is Decimal
        var body = token[start..(end + 1)];
        var commas = body.Count(letter => letter == ',');
        return commas switch
        {
            0 => [body],
            1 when !body.Contains('.') => [body.Replace(',', '.')],
            _ => body.Split(',', StringSplitOptions.RemoveEmptyEntries),
        };
    }

    private static bool SelfTestPasses()
    {
        var caveD = new Area("Cave D", 16, 2048);
        var areas = new AreaMaskManager([AreaMaskManager.Surface, caveD]);
        var inCave = new Position(-64.12f, 152.96f, -374.81f) { Area = caveD };
        var onSurface = new Position(-910.58f, 88.22f, 856.4f);

        return Format(inCave) == "-64.12 152.96 -374.81 16"
            && Format(onSurface) == "-910.58 88.22 856.4"
            && inCave.Equals(Parse(Format(inCave), areas))
            && onSurface.Equals(Parse(Format(onSurface), areas))
            && inCave.Equals(Parse(inCave.PrintableShort + " · 16", areas))
            && inCave.Equals(Parse("X -64,12 · Y 152,96 · Z -374,81 · 16", areas))
            && onSurface.Equals(Parse("-910.58, 88.22, 856.4", areas))
            && onSurface.Equals(Parse("-910.58,88.22,856.4", areas))
            && onSurface.Equals(Parse("x=-910.58; y=88.22; z=856.4", areas))
            && Parse("-64.12 152.96", areas) is null
            && Parse("-64.12 152.96 -374.81 17", areas) is null
            && Parse("-64.12 152.96 -374.81 16 3", areas) is null
            && Parse("1e99 2 3", areas) is null
            && Parse("Cave D", areas) is null
            && Parse(null, areas) is null;
    }
}
