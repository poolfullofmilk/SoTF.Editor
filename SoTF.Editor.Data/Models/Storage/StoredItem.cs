using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public class StoredItem
{
    private readonly int? _maxPerSlot;

    public int Count { get; set; }

    public StoredItem(
        ItemWrapper? item,
        int count,
        List<ItemWrapper> supportedItems,
        int? maxPerSlot,
        List<IStorageModule>? modules = null
    )
    {
        _maxPerSlot = maxPerSlot;
        SelectedItem = item;
        Count = count;
        SupportedItems = supportedItems;
        Modules = modules;
    }

    public List<ItemWrapper> SupportedItems { get; }
    public List<IStorageModule>? Modules { get; }

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

            field = value;
            Count = value == null ? 0 : Max;
        }
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

    public bool HasItem()
    {
        return SelectedItem != null;
    }
}
