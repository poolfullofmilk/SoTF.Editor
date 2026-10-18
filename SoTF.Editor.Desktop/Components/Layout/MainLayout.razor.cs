using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
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
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly PageLink[] s_pages =
    [
        new("/", "Savegames", Icons.Material.Rounded.FolderOpen, false),
        new("Player", "Player", Icons.Material.Rounded.Person, true),
        new("Inventory", "Inventory", Icons.Material.Rounded.Backpack, true),
        new("Storage", "Storage", Icons.Material.Rounded.Inventory2, true),
        new("Followers", "Followers", Icons.Material.Rounded.Group, true),
        new("Game", "Game", Icons.Material.Rounded.SportsEsports, true),
        new("Npcs", "Npcs", Icons.Material.Rounded.Pets, true),
        new("Structures", "Structures", Icons.Material.Rounded.Cabin, true),
        new("Map", "Map", Icons.Material.Rounded.Map, true),
    ];

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required GameStatePageViewModel GameStatePage { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private ErrorBoundary? _errorBoundary;
    private bool _isBusy;

    private bool HasSavegame => MainViewModel.SelectedSavegame != null;

    // A Client Save Holds Only The Player, Not The World
    private bool HasWorld => MainViewModel.SelectedSavegame?.IsMultiPlayerClient() == false;

    private static bool CheckForUpdatesOnStart => UserSettings.Current.CheckForUpdates;

    protected override void OnInitialized()
    {
        MainViewModel.Changed += OnChanged;
        MainViewModel.OpenRequested += OnOpenRequested;
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs eventArgs)
    {
        StateHasChanged();
    }

    private bool IsActive(PageLink link)
    {
        var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri).Split('?')[0];
        return string.Equals(path, link.Href.Trim('/'), StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && UserSettings.Current.CheckForUpdates)
        {
            await CheckForUpdatesAsync(false);
        }
    }

    private void OnChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private string SavegameLabel(Savegame savegame)
    {
        // The Same Names The Savegames Page Groups By
        var mode =
            savegame.IsMultiPlayer() ? "Multiplayer"
            : savegame.IsMultiPlayerClient() ? "MultiplayerClient"
            : "Singleplayer";
        return $"{savegame.Title} · {Localizer[mode]} · {Formatter.FormatDateTime(savegame.LastSaveTime)}";
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
                Localizer,
                Localizer["OverwriteExternalChangesTitle"],
                Localizer["OverwriteExternalChangesText"]
            )
        )
        {
            return;
        }

        try
        {
            var saved = false;
            await RunBusyAsync(() => saved = MainViewModel.SaveChanges());
            if (saved)
            {
                Snackbar.Add(Localizer["ChangesSaved"], Severity.Success);
            }
            else
            {
                Snackbar.Add(Localizer["NoChanges"], Severity.Info);
            }
        }
        catch (Exception exception)
        {
            s_logger.Error(exception, "Failed To Save");
            Snackbar.Add(Localizer["UnableToSave", exception.Message], Severity.Error);
        }
    }

    private void OnOpenRequested(Savegame savegame)
    {
        _ = InvokeAsync(() => OpenAsync(savegame));
    }

    private async Task OpenAsync(Savegame savegame)
    {
        if (!await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel))
        {
            return;
        }

        await RunBusyAsync(() => MainViewModel.SelectSavegame(savegame));
        if (IsActive(s_pages[0]))
        {
            NavigationManager.NavigateTo("Player");
        }
    }

    private async Task ReloadAsync()
    {
        if (await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel))
        {
            await RunBusyAsync(MainViewModel.ReloadSavegame);
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
            var folder = BackupManager.FolderOf(savegame.FullPath);
            Directory.CreateDirectory(folder);
            OpenInExplorer(folder);
        }
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
            Snackbar.Add(Localizer["NoBackupFound"], Severity.Info);
            return;
        }

        var dialog = await DialogService.ShowAsync<RestoreBackupDialog>(
            (string?)null,
            new DialogParameters<RestoreBackupDialog>
            {
                { restoreDialog => restoreDialog.Backups, backups },
            },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
        if (
            await dialog.Result is not { Canceled: false, Data: SavegameBackup backup }
            || !await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel)
        )
        {
            return;
        }

        try
        {
            await RunBusyAsync(() =>
            {
                BackupManager.Restore(savegame.FullPath, backup.Path);
                MainViewModel.ReloadSavegame();
            });
            Snackbar.Add(
                Localizer["RestoredBackup", Formatter.FormatDateTimeWithSeconds(backup.TakenAt)],
                Severity.Success
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, "Failed To Restore Backup");
            Snackbar.Add(Localizer["UnableToRestoreBackup"], Severity.Error);
        }
    }
    #endregion

    #region Tool Methods
    private async Task RunToolAsync(string labelKey, string textKey, Func<string> tool)
    {
        if (!await DialogService.ConfirmAsync(Localizer, Localizer[labelKey], Localizer[textKey]))
        {
            return;
        }

        var message = tool();
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }

        MainViewModel.NotifyChanged();
    }

    private Task SetToEndgameAsync()
    {
        return RunToolAsync(
            "SetToEndgame",
            "SetToEndgameText",
            () =>
            {
                GameStatePage.SetToEndgame();
                return Localizer["ChangesAdded"];
            }
        );
    }

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

    private static void ToggleCheckForUpdates()
    {
        UserSettings.Current.CheckForUpdates = !UserSettings.Current.CheckForUpdates;
        UserSettings.Current.Save();
    }

    private async Task CheckForUpdatesAsync(bool invokedManually)
    {
        var latest = await UpdateChecker.CheckAsync();
        if (latest is null)
        {
            if (invokedManually)
            {
                Snackbar.Add(Localizer["UnableToCheckForUpdates"], Severity.Warning);
            }

            return;
        }

        if (latest > AppInfo.Version)
        {
            await UpdateDialog.ShowAsync(DialogService, latest);
        }
        else if (invokedManually)
        {
            Snackbar.Add(Localizer["AlreadyLatestVersion"], Severity.Success);
        }
    }
    #endregion

    public void Dispose()
    {
        MainViewModel.Changed -= OnChanged;
        MainViewModel.OpenRequested -= OnOpenRequested;
        NavigationManager.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }

    private sealed record PageLink(string Href, string LabelKey, string Icon, bool NeedsSavegame);
}
