using NLog;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public class BaseStorage : IStorage
{
    private readonly int _index;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    protected readonly StorageDefinition Definition;

    public Position? Pos { get; set; }

    public BaseStorage(StorageDefinition definition, ItemList itemList, int index)
    {
        Definition = definition;
        _index = index;
        SupportedItems = SupportedItemsOf(itemList, definition);

        for (var slotNumber = 0; slotNumber < definition.Slots; slotNumber++)
        {
            Slots.Add(new StorageSlot());
        }
    }

    public List<StorageSlot> Slots { get; } = [];

    public List<ItemWrapper> SupportedItems { get; }

    public string Description => $"{Definition.Name} #{_index}";

    public int TotalStored => Slots.Sum(slot => slot.GetTotalStored());

    public virtual void SetSaveData(StorageSaveData saveData)
    {
        var currentSlot = 0;

        Pos = saveData.Pos;

        var supportedItems = SupportedItems;

        foreach (var storageBlock in saveData.Storages)
        {
            // A Save Can Hold More Slots Than Defined, All Stay
            if (currentSlot >= Slots.Count)
            {
                Slots.Add(new StorageSlot());
            }

            if (storageBlock.ItemBlocks.Count == 0)
            {
                currentSlot++;
                continue;
            }

            foreach (var itemBlock in storageBlock.ItemBlocks)
            {
                var candidates = supportedItems
                    .Where(supportedItem => supportedItem.Item.Id == itemBlock.ItemId)
                    .ToList();

                if (candidates.Count == 0)
                {
                    _logger.Warn(
                        $"Skipped Item {itemBlock.ItemId} Not Supported By {Definition.Name} ({Definition.Id})"
                    );
                    continue;
                }

                ItemWrapper? foundCandidate = null;

                if (candidates.Count == 1 || itemBlock.UniqueItems.Count == 0)
                {
                    foundCandidate = candidates[0];
                }
                else
                {
                    foreach (var candidate in candidates)
                    {
                        if (
                            candidate.FoodSpoilStorageModuleWrapper is not { } moduleWrapper
                            || !itemBlock.HasModuleEqualTo(moduleWrapper.FoodSpoilStorageModule)
                        )
                        {
                            continue;
                        }

                        foundCandidate = candidate;
                        break;
                    }
                }

                if (foundCandidate != null)
                {
                    var storedItem = new StoredItem(
                        foundCandidate,
                        itemBlock.TotalCount,
                        supportedItems,
                        Definition.MaxPerSlot,
                        itemBlock.UniqueItems
                    );

                    if (currentSlot >= Slots.Count)
                    {
                        _logger.Warn(
                            $"Found Slot {currentSlot} Above Slot Count {Slots.Count} For {Definition.Name} ({Definition.Id})"
                        );
                        Slots.Add(new StorageSlot());
                    }

                    Slots[currentSlot++].StoredItems.Add(storedItem);
                }
                else
                {
                    _logger.Warn(
                        $"Skipped Item {itemBlock.ItemId} Without A Slot In {Definition.Name} ({Definition.Id})"
                    );
                }
            }
        }

        foreach (var storageSlot in Slots.Where(slot => slot.StoredItems.Count == 0))
        {
            storageSlot.StoredItems.Add(
                new StoredItem(null, 0, supportedItems, Definition.MaxPerSlot)
            );
        }
    }

    public virtual StorageSaveData ToStorageSaveData()
    {
        var storageSaveData = new StorageSaveData { Id = Definition.Id, Storages = [] };

        foreach (var slot in Slots)
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

    public virtual void SetAllToMax()
    {
        foreach (var storedItem in Slots.SelectMany(slot => slot.StoredItems))
        {
            storedItem.Fill();
        }
    }

    public int GetStorageTypeId()
    {
        return Definition.Id;
    }

    public void ApplyFrom(IStorage storage)
    {
        Slots.Clear();
        SetSaveData(storage.ToStorageSaveData());
    }

    private static List<ItemWrapper> SupportedItemsOf(
        ItemList itemList,
        StorageDefinition definition
    )
    {
        var restrictedItemIds = definition.RestrictedItemIds;
        var isShelf = definition.Type == StorageType.Items;
        if (isShelf && restrictedItemIds?.Count == 0)
        {
            return [];
        }

        // No List Means Anything, Or What Fits A Shelf
        var supportedItems = new List<ItemWrapper>();
        foreach (
            var item in itemList.Where(item =>
                restrictedItemIds is { Count: > 0 }
                    ? restrictedItemIds.Contains(item.Id)
                    : !isShelf
                        || (
                            item.IsInventoryItem
                            && (item.StorageMax?.Shelf > 0 || item.StorageMax?.Holder > 0)
                        )
            )
        )
        {
            AddEffectiveSupportedItem(item, definition, supportedItems);
        }

        return isShelf ? [.. supportedItems.OrderBy(item => item.Name)] : supportedItems;
    }

    private static void AddEffectiveSupportedItem(
        Item item,
        StorageDefinition storageDefinition,
        List<ItemWrapper> target
    )
    {
        if (item.FoodSpoilModuleDefinition is { } foodSpoilModule)
        {
            target.AddRange(
                from variant in foodSpoilModule.Variants
                select new ItemWrapper(
                    item,
                    storageDefinition.MaxPerSlot,
                    storageDefinition.PreferHolder,
                    BuildFoodSpoilStorageModuleWrapper(foodSpoilModule, item.Id, variant)
                )
            );
        }
        else
        {
            target.Add(
                new ItemWrapper(item, storageDefinition.MaxPerSlot, storageDefinition.PreferHolder)
            );
        }
    }

    private static FoodSpoilStorageModuleWrapper BuildFoodSpoilStorageModuleWrapper(
        ItemModule foodSpoilModule,
        int itemId,
        int variant
    )
    {
        var storageModule = new FoodSpoilStorageModule(foodSpoilModule.ModuleId, variant);
        return new FoodSpoilStorageModuleWrapper(storageModule, itemId, variant);
    }
}
