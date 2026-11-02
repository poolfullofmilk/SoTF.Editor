using Microsoft.AspNetCore.Components;
using MudBlazor;
using Newtonsoft.Json;
using NLog;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class UnlocksDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

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
            MainViewModel.SelectedSavegame?.SavegameStore.GetParentDirectory()?.Parent
            is not { } steamFolder
        )
        {
            return;
        }

        try
        {
            _viewModel = new UnlocksViewModel(
                Path.Combine(steamFolder.FullName, "PlayerProfile.json")
            );
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A Locked Or Damaged Profile Is Never Written Over
            s_logger.Error(exception, "Failed To Read Player Profile");
            Snackbar.Add(AppText.Get("UnableToReadFile"), Severity.Error);
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
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // The Game Can Hold The Profile Open
            s_logger.Error(exception, "Failed To Save Player Profile");
            Snackbar.Add(AppText.Format("UnableToSave", exception.Message), Severity.Error);
        }
    }
}
