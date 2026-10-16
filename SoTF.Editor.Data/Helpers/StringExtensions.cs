namespace SoTF.Editor.Data.Helpers;

public static class StringExtensions
{
    public static string ExtendFilenameWith(this string filename, string extension)
    {
        return $"{Path.GetFileNameWithoutExtension(filename)}{extension}{Path.GetExtension(filename)}";
    }
}
