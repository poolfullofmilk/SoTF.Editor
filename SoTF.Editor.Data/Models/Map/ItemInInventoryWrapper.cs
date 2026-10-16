namespace SoTF.Editor.Data.Models.Map;

public class ItemInInventoryWrapper(Item item, bool hasItem)
{
    public Item Item { get; } = item;

    public bool HasItem { get; } = hasItem;
}
