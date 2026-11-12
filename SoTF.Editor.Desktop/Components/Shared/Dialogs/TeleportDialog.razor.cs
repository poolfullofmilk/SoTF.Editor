using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
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

    // Who Lands Where, As Caption And Value Rows
    private IEnumerable<(string Label, string Value, bool Wide)> Details
    {
        get
        {
            var landing = _viewModel.Landing.At();
            List<(string Label, string Value, bool Wide)> details =
            [
                (AppText.Get("ToTeleport"), _viewModel.Target, false),
                (AppText.Get("Destination"), _viewModel.Destination, false),
                (AppText.Get("Position"), landing.PrintableShort, true),
            ];

            // Two Possible Caves Show As Buttons Below Instead
            if (_viewModel.AreaChoices.Count == 0)
            {
                details.Add((AppText.Get("Area"), landing.Area.Name, true));
            }

            return details;
        }
    }

    private bool IsPicked(Area area)
    {
        return _viewModel.IsAreaPicked && _viewModel.Landing.Area.Equals(area);
    }

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
