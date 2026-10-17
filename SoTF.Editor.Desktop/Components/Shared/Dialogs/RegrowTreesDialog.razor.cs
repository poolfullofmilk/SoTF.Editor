using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class RegrowTreesDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private RegrowTreesViewModel _viewModel = null!;

    protected override void OnInitialized()
    {
        _viewModel = new RegrowTreesViewModel(MainViewModel.SelectedSavegame!);
    }

    private void Toggle(VegetationState state, bool selected)
    {
        _viewModel.VegetationStateSelected = selected
            ? _viewModel.VegetationStateSelected | state
            : _viewModel.VegetationStateSelected & ~state;
    }

    private void Regrow()
    {
        Snackbar.Add(_viewModel.Save(), Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }
}
