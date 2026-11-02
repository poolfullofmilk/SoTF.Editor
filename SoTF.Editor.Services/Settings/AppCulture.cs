using System.Globalization;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Services.Settings;

public static class AppCulture
{
    // The Full Name, pt-BR Would Read As pt
    public static string Current => AppText.Culture.Name;

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
