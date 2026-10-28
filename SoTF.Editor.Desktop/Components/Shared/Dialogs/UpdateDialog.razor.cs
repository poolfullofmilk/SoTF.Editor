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

    // Bytes So Far, Zero Total While No Length Is Sent
    private long _received;
    private long _total;
    private int _percent;

    private string ProgressText =>
        _total > 0 && _received >= _total ? AppText.Get("StartingUpdate")
        : _total > 0
            ? AppText.Format(
                "DownloadedOf",
                Formatter.FormatSize(_received),
                Formatter.FormatSize(_total)
            )
        : Formatter.FormatSize(_received);

    private static MainWindow? Window => Application.Current.MainWindow as MainWindow;

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
        Window?.IsUpdating = true;
        await SetClosableAsync(false);
        try
        {
            Updater.Launch(
                await Updater.DownloadAsync(Release, new Progress<(long, long)>(ShowProgress))
            );

            // The New Exe Waits, Edits Were Already Discarded
            if (Window is { } window)
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
        finally
        {
            Window?.IsUpdating = false;
        }
    }

    private void ShowProgress((long Received, long Total) progress)
    {
        // Redraws Once Per Percent, Not Per Chunk
        var (received, total) = progress;
        var percent = total > 0 ? (int)(received * 100 / total) : 0;
        var moved =
            total != _total || (total > 0 ? percent != _percent : received - _received >= 1048576);
        if (!moved)
        {
            return;
        }

        _received = received;
        _total = total;
        _percent = percent;
        _ = InvokeAsync(StateHasChanged);
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
