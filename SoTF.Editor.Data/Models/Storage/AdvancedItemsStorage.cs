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
            Slots.Add(new StorageSlot());
        }
    }

    public List<StorageSlot> Slots { get; } = [];

    public List<ItemWrapper> SupportedItems => GetSupportedItems();

    public string Description => $"{_definition.Name} #{_index}";

    public int TotalStored => Slots.Sum(slot => slot.GetTotalStored());

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
            slot.StoredItems.Add(
                storedItems.GetValueOrDefault(slotNumber)
                    ?? new StoredItem(null, 0, GetSupportedItems(slotNumber), null)
            );
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
                || !slotDefinition.SlotOrder.TryGetValue(selectedItem.Item.Id, out var slotOrder)
            )
            {
                for (var orderIndex = 0; orderIndex < slotDefinition.SlotOrder.Count; orderIndex++)
                {
                    storageSaveData.Storages.Add(new StorageBlock());
                }

                continue;
            }

            for (var orderIndex = 0; orderIndex < slotDefinition.SlotOrder.Count; orderIndex++)
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
        foreach (var storedItem in Slots.SelectMany(slot => slot.StoredItems))
        {
            storedItem.Fill();
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
        if (slotIndex >= _definition.Slots.Count)
        {
            s_logger.Warn($"Skipped Extra Slots For {_definition.Name} ({_definition.Id})");
            return [];
        }

        List<ItemWrapper> itemWrappers = [];
        foreach (var (itemId, max) in _definition.Slots[slotIndex].RestrictedItems)
        {
            if (_items.GetItem(itemId) is { } item)
            {
                itemWrappers.Add(new ItemWrapper(item, max));
            }
        }

        return itemWrappers;
    }

    private int? GetSlotIndex(int currentSlot)
    {
        // The Advanced Log Sled Saves Its Rabbit Holders In Pairs
        if (_definition.Id != 58)
        {
            return currentSlot;
        }

        return currentSlot <= 10 ? (currentSlot - 1) / 2 : null;
    }
}
