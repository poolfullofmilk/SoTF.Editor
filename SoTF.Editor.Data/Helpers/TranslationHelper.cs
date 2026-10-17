using System.Globalization;
using System.Text;

namespace SoTF.Editor.Data.Helpers;

public static class TranslationHelper
{
    public static string Normalize(string str)
    {
        // Accents Dropped, So Searches Match Either Spelling
        return string.Concat(
            str.Normalize(NormalizationForm.FormD)
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                )
        );
    }
}
