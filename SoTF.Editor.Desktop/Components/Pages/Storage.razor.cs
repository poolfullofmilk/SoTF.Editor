using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Storage : EditorPageBase
{
    [Inject]
    public required StoragePageViewModel StoragePage { get; set; }

    // Batch Fill Draft
    private ItemWrapper? _fillItem;
    private int _fillCount = 1;

    private string StorageRowClass(IStorage storage)
    {
        var selected = StoragePage.SelectedStorage == storage ? " list-row-selected" : "";
        return "list-row rounded-inner px-3 py-2" + selected;
    }

    private void SelectStorage(IStorage storage)
    {
        // A Fill Item Fits Only The Storage It Came From
        StoragePage.SelectedStorage = storage;
        _fillItem = null;
        _fillCount = 1;
    }

    private async Task FillAllStoragesAsync()
    {
        if (await ConfirmAsync("FillAllStorages", "FillAllStoragesText"))
        {
            StoragePage.FillAllStorages();
        }
    }

    private async Task ApplyToSameTypeAsync(IStorage source)
    {
        if (await ConfirmAsync("ApplyToSameType", "ApplyToSameTypeTooltip"))
        {
            StoragePage.ApplyToAllOfSameType(source);
        }
    }

    private void SelectFillItem(ItemWrapper? item)
    {
        _fillItem = item;
        _fillCount = item?.Max ?? 1;
    }

    private static Position? PositionOf(IStorage storage)
    {
        return storage switch
        {
            BaseStorage baseStorage => baseStorage.Pos,
            AdvancedItemsStorage advancedItemsStorage => advancedItemsStorage.Pos,
            _ => null,
        };
    }

    private static List<ItemWrapper> SupportedItemsOf(IStorage storage)
    {
        return storage switch
        {
            BaseStorage baseStorage => baseStorage.SupportedItems,
            AdvancedItemsStorage advancedItemsStorage => advancedItemsStorage.SupportedItems,
            _ => [],
        };
    }
}
