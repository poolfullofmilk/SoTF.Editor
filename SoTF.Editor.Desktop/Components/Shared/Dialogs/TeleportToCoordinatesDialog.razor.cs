using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class TeleportToCoordinatesDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private TeleportToCoordinatesViewModel _viewModel = null!;

    public static async Task ShowAsync(IDialogService dialogService)
    {
        var dialog = await dialogService.ShowAsync<TeleportToCoordinatesDialog>(
            AppText.Get("TeleportToCoordinates"),
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );
        await dialog.Result;
    }

    protected override void OnInitialized()
    {
        _viewModel = new TeleportToCoordinatesViewModel(PlayerPage, GameData.AreaManager);
    }

    private void Teleport()
    {
        _viewModel.Teleport();
        Snackbar.Add(AppText.Get("ChangesAdded"), Severity.Info);
        MainViewModel.NotifyChanged();
        MudDialog.Close(DialogResult.Ok(true));
    }
}
