using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Integration;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class UpdateDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Parameter]
    public required ReleaseInfo Release { get; set; }

    private bool _isInstalling;
    private List<string> _notes = [];

    protected override async Task OnInitializedAsync()
    {
        _notes = await Updater.NotesAsync(Release.Tag);
    }

    public static Task ShowAsync(IDialogService dialogService, ReleaseInfo release)
    {
        return dialogService.ShowAsync<UpdateDialog>(
            null,
            new DialogParameters<UpdateDialog> { { dialog => dialog.Release, release } },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
    }

    private async Task InstallAsync()
    {
        // The Button Stays Blue, So Ignore A Second Click
        if (_isInstalling || !await DialogService.ConfirmDiscardAsync(MainViewModel))
        {
            return;
        }

        // A Debug Build Has No Release Exe To Replace
        if (!Updater.CanInstall)
        {
            OpenReleasePage();
            return;
        }

        // A Running Update Cannot Be Closed Halfway
        _isInstalling = true;
        await SetClosableAsync(false);
        try
        {
            Updater.Launch(await Updater.DownloadAsync(Release));

            // The New Exe Waits, Edits Were Already Discarded
            if (Application.Current.MainWindow is MainWindow window)
            {
                window.ClosesWithoutAsking = true;
                _ = window.Dispatcher.BeginInvoke(window.Close);
            }
        }
        catch (InvalidDataException exception)
        {
            // An Unverified Download Never Runs, The Release Page Opens Instead
            s_logger.Error(exception, "Failed To Verify Update");
            Snackbar.Add(AppText.Get("UpdateNotVerified"), Severity.Warning);
            OpenReleasePage();
        }
        catch (UnauthorizedAccessException exception)
        {
            // A Protected Folder Like Program Files Takes A Manual Download
            s_logger.Error(exception, "Failed To Update, The App Folder Is Protected");
            Snackbar.Add(AppText.Get("UpdateFolderProtected"), Severity.Warning);
            OpenReleasePage();
        }
        catch (Exception exception)
            when (exception
                    is HttpRequestException
                        or IOException
                        or TaskCanceledException
                        or Win32Exception
            )
        {
            s_logger.Error(exception, "Failed To Update");
            Snackbar.Add(AppText.Get("UpdateFailed"), Severity.Error);
            _isInstalling = false;
            await SetClosableAsync(true);
        }
    }

    private Task SetClosableAsync(bool closable)
    {
        return MudDialog.SetOptionsAsync(
            MudDialog.Options with
            {
                CloseButton = closable,
                CloseOnEscapeKey = closable,
            }
        );
    }

    private void OpenReleasePage()
    {
        Process.Start(new ProcessStartInfo(AppInfo.LatestReleaseUrl) { UseShellExecute = true });
        MudDialog.Close();
    }
}
