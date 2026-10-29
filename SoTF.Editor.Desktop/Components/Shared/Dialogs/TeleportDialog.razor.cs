using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class TeleportDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public required BasePoi Destination { get; set; }

    [Parameter]
    public TeleportTarget Mode { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private MapTeleportWindowViewModel _viewModel = null!;

    // Who Goes Where, As Caption And Value Pairs
    private IEnumerable<(string Label, string Value)> Details =>
        [
            (AppText.Get("ToTeleport"), _viewModel.Target),
            (AppText.Get("Destination"), _viewModel.Destination),
            (
                AppText.Get("Position"),
                $"X: {_viewModel.Landing.X:F2}, Y: {_viewModel.Landing.Y:F2}, Z: {_viewModel.Landing.Z:F2}"
            ),
        ];

    protected override void OnInitialized()
    {
        _viewModel = new MapTeleportWindowViewModel(
            Destination,
            Mode,
            GameData.AreaManager,
            FollowerPage
        );
    }

    private void Teleport()
    {
        _viewModel.DoTeleport(PlayerPage.PlayerState);
        Snackbar.Add(AppText.Get("ChangesAdded"), Severity.Info);
        MainViewModel.NotifyChanged();
        MudDialog.Close(DialogResult.Ok(true));
    }
}
