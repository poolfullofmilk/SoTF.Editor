using System.Diagnostics;

namespace SoTF.Editor.Data.Helpers;

public static class SeasonCalendar
{
    static SeasonCalendar()
    {
        Debug.Assert(SelfTestPasses());
    }

    public static double? DaysPerSeason(string seasonLength)
    {
        // Short Is Untested In Game, So It Gets No Picker
        return seasonLength switch
        {
            "Default" => 5,
            "Long" => 10,
            "Realistic" => 90,
            _ => null,
        };
    }

    public static int SeasonAt(double days, double offset, double daysPerSeason)
    {
        // Spring Is Zero, Then Summer, Autumn And Winter
        var season = (int)Math.Floor((days + offset) / daysPerSeason) % 4;
        return (season + 4) % 4;
    }

    public static double OffsetFor(int season, double days, double offset, double daysPerSeason)
    {
        // The Short Way Round, So Going Back Restores It
        var seasonsAhead = (season - SeasonAt(days, offset, daysPerSeason) + 4) % 4;
        if (seasonsAhead == 3)
        {
            seasonsAhead = -1;
        }

        return offset + (seasonsAhead * daysPerSeason);
    }

    public static int DaysLeft(int days, double offset, double daysPerSeason)
    {
        // Today Counts, So A Last Day Has One Left
        var intoSeason = ((days + offset) % daysPerSeason + daysPerSeason) % daysPerSeason;
        return (int)Math.Ceiling(daysPerSeason - intoSeason);
    }

    public static double OffsetForDaysLeft(
        int daysLeft,
        int days,
        double offset,
        double daysPerSeason
    )
    {
        // The Same Season, Ending After That Many Days
        var seasonEnd = (Math.Floor((days + offset) / daysPerSeason) + 1) * daysPerSeason;
        return seasonEnd - daysLeft - days;
    }

    private static bool SelfTestPasses()
    {
        // Tested In Game: Day 36 Offset 5 Spring, 15 Autumn
        return SeasonAt(36.4, 5, 5) == 0
            && SeasonAt(36.4, 15, 5) == 2
            && OffsetFor(3, 36.4, 5, 5) == 0
            && OffsetFor(0, 36.4, OffsetFor(1, 36.4, 5, 5), 5) == 5
            && SeasonAt(36.4, OffsetFor(1, 36.4, 15, 5), 5) == 1
            && SeasonAt(2, -8, 5) == 2
            && DaysLeft(36, 5, 5) == 4
            && DaysLeft(39, 5, 5) == 1
            && OffsetForDaysLeft(1, 36, 5, 5) == 8
            && SeasonAt(36, OffsetForDaysLeft(5, 36, 5, 5), 5) == SeasonAt(36, 5, 5)
            && DaysLeft(36, OffsetForDaysLeft(5, 36, 5, 5), 5) == 5
            && DaysLeft(2, -8, 5) == 1;
    }
}
