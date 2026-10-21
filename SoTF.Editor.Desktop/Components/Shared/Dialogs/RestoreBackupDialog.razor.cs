using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class RestoreBackupDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    // Newest First
    [Parameter]
    public required List<SavegameBackup> Backups { get; set; }

    private SavegameBackup? _selected;

    // Measured Once, Every Row Click Renders Again
    private long _totalSize = BackupManager.TotalSize();

    private string UsageText =>
        Localizer[
            "BackupUsage",
            Formatter.FormatSize(_totalSize),
            Formatter.FormatSize(BackupManager.SizeLimit)
        ];

    private string RowClass(SavegameBackup backup)
    {
        var selected = backup == _selected ? " list-row-selected" : "";
        return "list-row rounded-inner px-3 py-1" + selected;
    }

    private string AgeLabel(SavegameBackup backup)
    {
        if (backup == Backups[0])
        {
            return Localizer["Newest"];
        }

        return backup == Backups[^1] ? Localizer["Oldest"] : "";
    }

    private async Task DeleteAsync(SavegameBackup backup)
    {
        if (
            !await DialogService.ConfirmAsync(
                Localizer,
                Localizer["DeleteBackup"],
                Localizer["DeleteBackupText", Formatter.FormatDateTimeWithSeconds(backup.TakenAt)]
            )
        )
        {
            return;
        }

        try
        {
            BackupManager.Delete(backup.Path);
            Backups.Remove(backup);
            _totalSize -= backup.Size;
            if (_selected == backup)
            {
                _selected = null;
            }

            if (Backups.Count == 0)
            {
                MudDialog.Cancel();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Delete Backup");
            Snackbar.Add(Localizer["UnableToDeleteBackup"], Severity.Error);
        }
    }

    private void Restore()
    {
        MudDialog.Close(DialogResult.Ok(_selected));
    }
}
