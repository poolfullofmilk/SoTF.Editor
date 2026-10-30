using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public sealed class ItemWrapper(
    Item item,
    int? maxPerSlot,
    bool? preferHolder = false,
    FoodSpoilStorageModuleWrapper? foodSpoilStorageModuleWrapper = null
)
{
    public Item Item { get; } = item;
    public FoodSpoilStorageModuleWrapper? FoodSpoilStorageModuleWrapper { get; } =
        foodSpoilStorageModuleWrapper;

    public string Name => FoodSpoilStorageModuleWrapper?.Name ?? Item.Name;

    public int Max =>
        maxPerSlot
        ?? (preferHolder == true ? Item.StorageMax?.Holder : null)
        ?? Item.StorageMax?.Shelf
        ?? 1;
}
