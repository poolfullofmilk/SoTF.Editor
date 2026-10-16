using Microsoft.AspNetCore.Components;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Inventory : EditorPageBase
{
    [Inject]
    public required InventoryPageViewModel InventoryPage { get; set; }

    // Page State
    private string? _inventoryFilter;
    private string? _unassignedFilter;
    private Category? _category;

    private void AddCategory()
    {
        if (_category is { } category)
        {
            InventoryPage.AddAllFromCategory(category);
        }
    }
}
