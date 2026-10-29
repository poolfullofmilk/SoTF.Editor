using MudBlazor.Utilities;

namespace SoTF.Editor.Desktop.Theming;

public static class StatColors
{
    // The Game's Own Bar Colours
    public const string Health = "#e5534b";
    public const string Fullness = "#f0a35e";
    public const string Hydration = "#4fa8f7";
    public const string Rest = "#a78bfa";
    public const string Stamina = "#57c785";
    public const string Sickness = "#b5c94f";
    public const string Affection = "#f472b6";

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
