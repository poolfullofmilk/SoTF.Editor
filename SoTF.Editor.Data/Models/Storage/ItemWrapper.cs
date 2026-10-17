using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public class ItemWrapper
{
    private readonly int? _maxPerSlot;
    private readonly bool _preferHolder;

    public ItemWrapper(
        Item item,
        int? maxPerSlot,
        bool? preferHolder = false,
        FoodSpoilStorageModuleWrapper? foodSpoilStorageModuleWrapper = null
    )
    {
        _maxPerSlot = maxPerSlot;
        _preferHolder = preferHolder ?? false;
        Item = item;
        FoodSpoilStorageModuleWrapper = foodSpoilStorageModuleWrapper;
    }

    public Item Item { get; }
    public FoodSpoilStorageModuleWrapper? FoodSpoilStorageModuleWrapper { get; }

    public string Name => FoodSpoilStorageModuleWrapper?.Name ?? Item.Name;

    public int Max =>
        _maxPerSlot
        ?? (_preferHolder ? Item.StorageMax?.Holder : null)
        ?? Item.StorageMax?.Shelf
        ?? 1;
}
