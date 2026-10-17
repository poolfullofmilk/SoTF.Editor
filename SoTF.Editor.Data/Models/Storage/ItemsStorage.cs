using SoTF.Editor.Data.Models.SaveData.Storage;

namespace SoTF.Editor.Data.Models.Storage;

public class ItemsStorage : BaseStorage
{
    private readonly List<ItemWrapper> _supportedItems;

    public ItemsStorage(StorageDefinition definition, ItemList itemList, int index)
        : base(definition, index)
    {
        _supportedItems = GetSupportedItems(itemList, definition);
    }

    private List<ItemWrapper> GetSupportedItems(
        ItemList itemList,
        StorageDefinition storageDefinition
    )
    {
        var supportedItems = new List<ItemWrapper>();
        if (Definition.RestrictedItemIds?.Count == 0)
        {
            return supportedItems;
        }

        // Without A List, Anything Fitting A Shelf Or Holder
        var candidates = itemList.Where(item =>
            Definition.RestrictedItemIds is { Count: > 0 } restrictedItemIds
                ? restrictedItemIds.Contains(item.Id)
                : item.IsInventoryItem
                    && (item.StorageMax?.Shelf > 0 || item.StorageMax?.Holder > 0)
        );

        foreach (var item in candidates)
        {
            AddEffectiveSupportedItem(item, storageDefinition, supportedItems);
        }

        return [.. supportedItems.OrderBy(item => item.Name)];
    }

    protected override List<ItemWrapper> GetSupportedItems()
    {
        return _supportedItems;
    }

    public static StorageSaveData GenericToStorageSaveData(
        IStorageDefinition storageDefinition,
        IEnumerable<StorageSlot> slots
    )
    {
        var storageSaveData = new StorageSaveData { Id = storageDefinition.Id, Storages = [] };

        foreach (var slot in slots)
        {
            var storageBlock = new StorageBlock
            {
                ItemBlocks = new List<StorageItemBlock>(slot.StoredItems.Count),
            };

            foreach (var storedItem in slot.StoredItems)
            {
                if (storedItem.Count == 0 || storedItem.SelectedItem is not { } item)
                {
                    continue;
                }

                storageBlock.ItemBlocks.Add(
                    new StorageItemBlock
                    {
                        TotalCount = storedItem.Count,
                        ItemId = item.Item.Id,
                        UniqueItems = storedItem.UniqueItemsToSave(),
                    }
                );
            }

            storageSaveData.Storages.Add(storageBlock);
        }

        return storageSaveData;
    }

    public override StorageSaveData ToStorageSaveData()
    {
        return GenericToStorageSaveData(Definition, Slots);
    }
}
