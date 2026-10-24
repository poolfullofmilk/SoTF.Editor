using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class UnlocksDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    private UnlocksViewModel? _viewModel;

    protected override void OnInitialized()
    {
        // The Steam Account That Owns The Open Savegame
        if (
            MainViewModel.SelectedSavegame?.SavegameStore.GetParentDirectory()?.Parent is
            { } steamFolder
        )
        {
            _viewModel = new UnlocksViewModel(
                Path.Combine(steamFolder.FullName, "PlayerProfile.json")
            );
        }
    }

    private void Save()
    {
        try
        {
            _viewModel?.Save();
            Snackbar.Add(AppText.Get("Saved"), Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The Game Can Hold The Profile Open
            Snackbar.Add(AppText.Format("UnableToSave", exception.Message), Severity.Error);
        }
    }
}
