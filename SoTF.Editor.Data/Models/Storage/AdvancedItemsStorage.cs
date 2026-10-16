using NLog;
using SoTF.Editor.Data.Models.SaveData.Storage;

namespace SoTF.Editor.Data.Models.Storage;

public class AdvancedItemsStorage : IStorage
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private readonly AdvancedStorageDefinition _definition;
    private readonly int _index;
    private readonly ItemList _items;

    public Position? Pos { get; set; }

    public AdvancedItemsStorage(AdvancedStorageDefinition definition, ItemList items, int index)
    {
        _definition = definition;
        _items = items;
        _index = index;

        for (var slotNumber = 0; slotNumber < definition.Slots.Count; slotNumber++)
        {
            var storageSlot = new StorageSlot();
            Slots.Add(storageSlot);
        }
    }

    public List<StorageSlot> Slots { get; } = [];

    public List<ItemWrapper> SupportedItems => GetSupportedItems();

    public string Description => $"{_definition.Name} #{_index} ({GetTotalStored()})";

    public void SetSaveData(StorageSaveData saveData)
    {
        Pos = saveData.Pos;
        var currentSlot = 0;
        var storedItems = new Dictionary<int, StoredItem>();

        foreach (
            var itemBlock in saveData.Storages.Select(storageBlock =>
                storageBlock.ItemBlocks.FirstOrDefault()
            )
        )
        {
            if (itemBlock == null)
            {
                currentSlot++;
                continue;
            }

            if (GetSlotIndex(currentSlot) is not { } slotIndex)
            {
                break;
            }

            var supportedItems = GetSupportedItems(slotIndex);
            var candidate = supportedItems.FirstOrDefault(item => item.Item.Id == itemBlock.ItemId);

            if (candidate == null)
            {
                s_logger.Warn(
                    $"Skipped Item {itemBlock.ItemId} Not Supported By {_definition.Name} ({_definition.Id})"
                );
                continue;
            }

            var storedItem = new StoredItem(
                candidate,
                itemBlock.TotalCount,
                supportedItems,
                null,
                itemBlock.UniqueItems
            );
            if (!storedItems.TryAdd(slotIndex, storedItem))
            {
                s_logger.Warn($"Found More Than One Item For Slot {slotIndex}");
                continue;
            }

            currentSlot++;
        }

        for (var slotNumber = 0; slotNumber < Slots.Count; slotNumber++)
        {
            var slot = Slots[slotNumber];
            slot.StoredItems.Clear();
            if (storedItems.GetValueOrDefault(slotNumber) is { } storedItem)
            {
                slot.StoredItems.Add(storedItem);
            }
            else
            {
                var supportedItems = GetSupportedItems(slotNumber);
                var emptyStoredItem = new StoredItem(null, 0, supportedItems, null);
                slot.StoredItems.Add(emptyStoredItem);
            }
        }
    }

    public StorageSaveData ToStorageSaveData()
    {
        var storageSaveData = new StorageSaveData
        {
            Id = _definition.Id,
            Storages = [],
            Pos = Pos,
        };

        for (var slotNumber = 0; slotNumber < Slots.Count; slotNumber++)
        {
            var slot = Slots[slotNumber];
            var slotDefinition = _definition.Slots[slotNumber];
            if (
                slot.StoredItems.FirstOrDefault()
                    is not { SelectedItem: { } selectedItem } storedItem
                || storedItem.Count == 0
                || slotDefinition.SlotOrder.GetIndexForItem(selectedItem.Item.Id)
                    is not { } slotOrder
            )
            {
                for (
                    var orderIndex = 0;
                    orderIndex <= slotDefinition.SlotOrder.GetMaxIndex();
                    orderIndex++
                )
                {
                    storageSaveData.Storages.Add(new StorageBlock());
                }

                continue;
            }

            for (
                var orderIndex = 0;
                orderIndex <= slotDefinition.SlotOrder.GetMaxIndex();
                orderIndex++
            )
            {
                var storageBlock = new StorageBlock();

                if (slotOrder == orderIndex)
                {
                    var storageItemBlock = new StorageItemBlock
                    {
                        ItemId = selectedItem.Item.Id,
                        TotalCount = storedItem.Count,
                        UniqueItems = storedItem.UniqueItemsToSave(),
                    };
                    storageBlock.ItemBlocks.Add(storageItemBlock);
                }

                storageSaveData.Storages.Add(storageBlock);
            }
        }

        return storageSaveData;
    }

    public void SetAllToMax()
    {
        foreach (var slot in Slots)
        foreach (var storedItem in slot.StoredItems)
        {
            if (!storedItem.HasItem() && storedItem.SupportedItems.Count == 1)
            {
                storedItem.SelectedItem = storedItem.SupportedItems[0];
            }
            else if (storedItem.HasItem())
            {
                storedItem.Count = storedItem.Max;
            }
        }
    }

    public int GetStorageTypeId()
    {
        return _definition.Id;
    }

    public void ApplyFrom(IStorage storage)
    {
        SetSaveData(storage.ToStorageSaveData());
    }

    private List<ItemWrapper> GetSupportedItems()
    {
        var itemIds = new HashSet<int>();
        var result = new List<ItemWrapper>();

        foreach (var (itemId, max) in _definition.Slots.SelectMany(slot => slot.RestrictedItems))
        {
            if (!itemIds.Add(itemId))
            {
                continue;
            }

            var item = _items.GetItem(itemId);
            if (item != null)
            {
                result.Add(new ItemWrapper(item, max));
            }
        }

        return result;
    }

    private List<ItemWrapper> GetSupportedItems(int slotIndex)
    {
        var itemWrappers = new List<ItemWrapper>();

        if (slotIndex >= _definition.Slots.Count)
        {
            s_logger.Warn($"Skipped Extra Slots For {_definition.Name} ({_definition.Id})");
            return itemWrappers;
        }

        var slotDefinition = _definition.Slots[slotIndex];

        foreach (var (itemId, max) in slotDefinition.RestrictedItems)
        {
            var item = _items.GetItem(itemId);
            if (item == null)
            {
                continue;
            }

            itemWrappers.Add(new ItemWrapper(item, max));
        }

        return itemWrappers;
    }

    private int? GetSlotIndex(int currentSlot)
    {
        if (_definition.Id != 58) //58 = Advanced Log Sled, fix for Rabbit Holders
        {
            return currentSlot;
        }

        if (currentSlot <= 10)
        {
            return (currentSlot - 1) / 2;
        }

        return null;
    }

    private int GetTotalStored()
    {
        return Slots.Select(item => item.GetTotalStored()).Sum();
    }
}
