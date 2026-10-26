using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Layout;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class RestoreBackupDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    // Newest First
    [Parameter]
    public required List<SavegameBackup> Backups { get; set; }

    [Parameter]
    public required string SavegamePath { get; set; }

    // The Picked Backup With Its Saved List And Its Differences
    private SavegameBackup? _selected;
    private Dictionary<string, List<Change>> _savedChanges = [];
    private Dictionary<string, List<Change>>? _differences;

    // Pages In Menu Order, Other Files Last
    private IEnumerable<KeyValuePair<string, List<Change>>> OrderedDifferences =>
        (_differences ?? []).OrderBy(pair =>
            PageOf(pair.Key) is { } link ? MainLayout.Pages.ToList().IndexOf(link) : int.MaxValue
        );
    private bool _compareFailed;

    private string? DifferencesText
    {
        get
        {
            if (_compareFailed)
            {
                return AppText.Get("UnableToCompare");
            }

            if (_differences is null)
            {
                return AppText.Get("Comparing");
            }

            return _differences.Count == 0 ? AppText.Get("SameAsNow") : null;
        }
    }

    private async Task SelectAsync(SavegameBackup backup)
    {
        _selected = backup;
        _savedChanges = BackupManager.ChangesOf(backup.Path);
        _differences = null;
        _compareFailed = false;

        // Reading Both Saves Takes A Moment, Off The Window's Thread
        try
        {
            var differences = await Task.Run(() =>
                BackupDiff.Describe(
                    SavegamePath,
                    backup.Path,
                    GameData,
                    key => AppText.Get(Pages.Game.SetupLabelKey(key), BackupDiff.Words(key))
                )
            );
            if (_selected == backup)
            {
                _differences = differences;
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            s_logger.Warn(exception, "Failed To Compare Backup");
            _compareFailed = _selected == backup;
        }
    }

    private static MainLayout.PageLink? PageOf(string href)
    {
        return MainLayout.Pages.FirstOrDefault(link => link.Href == href);
    }

    private static string LabelOf(string href)
    {
        return PageOf(href) is { } link ? AppText.Get(link.LabelKey) : AppText.Get(href, href);
    }

    // Restores And Other Files Are No Page, Own Icons
    private static string? IconOf(string href)
    {
        return href switch
        {
            BackupManager.RestoreKey => Icons.Material.Rounded.Restore,
            BackupDiff.FilesKey => Icons.Material.Rounded.Description,
            _ => PageOf(href)?.Icon,
        };
    }

    // Measured Once, Every Row Click Renders Again
    private long _totalSize = BackupManager.TotalSize();

    private string UsageText =>
        AppText.Format(
            "BackupUsage",
            Formatter.FormatSize(_totalSize),
            Formatter.FormatSize(BackupManager.SizeLimit)
        );

    private string RowClass(SavegameBackup backup)
    {
        var selected = backup == _selected ? " list-row-selected" : "";
        return "list-row rounded-inner px-3 py-1" + selected;
    }

    private string AgeLabel(SavegameBackup backup)
    {
        if (backup == Backups[0])
        {
            return AppText.Get("Newest");
        }

        return backup == Backups[^1] ? AppText.Get("Oldest") : "";
    }

    private async Task DeleteAsync(SavegameBackup backup)
    {
        if (
            !await DialogService.ConfirmAsync(
                AppText.Get("DeleteBackup"),
                AppText.Format(
                    "DeleteBackupText",
                    Formatter.FormatDateTimeWithSeconds(backup.TakenAt)
                )
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
            Snackbar.Add(AppText.Get("UnableToDeleteBackup"), Severity.Error);
        }
    }

    private async Task DeleteAllButNewestAsync()
    {
        if (
            !await DialogService.ConfirmAsync(
                AppText.Get("DeleteAllButNewest"),
                AppText.Format("DeleteAllButNewestText", Backups.Count - 1)
            )
        )
        {
            return;
        }

        var failed = BackupManager.DeleteAllButNewest(SavegamePath);
        Backups = BackupManager.List(SavegamePath);
        _totalSize = BackupManager.TotalSize();
        if (_selected is { } selected && !Backups.Exists(backup => backup.Path == selected.Path))
        {
            _selected = null;
        }

        if (failed > 0)
        {
            Snackbar.Add(AppText.Get("UnableToDeleteBackup"), Severity.Error);
        }
    }

    private void Restore()
    {
        MudDialog.Close(DialogResult.Ok(_selected));
    }
}
