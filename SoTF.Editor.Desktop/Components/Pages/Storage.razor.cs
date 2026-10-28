using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
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

    private string? _filter;

    // One Menu For Every Slot, A Select Each Cost Popovers
    private MudMenu? _slotMenu;
    private StoredItem? _menuSlot;

    // A Matching Type Shows Whole, Otherwise Only Its Matching Storages
    private List<(string Title, List<IStorage> Storages)> FilteredCollections()
    {
        return
        [
            .. StoragePage
                .StorageCollections.Select(collection =>
                    (
                        Title: collection.Name,
                        Storages: Matches(collection.Name)
                            ? collection.Storages
                            :
                            [
                                .. collection.Storages.Where(storage =>
                                    Matches(storage.Description)
                                ),
                            ]
                    )
                )
                .Where(group => group.Storages.Count > 0),
        ];
    }

    // Only A Shown Storage, A Search Can Hide It
    private IStorage? SelectedIn(List<(string Title, List<IStorage> Storages)> collections)
    {
        return collections.Any(group => group.Storages.Contains(StoragePage.SelectedStorage!))
            ? StoragePage.SelectedStorage
            : null;
    }

    private async Task OpenSlotMenuAsync(MouseEventArgs args, StoredItem slot)
    {
        // The Menu Opens Where The Slot Was Clicked
        _menuSlot = slot;
        if (_slotMenu is { } slotMenu)
        {
            await slotMenu.OpenMenuAsync(args);
        }
    }

    private void PickSlotItem(ItemWrapper item)
    {
        _menuSlot?.SelectedItem = item;
    }

    private bool Matches(string text)
    {
        return string.IsNullOrWhiteSpace(_filter)
            || text.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    private string StorageRowClass(IStorage storage)
    {
        var selected = StoragePage.SelectedStorage == storage ? " list-row-selected" : "";
        return "list-row px-4 py-2" + selected;
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
}
