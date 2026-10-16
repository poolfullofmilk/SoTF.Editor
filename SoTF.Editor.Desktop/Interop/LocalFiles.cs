namespace SoTF.Editor.Desktop.Interop;

public static class LocalFiles
{
    // Savegame Thumbnails Live Outside The Web Root
    public const string Host = "https://files.local/";

    public static string Url(string fullPath)
    {
        return Host + Uri.EscapeDataString(fullPath);
    }

    public static string? PathOf(string url)
    {
        return url.StartsWith(Host, StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(url[Host.Length..].Split('?')[0])
            : null;
    }
}
