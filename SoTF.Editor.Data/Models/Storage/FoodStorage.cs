using NLog;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public class FoodStorage(StorageDefinition definition, ItemList itemList, int index)
    : RestrictedStorage(definition, itemList, index)
{
    // Empty Racks Fill Up With Dried Fish
    private const int DefaultItemIdForUnselectedSlot = 436;
    private const int DefaultVariantForUnselectedSlot = 4;
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public override void SetSaveData(StorageSaveData saveData)
    {
        var currentSlot = 0;

        Pos = saveData.Pos;

        var supportedItems = SupportedItems;

        foreach (
            var itemBlock in saveData.Storages.SelectMany(storageBlock => storageBlock.ItemBlocks)
        )
        {
            if (supportedItems.All(supportedItem => supportedItem.Item.Id != itemBlock.ItemId))
            {
                s_logger.Warn($"Skipped Unsupported Item {itemBlock.ItemId}");
                continue;
            }

            // Every Unique Item Is One Rack Slot
            foreach (var uniqueItem in itemBlock.UniqueItems)
            {
                if (currentSlot >= Slots.Count)
                {
                    Slots.Add(new StorageSlot());
                }

                var foodSpoilStorageModule = uniqueItem
                    .Modules.OfType<FoodSpoilStorageModule>()
                    .FirstOrDefault();
                var selectedItem =
                    foodSpoilStorageModule == null
                        ? null
                        : supportedItems.FirstOrDefault(itemWrapper =>
                            itemWrapper.Item.Id == itemBlock.ItemId
                            && (
                                itemWrapper.FoodSpoilStorageModuleWrapper?.FoodSpoilStorageModule.IsEqualTo(
                                    foodSpoilStorageModule
                                ) ?? false
                            )
                        );

                Slots[currentSlot++]
                    .StoredItems.Add(
                        uniqueItem.Modules.Count == 0
                            ? new StoredItem(null, 0, supportedItems, Definition.MaxPerSlot)
                            : new StoredItem(
                                selectedItem,
                                selectedItem == null ? 0 : 1,
                                supportedItems,
                                Definition.MaxPerSlot,
                                [uniqueItem]
                            )
                    );
            }
        }

        foreach (var storageSlot in Slots.Where(slot => slot.StoredItems.Count == 0))
        {
            storageSlot.StoredItems.Add(
                new StoredItem(null, 0, supportedItems, Definition.MaxPerSlot)
            );
        }
    }

    public override StorageSaveData ToStorageSaveData()
    {
        // One Item Block Per Food, One Unique Item Per Slot
        var storageBlock = new StorageBlock();
        foreach (
            var group in Slots
                .SelectMany(slot => slot.StoredItems)
                .Where(item => item is { Count: > 0, SelectedItem: not null })
                .GroupBy(item => item.SelectedItem!.Item.Id)
        )
        {
            storageBlock.ItemBlocks.Add(
                new StorageItemBlock
                {
                    TotalCount = group.Count(),
                    ItemId = group.Key,
                    UniqueItems =
                    [
                        .. group.Select(storedItem => new UniqueItem
                        {
                            Modules = SelectModules(storedItem, storedItem.SelectedItem!),
                        }),
                    ],
                }
            );
        }

        return new StorageSaveData { Id = Definition.Id, Storages = [storageBlock] };
    }

    private static List<BaseStorageModule> SelectModules(
        StoredItem storedItem,
        ItemWrapper selectedItem
    )
    {
        var chosenFoodModule = selectedItem.FoodSpoilStorageModuleWrapper?.FoodSpoilStorageModule;
        var modules =
            storedItem
                .Modules?.Where(module =>
                    module switch
                    {
                        FoodSpoilStorageModule => selectedItem.Item.FoodSpoilModuleDefinition
                            != null,
                        SourceActorStorageModule => selectedItem.Item.SourceActorModuleDefinition
                            != null,
                        _ => true,
                    }
                )
                .Select(module =>
                    // Only Another Food State Swaps The Module, Its Timer Stays
                    module is FoodSpoilStorageModule foodModule
                    && chosenFoodModule != null
                    && !foodModule.IsEqualTo(chosenFoodModule)
                        ? chosenFoodModule
                        : module
                )
                .ToList()
            ?? [];

        if (
            selectedItem.Item.FoodSpoilModuleDefinition is { } foodSpoilModuleDefinition
            && !modules.OfType<FoodSpoilStorageModule>().Any()
        )
        {
            modules.Add(chosenFoodModule ?? foodSpoilModuleDefinition.BuildNewModuleWithDefaults());
        }

        if (
            selectedItem.Item.SourceActorModuleDefinition is { } sourceActorModuleDefinition
            && !modules.OfType<SourceActorStorageModule>().Any()
        )
        {
            modules.Add(sourceActorModuleDefinition.BuildNewModuleWithDefaults());
        }

        return modules;
    }

    public override void SetAllToMax()
    {
        foreach (var storedItem in Slots.SelectMany(slot => slot.StoredItems))
        {
            storedItem.Fill(
                storedItem.SupportedItems.FirstOrDefault(wrapper =>
                    wrapper.Item.Id == DefaultItemIdForUnselectedSlot
                    && wrapper.FoodSpoilStorageModuleWrapper?.FoodSpoilStorageModule
                        is { CurrentState: DefaultVariantForUnselectedSlot }
                )
            );
        }
    }
}
