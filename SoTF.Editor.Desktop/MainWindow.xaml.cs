using System.ComponentModel;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using NLog;
using SoTF.Editor.Desktop.Interop;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop;

public partial class MainWindow : Window
{
    private const string DropMessage = "SavegameDropped";

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly MainViewModel _mainViewModel;

    // Windows Shutting Down Or An Update Restarting
    public bool ClosesWithoutAsking { get; set; }

    public MainWindow(IServiceProvider services, MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        WebView.Services = services;
        WebView.BlazorWebViewInitialized += (_, eventArgs) =>
            SetUpBrowser(eventArgs.WebView.CoreWebView2);
        SourceInitialized += (_, _) => WindowCaptionTheme.Apply(this);
        Closing += ConfirmClose;
    }

    private void ConfirmClose(object? sender, CancelEventArgs eventArgs)
    {
        // Windows Shutting Down Is Never Held Up
        if (ClosesWithoutAsking)
        {
            return;
        }

        bool hasUnsavedChanges;
        try
        {
            hasUnsavedChanges = _mainViewModel.HasUnsavedChanges();
        }
        catch (Exception exception)
        {
            // A Failed Check Asks Anyway, Edits Are Never Dropped Unasked
            s_logger.Error(exception, "Failed To Check For Unsaved Changes");
            hasUnsavedChanges = true;
        }

        eventArgs.Cancel =
            hasUnsavedChanges
            && MessageBox.Show(
                this,
                AppText.Get("DiscardChangesText"),
                AppText.Get("DiscardChangesTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            ) != MessageBoxResult.Yes;
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void SetUpBrowser(CoreWebView2 browser)
    {
        // Page Background Before The First Paint, No White Flash
        WebView.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x20, 0x20, 0x20);

        // Browser Keys Like F5 And Ctrl P Stay Off
        browser.Settings.AreBrowserAcceleratorKeysEnabled = false;

        // Ctrl Wheel And Touchpad Pinch Never Resize The Whole App
        browser.Settings.IsZoomControlEnabled = false;
        browser.Settings.IsPinchZoomEnabled = false;

        browser.AddWebResourceRequestedFilter(
            LocalFiles.Host + "*",
            CoreWebView2WebResourceContext.Image
        );
        browser.WebResourceRequested += (_, eventArgs) => ServeLocalFile(browser, eventArgs);
        browser.WebMessageReceived += (_, eventArgs) => OpenDroppedSavegame(eventArgs);
    }

    private static void ServeLocalFile(
        CoreWebView2 browser,
        CoreWebView2WebResourceRequestedEventArgs eventArgs
    )
    {
        // Requests For Other Hosts Belong To Blazor Or The Web
        if (LocalFiles.PathOf(eventArgs.Request.Uri) is not { } path)
        {
            return;
        }

        // Serve Only Savegame Thumbnails
        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null,
        };
        if (contentType == null || ReadOrNull(path) is not { } bytes)
        {
            eventArgs.Response = browser.Environment.CreateWebResourceResponse(
                null,
                404,
                "Not Found",
                ""
            );
            return;
        }

        eventArgs.Response = browser.Environment.CreateWebResourceResponse(
            new MemoryStream(bytes),
            200,
            "OK",
            $"Content-Type: {contentType}"
        );
    }

    private static byte[]? ReadOrNull(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A Thumbnail Mid Write Shows The Placeholder
            return null;
        }
    }

    private void OpenDroppedSavegame(CoreWebView2WebMessageReceivedEventArgs eventArgs)
    {
        // Blazor Shares This Channel, Its Own Messages Pass Untouched
        if (
            eventArgs.TryGetWebMessageAsString() != DropMessage
            || eventArgs.AdditionalObjects.OfType<CoreWebView2File>().FirstOrDefault()?.Path
                is not { } path
        )
        {
            return;
        }

        // Only A Folder Holding Save Data Opens
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (
            directory == null
            || !Directory.Exists(directory)
            || Directory.GetFiles(directory, "SaveData.*").Length == 0
        )
        {
            s_logger.Info($"Ignored Dropped Folder Without Save Data {directory}");
            return;
        }

        if (SavegameManager.CreateSaveInfo(new DirectoryInfo(directory)) is { } savegame)
        {
            _mainViewModel.RequestOpen(savegame);
        }
    }
}
