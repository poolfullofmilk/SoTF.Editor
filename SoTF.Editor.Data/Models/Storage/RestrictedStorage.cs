namespace SoTF.Editor.Data.Models.Storage;

public abstract class RestrictedStorage : BaseStorage
{
    private readonly List<ItemWrapper> _supportedItems;

    protected RestrictedStorage(StorageDefinition definition, ItemList itemList, int index)
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
        foreach (
            var item in itemList.Where(item =>
                Definition.RestrictedItemIds is not { Count: > 0 } restrictedItemIds
                || restrictedItemIds.Contains(item.Id)
            )
        )
        {
            AddEffectiveSupportedItem(item, storageDefinition, supportedItems);
        }

        return supportedItems;
    }

    protected override List<ItemWrapper> GetSupportedItems()
    {
        return _supportedItems;
    }
}
