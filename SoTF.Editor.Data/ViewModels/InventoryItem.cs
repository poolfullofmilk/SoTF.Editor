using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Inventory;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.ViewModels;

public class InventoryItem(ItemBlockModel itemBlock, Item? item, bool isEquipped = false)
{
    public Item? Item { get; } = item;
    public bool IsEquipped { get; } = isEquipped;

    public ItemBlockModel ItemBlock { get; } = itemBlock;

    public string Type => Item?.Type ?? "";

    public string TypeRendered =>
        string.IsNullOrEmpty(Item?.Type) ? "" : AppText.Get(TextKeys.ItemType(Type));

    public int Id => Item?.Id ?? ItemBlock.ItemId;

    public string? ImageUrl => Item?.ImageUrl;

    public int TotalCount
    {
        get => ItemBlock.TotalCount;
        set => ItemBlock.TotalCount = value;
    }

    public string Name => Item?.Name ?? AppText.Get("UnknownItem");
    public string NormalizedName => Item?.NormalizedLowercaseName ?? AppText.Get("UnknownItem");

    public int Max => Item?.StorageMax?.Inventory ?? 1;

    public void SetToMax()
    {
        TotalCount = Max;
    }
}
