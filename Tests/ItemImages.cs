using System.Runtime.CompilerServices;
using System.Text.Json;

var root = Path.GetFullPath(Path.Combine(SourceDirectory(), ".."));
var resources = Path.Combine(root, "SoTF.Editor.Data", "Resources");
var webRoot = Path.Combine(root, "SoTF.Editor.Desktop", "wwwroot");
// Compare Exact Paths Even On Windows, Where File.Exists Ignores Case
var files = Directory
    .EnumerateFiles(Path.Combine(webRoot, "images", "items"), "*", SearchOption.AllDirectories)
    .Select(file => Path.GetRelativePath(webRoot, file).Replace('\\', '/'))
    .ToHashSet(StringComparer.Ordinal);
var referenced = new HashSet<string>(StringComparer.Ordinal);
var failures = 0;
var itemImages = 0;
var explicitPaths = 0;
using var gameData = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "GameData.json")));
using var itemPois = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "ItemPois.json")));
var items = gameData.RootElement.GetProperty("items");

foreach (var item in items.EnumerateArray())
{
    // Items Without An Image Use The Existing UI Fallbacks
    if (item.TryGetProperty("image", out var image) && image.ValueKind != JsonValueKind.Null)
    {
        itemImages++;
        Check($"images/items/{image.GetString()}", $"GameData.json item {item.GetProperty("id")}");
    }
}

CheckExplicitPaths(gameData.RootElement, "GameData.json");
CheckExplicitPaths(itemPois.RootElement, "ItemPois.json");
Console.WriteLine(
    $"{(failures == 0 ? "PASS" : "FAIL")}: {items.GetArrayLength()} items; "
        + $"item images: {itemImages}; explicit item paths: {explicitPaths}; "
        + $"unique files: {referenced.Count}; missing references: {failures}"
);
return failures == 0 ? 0 : 1;

void Check(string path, string source)
{
    referenced.Add(path);
    if (!files.Contains(path))
    {
        Console.Error.WriteLine($"FAIL: {source}: missing item image '{path}' (exact case required)");
        failures++;
    }
}

void CheckExplicitPaths(JsonElement element, string source)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (var property in element.EnumerateObject())
            {
                CheckExplicitPaths(property.Value, $"{source}.{property.Name}");
            }
            break;
        case JsonValueKind.Array:
            var index = 0;
            foreach (var value in element.EnumerateArray())
            {
                CheckExplicitPaths(value, $"{source}[{index++}]");
            }
            break;
        case JsonValueKind.String:
            var path = element.GetString()!.TrimStart('/');
            if (path.StartsWith("images/items/", StringComparison.OrdinalIgnoreCase))
            {
                explicitPaths++;
                Check(path, source);
            }
            break;
    }
}

static string SourceDirectory([CallerFilePath] string path = "")
{
    return Path.GetDirectoryName(path)!;
}
