using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Integration;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Layout;

public partial class MainLayout : IDisposable
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    // Savegames, Then You, Then The World, Then The Game Itself
    private static readonly PageLink[][] s_pageGroups =
    [
        [new("/", "Savegames", Icons.Material.Rounded.Folder, false)],
        [
            new("Player", "Player", Icons.Material.Rounded.Person, true),
            new("Inventory", "Inventory", Icons.Material.Rounded.Backpack, true),
            new("Followers", "Followers", Icons.Material.Rounded.Group, true, true),
        ],
        [
            new("Storage", "Storage", Icons.Material.Rounded.Inventory2, true, true),
            new("Structures", "Structures", Icons.Material.Rounded.Cabin, true, true),
            new("Npcs", "Npcs", Icons.Material.Rounded.Pets, true, true),
            new("Map", "Map", Icons.Material.Rounded.Map, true),
        ],
        [
            new("Game", "Game", Icons.Material.Rounded.SportsEsports, true, true),
            new("Tools", "Tools", Icons.Material.Rounded.Handyman, true),
        ],
    ];

    private static readonly PageLink[] s_pages = [.. s_pageGroups.SelectMany(group => group)];

    public static IReadOnlyList<PageLink> Pages => s_pages;

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private ErrorBoundary? _errorBoundary;
    private bool _isBusy;
    private ReleaseInfo? _availableUpdate;

    private bool HasSavegame => MainViewModel.SelectedSavegame != null;

    private bool HasBackups =>
        MainViewModel.SelectedSavegame is { } savegame && BackupManager.HasAny(savegame.FullPath);

    private bool IsClientSave => MainViewModel.SelectedSavegame?.IsMultiPlayerClient() == true;

    // Only A Page With Unsaved Edits It Can Take Back
    private bool CanRevert(PageLink link)
    {
        return MainViewModel.HasChanges(link.Href);
    }

    // A Client Save Holds Only The Player, World Pages Stay Shut
    private bool IsDisabled(PageLink link)
    {
        return (link.NeedsSavegame && !HasSavegame)
            || (link.NeedsWorld && MainViewModel.SelectedSavegame?.IsMultiPlayerClient() == true);
    }

    // The Dot Leads From Help Down To Check For Updates
    private string? UpdateDotClass => _availableUpdate is null ? null : "update-dot";

    protected override void OnInitialized()
    {
        MainViewModel.Changed += OnChanged;
        MainViewModel.OpenRequested += OnOpenRequested;
        BackupManager.Pruned += OnBackupsPruned;
    }

    private void OnBackupsPruned(int count)
    {
        _ = InvokeAsync(() =>
            Snackbar.Add(
                AppText.Format(
                    "BackupsPruned",
                    count,
                    Formatter.FormatSize(BackupManager.SizeLimit)
                ),
                Severity.Warning
            )
        );
    }

    private bool IsActive(PageLink link)
    {
        var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri).Split('?')[0];
        return string.Equals(path, link.Href.Trim('/'), StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await CheckForUpdatesAsync(false);
        }
    }

    private void OnChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void RecoverFromError()
    {
        _errorBoundary?.Recover();
    }

    #region Savegame Methods
    private async Task SaveAsync()
    {
        if (MainViewModel.SelectedSavegame is not { } savegame)
        {
            return;
        }

        if (
            savegame.SavegameStore.HasChanged()
            && !await DialogService.ConfirmAsync(
                AppText.Get("OverwriteExternalChangesTitle"),
                AppText.Get("OverwriteExternalChangesText")
            )
        )
        {
            return;
        }

        if (await ConfirmSaveAsync() is not { } changes)
        {
            return;
        }

        try
        {
            var saved = false;
            await RunBusyAsync(() => saved = MainViewModel.SaveChanges(changes));
            if (saved)
            {
                Snackbar.Add(AppText.Get("ChangesSaved"), Severity.Success);
            }
            else
            {
                Snackbar.Add(AppText.Get("NoChanges"), Severity.Info);
            }
        }
        catch (Exception exception)
        {
            s_logger.Error(exception, "Failed To Save");
            Snackbar.Add(AppText.Format("UnableToSave", exception.Message), Severity.Error);
        }
    }

    private async Task<Dictionary<string, List<Change>>?> ConfirmSaveAsync()
    {
        // No Page Marked Saves Straight Away, The Store Decides
        var changedPages = s_pages.Where(page => MainViewModel.HasChanges(page.Href)).ToList();
        if (changedPages.Count == 0)
        {
            return [];
        }

        var dialog = await DialogService.ShowAsync<SaveChangesDialog>(
            (string?)null,
            new DialogParameters<SaveChangesDialog>
            {
                { saveDialog => saveDialog.Pages, changedPages },
            },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
        return
            await dialog.Result
                is { Canceled: false, Data: Dictionary<string, List<Change>> changes }
            ? changes
            : null;
    }

    private void OnOpenRequested(Savegame savegame)
    {
        _ = InvokeAsync(() => OpenAsync(savegame));
    }

    private async Task OpenAsync(Savegame savegame)
    {
        if (!await DialogService.ConfirmDiscardAsync(MainViewModel))
        {
            return;
        }

        if (
            !await TryBusyAsync(
                savegame,
                () => MainViewModel.SelectSavegame(savegame),
                "UnableToOpen"
            )
        )
        {
            return;
        }

        if (IsActive(s_pages[0]))
        {
            NavigationManager.NavigateTo("Player");
        }
    }

    private async Task RevertPageAsync(PageLink page)
    {
        if (
            await DialogService.ConfirmAsync(
                AppText.Get("RevertPage"),
                AppText.Format("RevertPageText", AppText.Get(page.LabelKey))
            )
        )
        {
            MainViewModel.RevertPage(page.Href);
        }
    }

    private async Task ReloadAsync()
    {
        if (
            MainViewModel.SelectedSavegame is { } savegame
            && await DialogService.ConfirmDiscardAsync(MainViewModel)
        )
        {
            await TryBusyAsync(savegame, MainViewModel.ReloadSavegame, "UnableToOpen");
        }
    }

    private async Task<bool> TryBusyAsync(Savegame savegame, Action work, string failureKey)
    {
        try
        {
            await RunBusyAsync(work);
            return true;
        }
        catch (Exception exception)
        {
            // A Half Loaded Savegame Is Closed, Its Backups Stay Reachable
            s_logger.Error(exception, "Failed To Load Savegame");
            MainViewModel.SelectSavegame(null);
            Snackbar.Add(
                AppText.Get(failureKey),
                Severity.Error,
                options =>
                {
                    options.RequireInteraction = true;
                    options.Action = AppText.Get("OpenBackupFolder");
                    options.OnClick = _ =>
                    {
                        OpenBackupFolderOf(savegame.FullPath);
                        return Task.CompletedTask;
                    };
                }
            );
            NavigationManager.NavigateTo("/");
            return false;
        }
    }

    private async Task RunBusyAsync(Action work)
    {
        // The Overlay Shows Before The Work Holds The Thread
        _isBusy = true;
        StateHasChanged();
        await Task.Yield();
        try
        {
            work();
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private void OpenSavegameFolder()
    {
        if (MainViewModel.SelectedSavegame is { } savegame)
        {
            OpenInExplorer(savegame.FullPath);
        }
    }

    private void OpenBackupFolder()
    {
        if (MainViewModel.SelectedSavegame is { } savegame)
        {
            OpenBackupFolderOf(savegame.FullPath);
        }
    }

    private static void OpenBackupFolderOf(string savegamePath)
    {
        var folder = BackupManager.FolderOf(savegamePath);
        Directory.CreateDirectory(folder);
        OpenInExplorer(folder);
    }

    private static void OpenInExplorer(string folder)
    {
        Process.Start(
            new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }
        );
    }

    private async Task RestoreBackupAsync()
    {
        if (MainViewModel.SelectedSavegame is not { } savegame)
        {
            return;
        }

        var backups = BackupManager.List(savegame.FullPath);
        if (backups.Count == 0)
        {
            return;
        }

        var dialog = await DialogService.ShowAsync<RestoreBackupDialog>(
            (string?)null,
            new DialogParameters<RestoreBackupDialog>
            {
                { restoreDialog => restoreDialog.Backups, backups },
                { restoreDialog => restoreDialog.SavegamePath, savegame.FullPath },
            },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
        if (
            await dialog.Result is not { Canceled: false, Data: SavegameBackup backup }
            || !await DialogService.ConfirmDiscardAsync(MainViewModel)
        )
        {
            return;
        }

        if (
            await TryBusyAsync(
                savegame,
                () =>
                {
                    BackupManager.Restore(savegame.FullPath, backup.Path);
                    MainViewModel.ReloadSavegame();
                },
                "UnableToRestoreBackup"
            )
        )
        {
            Snackbar.Add(
                AppText.Format(
                    "RestoredBackup",
                    Formatter.FormatDateTimeWithSeconds(backup.TakenAt)
                ),
                Severity.Success
            );
        }
    }
    #endregion

    #region Help Methods
    private async Task ShowDialogAsync<TDialog>()
        where TDialog : IComponent
    {
        var dialog = await DialogService.ShowAsync<TDialog>(
            (string?)null,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );
        await dialog.Result;
        MainViewModel.NotifyChanged();
    }

    private async Task CheckForUpdatesAsync(bool invokedManually)
    {
        var latest = await Updater.CheckAsync();
        if (latest is null)
        {
            if (invokedManually)
            {
                Snackbar.Add(AppText.Get("UnableToCheckForUpdates"), Severity.Warning);
            }

            return;
        }

        if (latest.Version > AppInfo.Version)
        {
            // The Badge Stays After The Dialog Is Dismissed
            _availableUpdate = latest;
            StateHasChanged();
            await UpdateDialog.ShowAsync(DialogService, latest);
        }
        else if (invokedManually)
        {
            Snackbar.Add(AppText.Get("AlreadyLatestVersion"), Severity.Success);
        }
    }
    #endregion

    public void Dispose()
    {
        MainViewModel.Changed -= OnChanged;
        MainViewModel.OpenRequested -= OnOpenRequested;
        BackupManager.Pruned -= OnBackupsPruned;
        GC.SuppressFinalize(this);
    }

    public sealed record PageLink(
        string Href,
        string LabelKey,
        string Icon,
        bool NeedsSavegame,
        bool NeedsWorld = false
    );
}
