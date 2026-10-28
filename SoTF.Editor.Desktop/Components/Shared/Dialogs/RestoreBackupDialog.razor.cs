using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
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
                DifferenceList.Describe(SavegamePath, backup.Path, GameData)
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

    // Measured Once, Every Row Click Renders Again
    private long _totalSize = BackupManager.TotalSize();

    private string UsageText =>
        AppText.Format(
            "BackupUsage",
            Formatter.FormatSize(_totalSize),
            Formatter.FormatSize(BackupManager.SizeLimit)
        );

    // Picking Backups To Delete, The Newest Never Among Them
    private bool _deleting;
    private readonly HashSet<SavegameBackup> _marked = [];

    private bool IsPicked(SavegameBackup backup)
    {
        return _deleting ? _marked.Contains(backup) : backup == _selected;
    }

    private string RowClass(SavegameBackup backup)
    {
        var picked = IsPicked(backup) ? " list-row-selected" : "";
        var locked = _deleting && backup == Backups[0] ? " not-held" : "";
        return "list-row px-4 py-2" + picked + locked;
    }

    private string RowIcon(SavegameBackup backup)
    {
        if (!_deleting)
        {
            return Icons.Material.Rounded.History;
        }

        if (backup == Backups[0])
        {
            return Icons.Material.Rounded.Lock;
        }

        return _marked.Contains(backup)
            ? Icons.Material.Rounded.CheckBox
            : Icons.Material.Rounded.CheckBoxOutlineBlank;
    }

    private string DetailOf(SavegameBackup backup)
    {
        // Size, Then Newest Or Oldest Where It Is One
        var age =
            backup == Backups[0] ? AppText.Get("Newest")
            : backup == Backups[^1] ? AppText.Get("Oldest")
            : null;
        var size = Formatter.FormatSize(backup.Size);
        return age is null ? size : $"{size} · {age}";
    }

    private async Task ClickAsync(SavegameBackup backup)
    {
        if (!_deleting)
        {
            // A Second Click Lets The Backup Go Again
            if (backup == _selected)
            {
                _selected = null;
                return;
            }

            await SelectAsync(backup);
            return;
        }

        if (backup != Backups[0] && !_marked.Remove(backup))
        {
            _marked.Add(backup);
        }
    }

    private void StartDeleting()
    {
        _deleting = true;
        _marked.Clear();
    }

    private void StopDeleting()
    {
        _deleting = false;
        _marked.Clear();
    }

    private void MarkAllButNewest()
    {
        _marked.UnionWith(Backups.Skip(1));
    }

    private async Task DeleteMarkedAsync()
    {
        if (
            !await DialogService.ConfirmAsync(
                AppText.Format("DeleteCount", _marked.Count),
                AppText.Format("DeleteBackupsConfirm", _marked.Count)
            )
        )
        {
            return;
        }

        // The Newest Stays Whatever Was Marked, One Locked Folder Never Stops The Rest
        var failed = 0;
        foreach (var backup in _marked.Where(backup => backup != Backups[0]))
        {
            try
            {
                BackupManager.Delete(backup.Path);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                s_logger.Error(exception, "Failed To Delete Backup");
                failed++;
            }
        }

        Backups = BackupManager.List(SavegamePath);
        _totalSize = BackupManager.TotalSize();
        if (_selected is { } selected && !Backups.Exists(backup => backup.Path == selected.Path))
        {
            _selected = null;
        }

        StopDeleting();
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
