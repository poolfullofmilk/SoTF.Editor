using MudBlazor.Utilities;

namespace SoTF.Editor.Desktop.Theming;

public static class StatColors
{
    // Theme Colours Where A Game Bar Has One
    public static readonly string Health = AppTheme.Custom.PaletteDark.Error.Value;
    public static readonly string Stamina = AppTheme.Custom.PaletteDark.Success.Value;
    public static readonly string Summer = AppTheme.Custom.PaletteDark.Warning.Value;

    // The Game's Own Bar Colours, The Seasons Share Them
    public const string Fullness = "#ee8a4c";
    public const string Hydration = "#8ec5f5";
    public const string Rest = "#a78bfa";
    public const string Sickness = "#b5c94f";
    public const string Affection = "#f472b6";

    public static string Season(int season)
    {
        // Green Spring, Amber Summer, Orange Autumn, Icy Winter
        return season switch
        {
            0 => Stamina,
            1 => Summer,
            2 => Fullness,
            _ => Hydration,
        };
    }

    public static string? StyleOf(string? color)
    {
        // A Tinted Bar Takes The Info Colour's Place Inside Itself
        if (color is null)
        {
            return null;
        }

        var parsed = new MudColor(color);
        return $"--mud-palette-info:{color};--mud-palette-info-rgb:{parsed.R},{parsed.G},{parsed.B}";
    }
}
