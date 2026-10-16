using System.Globalization;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Services.Settings;

public static class AppCulture
{
    // Every Language Named In Its Own Language
    public static IReadOnlyList<(string Culture, string Name)> Languages { get; } =
    [("en", "English"), ("nl", "Nederlands")];

    public static string Current => AppText.Culture.TwoLetterISOLanguageName;

    public static void Apply(string culture)
    {
        // Only Shipped Languages, English Otherwise
        var supported = Languages.Any(language => language.Culture == culture)
            ? culture
            : Languages[0].Culture;
        AppText.Culture = CultureInfo.GetCultureInfo(supported);

        if (UserSettings.Current.Language != supported)
        {
            UserSettings.Current.Language = supported;
            UserSettings.Current.Save();
        }
    }
}
