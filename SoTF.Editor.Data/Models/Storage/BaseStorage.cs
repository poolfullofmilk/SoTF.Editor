using NLog;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public abstract class BaseStorage : IStorage
{
    private readonly int _index;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    protected readonly StorageDefinition Definition;

    public Position? Pos { get; set; }

    protected BaseStorage(StorageDefinition definition, int index)
    {
        Definition = definition;
        _index = index;

        for (var i = 0; i < definition.Slots; i++)
        {
            var storageSlot = new StorageSlot();
            Slots.Add(storageSlot);
        }
    }

    public List<StorageSlot> Slots { get; } = [];

    public List<ItemWrapper> SupportedItems => GetSupportedItems();

    public string Description => $"{Definition.Name} #{_index} ({GetTotalStored()})";

    public virtual void SetSaveData(StorageSaveData saveData)
    {
        var currentSlot = 0;

        Pos = saveData.Pos;

        var supportedItems = GetSupportedItems();

        foreach (var storageBlock in saveData.Storages)
        {
            if (currentSlot >= Slots.Count)
            {
                var storageSlot = new StorageSlot();
                Slots.Add(storageSlot);
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
                    var modules = itemBlock.UniqueItems.FirstOrDefault()?.Modules;

                    var storedItem = new StoredItem(
                        foundCandidate,
                        itemBlock.TotalCount,
                        supportedItems,
                        Definition.MaxPerSlot,
                        modules
                    );

                    if (currentSlot >= Slots.Count)
                    {
                        _logger.Warn(
                            $"Found Slot {currentSlot} Above Slot Count {Slots.Count} For {Definition.Name} ({Definition.Id})"
                        );
                        var storageSlot = new StorageSlot();
                        Slots.Add(storageSlot);
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

        RemoveOverflowingSlots();

        foreach (var storageSlot in Slots)
        {
            if (storageSlot.StoredItems.Count > 0)
            {
                continue;
            }

            var storedItem = new StoredItem(null, 0, supportedItems, Definition.MaxPerSlot);
            storageSlot.StoredItems.Add(storedItem);
        }
    }

    public abstract StorageSaveData ToStorageSaveData();

    public virtual void SetAllToMax()
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
        return Definition.Id;
    }

    public void ApplyFrom(IStorage storage)
    {
        Slots.Clear();
        SetSaveData(storage.ToStorageSaveData());
    }

    private void RemoveOverflowingSlots()
    {
        if (Slots.Count > Definition.Slots)
        {
            for (var i = Slots.Count - 1; i >= 0; i--)
            {
                var slot = Slots[i];
                if (slot.StoredItems.Count == 0)
                {
                    Slots.RemoveAt(i);
                }

                if (Slots.Count == Definition.Slots)
                {
                    break;
                }
            }
        }

        if (Slots.Count <= Definition.Slots)
        {
            return;
        }

        for (var i = Slots.Count - 1; i >= 0; i--)
        {
            Slots.RemoveAt(i);

            if (Slots.Count == Definition.Slots)
            {
                break;
            }
        }
    }

    private int GetTotalStored()
    {
        return Slots.Select(item => item.GetTotalStored()).Sum();
    }

    protected abstract List<ItemWrapper> GetSupportedItems();

    protected static void AddEffectiveSupportedItem(
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
