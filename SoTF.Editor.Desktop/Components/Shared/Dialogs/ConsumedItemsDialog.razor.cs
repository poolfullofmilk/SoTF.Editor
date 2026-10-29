using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class ConsumedItemsDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    private ModifyConsumedItemsViewModel _viewModel = null!;
    private HashSet<ModifyConsumedItemsViewModel.ConsumedItemWrapper> _selected = [];

    // The Ticked Rows Are The Ones Removed
    private void SelectionChanged(
        HashSet<ModifyConsumedItemsViewModel.ConsumedItemWrapper> selected
    )
    {
        _selected = selected;
        foreach (var item in _viewModel.ConsumedItems)
        {
            item.Remove = selected.Contains(item);
        }
    }

    protected override void OnInitialized()
    {
        _viewModel = new ModifyConsumedItemsViewModel(MainViewModel.SelectedSavegame!, GameData);
    }

    private async Task RemoveAllAsync()
    {
        if (
            await DialogService.ConfirmAsync(
                AppText.Get("RemoveAll"),
                AppText.Get("RemoveConsumedItemsText")
            )
        )
        {
            _viewModel.RemoveAll();
            Close();
        }
    }

    private void Apply()
    {
        _viewModel.Save();
        Close();
    }

    private void Close()
    {
        Snackbar.Add(AppText.Get("ChangesAdded"), Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }
}
