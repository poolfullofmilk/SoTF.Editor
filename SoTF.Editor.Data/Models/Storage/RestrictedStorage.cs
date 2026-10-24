namespace SoTF.Editor.Data.Models.Storage;

public class RestrictedStorage(StorageDefinition definition, ItemList itemList, int index)
    : BaseStorage(definition, index, SupportedItemsOf(itemList, definition))
{
    private static List<ItemWrapper> SupportedItemsOf(
        ItemList itemList,
        StorageDefinition storageDefinition
    )
    {
        var supportedItems = new List<ItemWrapper>();
        foreach (
            var item in itemList.Where(item =>
                storageDefinition.RestrictedItemIds is not { Count: > 0 } restrictedItemIds
                || restrictedItemIds.Contains(item.Id)
            )
        )
        {
            AddEffectiveSupportedItem(item, storageDefinition, supportedItems);
        }

        return supportedItems;
    }
}
