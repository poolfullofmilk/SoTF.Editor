using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class ConsumedItemsDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

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

    protected override void OnInitialized()
    {
        _viewModel = new ModifyConsumedItemsViewModel(MainViewModel.SelectedSavegame!, GameData);
    }

    private async Task RemoveAllAsync()
    {
        if (
            await DialogService.ConfirmAsync(
                Localizer,
                Localizer["RemoveAll"],
                Localizer["RemoveConsumedItemsText"]
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
        Snackbar.Add(Localizer["ChangesAdded"], Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }
}
