namespace SoTF.Editor.Data.Models.Map.Static;

public record RawItemPoi(
    string? Description,
    float X,
    float Y,
    int[]? Requirements,
    int[]? AltItemIds,
    Teleport Teleport
)
{
    public int[]? GetMissingRequiredItems(HashSet<int> inventoryItems)
    {
        return Requirements == null || Requirements.Length == 0
            ? null
            : [.. Requirements.Where(itemId => !inventoryItems.Contains(itemId))];
    }
}
