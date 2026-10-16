namespace SoTF.Editor.Data.Resources;

public static class ResourceFile
{
    public static string Read(string name)
    {
        // Game Data Ships Inside The Assembly, Never Beside The Exe
        using var stream =
            typeof(ResourceFile).Assembly.GetManifestResourceStream(
                $"{typeof(ResourceFile).Namespace}.{name}"
            ) ?? throw new FileNotFoundException($"Missing Embedded Resource {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
