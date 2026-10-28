using System.Globalization;
using System.Resources;

namespace SoTF.Editor.Data.Resources;

public static class AppText
{
    // Code Without A Localizer Reads The Same Resx
    private static readonly ResourceManager s_resourceManager = new(typeof(SharedResource));

    // English Is The Neutral Resx, Others Fall Back To It
    public const string DefaultCulture = "en";

    // Every Shipped Language, Named In Its Own Language
    public static IReadOnlyList<(string Culture, string Name)> Languages { get; } =
    [
        ("de", "Deutsch"),
        ("en", "English"),
        ("fr", "Français"),
        ("nl", "Nederlands"),
        ("tr", "Türkçe"),
    ];

    // The Picked Language, WPF Pins The Thread Culture At Startup
    public static CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo(DefaultCulture);

    public static string Get(string key)
    {
        return s_resourceManager.GetString(key, Culture) ?? key;
    }

    public static string Get(string key, string fallback)
    {
        return s_resourceManager.GetString(key, Culture) ?? fallback;
    }

    public static string Format(string key, params object[] arguments)
    {
        return string.Format(Culture, Get(key), arguments);
    }

    public static bool Has(string key, CultureInfo culture)
    {
        // No Parent Fallback, So Every Missing Key Counts
        return s_resourceManager.GetResourceSet(culture, true, false)?.GetString(key) != null;
    }
}
