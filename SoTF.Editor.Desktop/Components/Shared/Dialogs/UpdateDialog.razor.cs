using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Services.Integration;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class UpdateDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Parameter]
    public required Version Version { get; set; }

    private bool _isInstalling;

    private static bool CheckForUpdatesOnStart => UserSettings.Current.CheckForUpdates;

    public static Task ShowAsync(IDialogService dialogService, Version version)
    {
        return dialogService.ShowAsync<UpdateDialog>(
            null,
            new DialogParameters<UpdateDialog> { { dialog => dialog.Version, version } },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
    }

    private static void SetCheckForUpdatesOnStart(bool checkOnStart)
    {
        UserSettings.Current.CheckForUpdates = checkOnStart;
        UserSettings.Current.Save();
    }

    private async Task InstallAsync()
    {
        // The Button Stays Blue, So Ignore A Second Click
        if (_isInstalling || !await DialogService.ConfirmDiscardAsync(Localizer, MainViewModel))
        {
            return;
        }

        // A Debug Build Has No Release Exe To Replace
        if (!Updater.CanInstall)
        {
            Process.Start(
                new ProcessStartInfo(AppInfo.LatestReleaseUrl) { UseShellExecute = true }
            );
            MudDialog.Close();
            return;
        }

        _isInstalling = true;
        try
        {
            Updater.Launch(await Updater.DownloadAsync(Version));

            // The New Exe Waits, Edits Were Already Discarded
            if (Application.Current.MainWindow is MainWindow window)
            {
                window.ClosesWithoutAsking = true;
                _ = window.Dispatcher.BeginInvoke(window.Close);
            }
        }
        catch (Exception exception)
            when (exception
                    is HttpRequestException
                        or IOException
                        or UnauthorizedAccessException
                        or TaskCanceledException
                        or Win32Exception
            )
        {
            s_logger.Error(exception, "Failed To Update");
            Snackbar.Add(Localizer["UpdateFailed"], Severity.Error);
            _isInstalling = false;
        }
    }
}
