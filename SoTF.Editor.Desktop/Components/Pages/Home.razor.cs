using System.ComponentModel;
using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Win32;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Home : EditorPageBase
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // Empty While The Folder Is Still Being Read
    private List<Savegame>? _savegames;
    private Dictionary<string, int> _backupCounts = [];
    private int _scan;
    private MudMenu? _duplicateMenu;
    private Savegame? _menuSavegame;

    private static string SavegamePath => SavegameManager.GetSavePath();

    private static bool IsDefaultFolder =>
        string.IsNullOrWhiteSpace(UserSettings.Current.SavegamePath);

    private IEnumerable<SavegameGroup> SavegameGroups =>
        new[]
        {
            new SavegameGroup(
                AppText.Get("Singleplayer"),
                [
                    .. (_savegames ?? []).Where(savegame =>
                        !savegame.IsMultiPlayer() && !savegame.IsMultiPlayerClient()
                    ),
                ]
            ),
            new SavegameGroup(
                AppText.Get("HostedMultiplayer"),
                [.. (_savegames ?? []).Where(savegame => savegame.IsMultiPlayer())]
            ),
            new SavegameGroup(
                AppText.Get("JoinedMultiplayer"),
                [.. (_savegames ?? []).Where(savegame => savegame.IsMultiPlayerClient())],
                AppText.Get("MultiplayerClientInfo")
            ),
        }.Where(group => group.Savegames.Count > 0);

    protected override async Task OnInitializedAsync()
    {
        await LoadSavegamesAsync();
    }

    private async Task LoadSavegamesAsync()
    {
        // A Slow Disk Never Holds The Window, Newer Scans Win
        var scan = ++_scan;
        _savegames = null;
        var (savegames, backupCounts) = await Task.Run(() =>
        {
            var found = SavegameManager.GetSavegames();
            found.ForEach(savegame => _ = savegame.Summary);
            return (
                found,
                found.ToDictionary(
                    savegame => savegame.FullPath,
                    savegame => BackupManager.Count(savegame.FullPath)
                )
            );
        });
        if (scan == _scan)
        {
            _savegames = savegames;
            _backupCounts = backupCounts;
        }
    }

    private bool IsOpen(Savegame savegame)
    {
        return MainViewModel.SelectedSavegame?.FullPath == savegame.FullPath;
    }

    private string CardClass(Savegame savegame)
    {
        // The Open Savegame Gets The Accent Ring
        var selected = IsOpen(savegame) ? " card-open" : "";
        return "rounded-panel overflow-hidden hover-card" + selected;
    }

    private static string WorldText(SavegameSummary summary)
    {
        // Day, Mode And Crash Site Tell Worlds Apart
        return Formatter.JoinParts(
            AppText.Format("SavegameDay", summary.Days),
            summary.GameType is { } gameType ? AppText.Get(TextKeys.GameMode(gameType)) : null,
            summary.CrashSite is { } crashSite
                ? AppText.Format("SavegameCrashSite", AppText.Get(TextKeys.CrashSite(crashSite)))
                : null
        );
    }

    private static string PlayerText(SavegameSummary summary)
    {
        return $"{AppText.Format("SavegameStrength", summary.StrengthLevel)} · {AppText.Format("SavegameItems", summary.ItemCount)}";
    }

    private async Task OpenAsync(Savegame savegame)
    {
        // The Card Was Read When Listed, Opening Reads It Again
        if (SavegameManager.Load(savegame.FullPath) is { } fresh)
        {
            MainViewModel.RequestOpen(fresh);
            return;
        }

        // Gone Since The List Was Read, The List Catches Up
        Snackbar.Add(AppText.Get("SavegameGone"), Severity.Warning);
        await LoadSavegamesAsync();
    }

    private int BackupCountOf(Savegame savegame)
    {
        return _backupCounts.GetValueOrDefault(savegame.FullPath);
    }

    private static string BackupsTitle(int count)
    {
        return AppText.Format(
            "RestoreBackupOf",
            AppText.Format(count == 1 ? "OneBackup" : "BackupCount", count)
        );
    }

    private async Task OpenDuplicateMenuAsync(MouseEventArgs args, Savegame savegame)
    {
        // One Menu For Every Card, Opened At The Button
        _menuSavegame = savegame;
        if (_duplicateMenu is not null)
        {
            await _duplicateMenu.OpenMenuAsync(args);
        }
    }

    private void RestoreFromCard(Savegame savegame)
    {
        // A Fresh Read, Or The Outside Change Check Misfires
        if (SavegameManager.Load(savegame.FullPath) is { } fresh)
        {
            MainViewModel.RequestRestore(fresh);
        }
    }

    private async Task ExportAsync(Savegame savegame)
    {
        try
        {
            var exported = await Task.Run(() => SavegameManager.Export(savegame));
            Snackbar.Add(AppText.Get("SavegameExported"), Severity.Success);
            ShowInExplorer(exported);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Export Savegame");
            Snackbar.Add(AppText.Get("UnableToExport"), Severity.Error);
        }
    }

    private static void ShowInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\""));
        }
        catch (Win32Exception exception)
        {
            // A Shell That Fails To Open Never Fails The Export
            s_logger.Warn(exception, "Failed To Show Export");
        }
    }

    private Task DuplicateToSingleplayerAsync()
    {
        return DuplicateAsync(SavegameManager.SinglePlayerFolder, "DuplicateToSingleplayer");
    }

    private Task DuplicateToMultiplayerAsync()
    {
        return DuplicateAsync(SavegameManager.MultiplayerFolder, "DuplicateToMultiplayer");
    }

    private async Task DuplicateAsync(string modeFolder, string labelKey)
    {
        if (_menuSavegame is not { } savegame || !await ConfirmAsync(labelKey, labelKey + "Text"))
        {
            return;
        }

        try
        {
            // A Big Savegame Copies Off The Window Thread
            await Task.Run(() => SavegameManager.Duplicate(savegame, modeFolder));
            Snackbar.Add(AppText.Get("SavegameDuplicated"), Severity.Success);
            await LoadSavegamesAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Copy Savegame");
            Snackbar.Add(AppText.Get("UnableToCopySavegame"), Severity.Error);
        }
    }

    private async Task DeleteCopyAsync(Savegame savegame)
    {
        var isOpen = IsOpen(savegame);
        if (
            isOpen && !await DialogService.ConfirmDiscardAsync(MainViewModel)
            || !await ConfirmAsync("DeleteCopy", "DeleteCopyText")
        )
        {
            return;
        }

        try
        {
            // An Open Copy Closes First, Its Files Are Going
            if (isOpen)
            {
                MainViewModel.SelectSavegame(null);
            }

            SavegameManager.DeleteCopy(savegame);
            Snackbar.Add(AppText.Get("CopyDeleted"), Severity.Success);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or OperationCanceledException
            )
        {
            s_logger.Error(exception, "Failed To Delete Savegame Copy");
            Snackbar.Add(AppText.Get("UnableToDeleteCopy"), Severity.Error);
        }

        await LoadSavegamesAsync();
    }

    private async Task ChangeFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = AppText.Get("SelectSavesFolder"),
            InitialDirectory = Directory.Exists(SavegamePath) ? SavegamePath : "",
        };

        if (dialog.ShowDialog() == true)
        {
            UserSettings.Current.SavegamePath = dialog.FolderName;
            UserSettings.Current.Save();
            await LoadSavegamesAsync();
        }
    }

    private async Task SetDefaultFolderAsync()
    {
        UserSettings.Current.SavegamePath = "";
        UserSettings.Current.Save();
        await LoadSavegamesAsync();
    }

    private sealed record SavegameGroup(
        string Title,
        List<Savegame> Savegames,
        string? Info = null
    );
}
