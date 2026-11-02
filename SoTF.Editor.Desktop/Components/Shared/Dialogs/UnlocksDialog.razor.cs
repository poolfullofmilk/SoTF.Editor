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

    // A Profile That Cannot Be Read Is Never Written
    private bool _unreadable;

    private string EmptyText =>
        _unreadable
            ? AppText.Get("UnableToReadFile")
            : AppText.Format("PlayerProfileNotFound", SavegameManager.GetSavePath());

    private static bool IsProfileFailure(Exception exception)
    {
        return exception
            is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or JsonException;
    }

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
        catch (Exception exception) when (IsProfileFailure(exception))
        {
            s_logger.Error(exception, "Failed To Read Player Profile");
            _unreadable = true;
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
        catch (Exception exception) when (IsProfileFailure(exception))
        {
            // The Game Can Hold The Profile, Or It Changed Since
            s_logger.Error(exception, "Failed To Save Player Profile");
            Snackbar.Add(AppText.Format("UnableToSave", exception.Message), Severity.Error);
        }
    }
}
