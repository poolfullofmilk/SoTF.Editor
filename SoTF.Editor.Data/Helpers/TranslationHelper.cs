using System.Globalization;
using System.Text;

namespace SoTF.Editor.Data.Helpers;

public static class TranslationHelper
{
    public static string Normalize(string str)
    {
        return new string([
            .. str.Normalize(NormalizationForm.FormD)
                .ToCharArray()
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                ),
        ]);
    }
}
