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
        // Whole Seasons Forward, So The Day Within It Stays
        var seasonsAhead = (season - SeasonAt(days, offset, daysPerSeason) + 4) % 4;
        return offset + (seasonsAhead * daysPerSeason);
    }

    private static bool SelfTestPasses()
    {
        // Tested In Game: Day 36 Offset 5 Spring, 15 Autumn
        return SeasonAt(36.4, 5, 5) == 0
            && SeasonAt(36.4, 15, 5) == 2
            && OffsetFor(3, 36.4, 5, 5) == 20
            && SeasonAt(36.4, OffsetFor(1, 36.4, 15, 5), 5) == 1
            && SeasonAt(2, -8, 5) == 2;
    }
}
