namespace SoTF.Editor.Data.Models.Map.Static;

public record RawPoi(
    string? Title,
    string? Description,
    float X,
    float Y,
    int[]? Requirements,
    int[]? Items,
    string[]? Objects,
    bool IsUnderground = false,
    string? Wiki = null,
    Teleport? Teleport = null
)
{
    public int[]? GetMissingRequiredItems(HashSet<int> inventoryItems)
    {
        return Requirements == null || Requirements.Length == 0
            ? null
            : [.. Requirements.Where(itemId => !inventoryItems.Contains(itemId))];
    }
}
