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

    public static int DaysLeft(int days, double offset, double daysPerSeason)
    {
        // Today Counts, So A Last Day Has One Left
        var intoSeason = ((days + offset) % daysPerSeason + daysPerSeason) % daysPerSeason;
        return (int)Math.Ceiling(daysPerSeason - intoSeason);
    }

    public static double OffsetForDay(
        int season,
        int day,
        double days,
        double offset,
        double daysPerSeason
    )
    {
        // The Short Way Round, Today Lands On That Day
        var seasonsAhead = (season - SeasonAt(days, offset, daysPerSeason) + 4) % 4;
        if (seasonsAhead == 3)
        {
            seasonsAhead = -1;
        }

        var seasonStart =
            (Math.Floor((days + offset) / daysPerSeason) + seasonsAhead) * daysPerSeason;
        return seasonStart + day - 1 - Math.Floor(days);
    }

    private static bool SelfTestPasses()
    {
        // Tested In Game: Day 36 Offset 5 Spring, 15 Autumn
        return SeasonAt(36.4, 5, 5) == 0
            && SeasonAt(36.4, 15, 5) == 2
            && SeasonAt(2, -8, 5) == 2
            && DaysLeft(36, 5, 5) == 4
            && DaysLeft(39, 5, 5) == 1
            && DaysLeft(2, -8, 5) == 1
            && OffsetForDay(0, 2, 36.4, 5, 5) == 5
            && OffsetForDay(2, 1, 36.4, 5, 5) == 14
            && SeasonAt(36.4, 14, 5) == 2
            && DaysLeft(36, 14, 5) == 5
            && OffsetForDay(3, 5, 36.4, 5, 5) == 3
            && SeasonAt(36.4, 3, 5) == 3
            && DaysLeft(36, 3, 5) == 1
            && OffsetForDay(2, 5, 2, -8, 5) == -8;
    }
}
