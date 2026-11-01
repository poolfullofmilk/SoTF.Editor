using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public sealed class StoredItem
{
    private readonly int? _maxPerSlot;

    // What Was Loaded, Written Back While Untouched
    private int _loadedCount;
    private List<UniqueItem> _uniqueItems = [];

    // As Built, A Put Back Brings It Back
    private readonly ItemWrapper? _builtItem;
    private readonly int _builtCount;
    private readonly List<UniqueItem> _builtUniqueItems;

    public int Count { get; set; }

    public StoredItem(
        ItemWrapper? item,
        int count,
        List<ItemWrapper> supportedItems,
        int? maxPerSlot,
        List<UniqueItem>? uniqueItems = null
    )
    {
        _maxPerSlot = maxPerSlot;
        SelectedItem = item;
        Count = count;
        SupportedItems = supportedItems;
        _uniqueItems = uniqueItems ?? [];
        _loadedCount = count;
        _builtItem = item;
        _builtCount = count;
        _builtUniqueItems = _uniqueItems;
    }

    public List<ItemWrapper> SupportedItems { get; }

    public List<BaseStorageModule>? Modules => _uniqueItems.FirstOrDefault()?.Modules;

    public int Max => _maxPerSlot ?? SelectedItem?.Max ?? 0;

    public ItemWrapper? SelectedItem
    {
        get;
        set
        {
            if (Equals(field, value))
            {
                return;
            }

            // Another Item Carries None Of The Old Modules
            field = value;
            _uniqueItems = [];
            _loadedCount = -1;
            Count = value == null ? 0 : Max;
        }
    }

    public List<UniqueItem> UniqueItemsToSave()
    {
        // Loaded Items Keep Their Own Timers And Seeds
        if (Count == _loadedCount)
        {
            return _uniqueItems;
        }

        var uniqueItems = _uniqueItems.Take(Count).ToList();
        while (uniqueItems.Count < Count && NewUniqueItem() is { } uniqueItem)
        {
            uniqueItems.Add(uniqueItem);
        }

        return uniqueItems;
    }

    private UniqueItem? NewUniqueItem()
    {
        if (SelectedItem?.FoodSpoilStorageModuleWrapper is { } moduleWrapper)
        {
            return new UniqueItem { Modules = [moduleWrapper.FoodSpoilStorageModule] };
        }

        return _uniqueItems.Count > 0 ? new UniqueItem { Modules = _uniqueItems[0].Modules } : null;
    }

    // Ponytail: Slots Rebuilt By Apply To All Put Back Nothing
    public bool CanRevert => !Equals(SelectedItem, _builtItem) || Count != _builtCount;

    public void Revert()
    {
        SelectedItem = _builtItem;
        _uniqueItems = _builtUniqueItems;
        _loadedCount = _builtCount;
        Count = _builtCount;
    }

    public void Clear()
    {
        SelectedItem = null;
    }

    public void SetToMax()
    {
        if (HasItem())
        {
            Count = Max;
        }
    }

    public void Fill(ItemWrapper? preferred = null)
    {
        // An Empty Slot Takes Its First Item At Its Max
        if (!HasItem())
        {
            SelectedItem = preferred ?? SupportedItems.MinBy(item => item.Name);
        }
        else
        {
            SetToMax();
        }
    }

    public bool CanFill()
    {
        return HasItem() ? Count < Max : SupportedItems.Count > 0;
    }

    public bool HasItem()
    {
        return SelectedItem != null;
    }
}
