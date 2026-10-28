using System.Globalization;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Services.Settings;

public static class AppCulture
{
    public static string Current => AppText.Culture.TwoLetterISOLanguageName;

    public static void Apply(string culture)
    {
        // Only Shipped Languages, English Otherwise
        var supported = AppText.Languages.Any(language => language.Culture == culture)
            ? culture
            : AppText.DefaultCulture;
        AppText.Culture = CultureInfo.GetCultureInfo(supported);

        if (UserSettings.Current.Language != supported)
        {
            UserSettings.Current.Language = supported;
            UserSettings.Current.Save();
        }
    }
}
