using Microsoft.AspNetCore.Components;
using Microsoft.Win32;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Interop;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Inventory : EditorPageBase
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [Inject]
    public required InventoryPageViewModel InventoryPage { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // Page State
    private string? _filter;
    private Category? _category;
    private bool _heldOnly;

    private List<InventoryItem> Items =>
        [.. InventoryPage.Filter(_filter, _category?.Type, _heldOnly)];

    private async Task RemoveAllAsync()
    {
        if (await ConfirmAsync("RemoveAll", "RemoveAllText"))
        {
            InventoryPage.RemoveAllEquipped();
        }
    }

    private void Export()
    {
        try
        {
            var exported = InventoryPage.Export(MainViewModel.SelectedSavegame?.Name ?? "");
            Snackbar.Add(AppText.Get("InventoryExported"), Severity.Success);
            Explorer.ShowFile(exported);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Export Inventory");
            Snackbar.Add(AppText.Get("UnableToExportInventory"), Severity.Error);
        }
    }

    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = AppText.Get("ImportInventory"),
            Filter = "*.txt|*.txt",
            InitialDirectory = Directory.Exists(AppInfo.ExportsDirectory)
                ? AppInfo.ExportsDirectory
                : "",
        };
        if (
            dialog.ShowDialog() != true
            || !await ConfirmAsync("ImportInventory", "ImportInventoryText")
        )
        {
            return;
        }

        try
        {
            var imported = InventoryPage.Import(File.ReadAllText(dialog.FileName));
            if (imported > 0)
            {
                Snackbar.Add(AppText.Format("ImportedItems", imported), Severity.Success);
            }
            else
            {
                Snackbar.Add(AppText.Get("NothingToImport"), Severity.Warning);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Read Inventory");
            Snackbar.Add(AppText.Get("UnableToReadFile"), Severity.Error);
        }
    }

    private async Task AddAllAsync()
    {
        if (await ConfirmAsync("AddAll", "AddAllText"))
        {
            InventoryPage.AddAllUnassigned();
        }
    }
}
