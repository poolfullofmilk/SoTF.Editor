using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
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

    private bool HasSavegame => MainViewModel.SelectedSavegame != null;

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

    private static string SavegameLabel(Savegame savegame)
    {
        return $"{savegame.Title} · {savegame.PrintableType} · {Formatter.FormatDateTime(savegame.LastSaveTime)}";
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
            if (MainViewModel.SaveChanges())
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

        MainViewModel.SelectSavegame(savegame);
        if (IsActive(s_pages[0]))
        {
            NavigationManager.NavigateTo("Player");
        }
    }

    private async Task ReloadAsync()
    {
        if (await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel))
        {
            MainViewModel.ReloadSavegame();
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

    private async Task RestoreBackupAsync(bool newest)
    {
        if (
            MainViewModel.SelectedSavegame is not { } savegame
            || !await DialogService.ConfirmAsync(
                Localizer,
                Localizer["RestoreBackupTitle"],
                Localizer["RestoreBackupText"]
            )
        )
        {
            return;
        }

        try
        {
            if (savegame.SavegameStore.RestoreBackup(newest) is not { } takenAt)
            {
                Snackbar.Add(Localizer["NoBackupFound"], Severity.Info);
                return;
            }

            MainViewModel.ReloadSavegame();
            Snackbar.Add(
                Localizer["RestoredBackup", Formatter.FormatDateTime(takenAt)],
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
    private void RunTool(Func<string> tool)
    {
        var message = tool();
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }

        MainViewModel.NotifyChanged();
    }

    private void SetToEndgame()
    {
        GameStatePage.SetToEndgame();
        Snackbar.Add(Localizer["ChangesAdded"], Severity.Info);
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
            Snackbar.Add(
                Localizer["UpdateAvailable", latest.ToString(2)],
                Severity.Info,
                options =>
                {
                    options.Action = Localizer["Download"];
                    options.RequireInteraction = true;
                    options.OnClick = _ =>
                    {
                        Process.Start(
                            new ProcessStartInfo(AppInfo.LatestReleaseUrl)
                            {
                                UseShellExecute = true,
                            }
                        );
                        return Task.CompletedTask;
                    };
                }
            );
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
