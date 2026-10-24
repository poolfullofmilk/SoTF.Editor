namespace SoTF.Editor.Data.Models.Storage;

public class ItemsStorage(StorageDefinition definition, ItemList itemList, int index)
    : BaseStorage(definition, index, SupportedItemsOf(itemList, definition))
{
    private static List<ItemWrapper> SupportedItemsOf(
        ItemList itemList,
        StorageDefinition storageDefinition
    )
    {
        var supportedItems = new List<ItemWrapper>();
        if (storageDefinition.RestrictedItemIds?.Count == 0)
        {
            return supportedItems;
        }

        // Without A List, Anything Fitting A Shelf Or Holder
        var candidates = itemList.Where(item =>
            storageDefinition.RestrictedItemIds is { Count: > 0 } restrictedItemIds
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
}
