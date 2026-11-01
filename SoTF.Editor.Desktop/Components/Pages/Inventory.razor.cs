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

    [Inject]
    public required ArmorPageViewModel ArmorPage { get; set; }

    // Page State
    private string? _filter;
    private Category? _category;
    private bool _heldOnly;
    private List<string> _loadouts = ReadLoadouts();

    // Saved Inventories, Named By Their Files
    private static List<string> ReadLoadouts()
    {
        return Directory.Exists(AppInfo.LoadoutsDirectory)
            ?
            [
                .. Directory
                    .EnumerateFiles(AppInfo.LoadoutsDirectory, "*.txt")
                    .Order(StringComparer.CurrentCultureIgnoreCase),
            ]
            : [];
    }

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
        if (dialog.ShowDialog() == true)
        {
            await ImportFileAsync(dialog.FileName, "ImportInventory");
        }
    }

    private Task ApplyLoadoutAsync(string loadout)
    {
        return ImportFileAsync(loadout, "ApplyLoadout");
    }

    private async Task ImportFileAsync(string path, string titleKey)
    {
        if (!await ConfirmAsync(titleKey, "ImportInventoryText"))
        {
            return;
        }

        try
        {
            var imported = InventoryPage.Import(File.ReadAllText(path));
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

    private void SaveLoadout()
    {
        try
        {
            // The Windows Dialog Names It And Asks Before Overwriting
            var dialog = new SaveFileDialog
            {
                Title = AppText.Get("SaveLoadout"),
                Filter = "*.txt|*.txt",
                InitialDirectory = Directory.CreateDirectory(AppInfo.LoadoutsDirectory).FullName,
                FileName = AppText.Get("Loadout"),
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, InventoryPage.ExportText());
            _loadouts = ReadLoadouts();
            Snackbar.Add(AppText.Get("LoadoutSaved"), Severity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Save Loadout");
            Snackbar.Add(AppText.Get("UnableToSaveLoadout"), Severity.Error);
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
