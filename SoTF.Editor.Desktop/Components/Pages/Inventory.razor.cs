using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Inventory : EditorPageBase
{
    [Inject]
    public required InventoryPageViewModel InventoryPage { get; set; }

    // Page State
    private string? _filter;
    private Category? _category;
    private bool _heldOnly;

    private IEnumerable<InventoryItem> Items =>
        InventoryPage.Filter(_filter, _category?.Type, _heldOnly);

    private async Task RemoveAllAsync()
    {
        if (await ConfirmAsync("RemoveAll", "RemoveAllText"))
        {
            InventoryPage.RemoveAllEquipped();
        }
    }

    private async Task AddAllAsync()
    {
        if (await ConfirmAsync("AddAll", "AddAllText"))
        {
            InventoryPage.AddAllUnassigned();
        }
    }

    private void AddCategory()
    {
        if (_category is { } category)
        {
            InventoryPage.AddAllFromCategory(category);
        }
    }
}
