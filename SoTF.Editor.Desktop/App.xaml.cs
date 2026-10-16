using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop;

public partial class App : Application
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        // Two Editors On One Save Could Overwrite Each Other
        _showSignal = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            $"{AppInfo.FolderName}.Show",
            out var isFirstInstance
        );
        if (!isFirstInstance)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }

        var logDirectory = Path.Combine(AppInfo.DocumentsDirectory, "Logs");
        LogManager
            .Setup()
            .LoadConfiguration(configuration =>
                configuration
                    .ForLogger()
                    .FilterMinLevel(NLog.LogLevel.Info)
                    .WriteToFile(Path.Combine(logDirectory, "log-${shortdate}.txt"))
            );

        try
        {
            Start();
        }
        catch (Exception exception)
        {
            // Without A Window The Process Would Linger Unseen
            s_logger.Fatal(exception, "Failed To Start");
            MessageBox.Show(
                $"{AppInfo.DisplayName} Could Not Start, See The Log In {logDirectory}",
                AppInfo.DisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Shutdown(1);
        }
    }

    private void Start()
    {
        Directory.CreateDirectory(AppInfo.DataDirectory);
        Environment.SetEnvironmentVariable(
            "WEBVIEW2_USER_DATA_FOLDER",
            Path.Combine(AppInfo.DataDirectory, "WebView2")
        );
        AppCulture.Apply(UserSettings.Current.Language);

        // A Failure In The Page Is Logged, Never Fatal
        DispatcherUnhandledException += (_, exceptionArgs) =>
        {
            exceptionArgs.Handled = true;
            s_logger.Error(exceptionArgs.Exception, "Failed In The Page");
        };
        TaskScheduler.UnobservedTaskException += (_, exceptionArgs) =>
        {
            s_logger.Error(exceptionArgs.Exception, "Failed In A Background Task");
            exceptionArgs.SetObserved();
        };

        var gameData =
            JsonConvert.DeserializeObject<GameData>(ResourceFile.Read("GameData.json"))
            ?? throw new InvalidDataException("Found No Game Data");
        CheckTextKeys(gameData);

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddSingleton<IStringLocalizer<SharedResource>, AppLocalizer>();
        services.AddMudServices(configuration =>
        {
            // Snackbars Default To Outlined Icons, Every Other Icon Is Rounded
            configuration.SnackbarConfiguration.SuccessIcon = Icons.Material.Rounded.CheckCircle;
            configuration.SnackbarConfiguration.WarningIcon = Icons.Material.Rounded.Warning;
            configuration.SnackbarConfiguration.InfoIcon = Icons.Material.Rounded.Info;
            configuration.SnackbarConfiguration.ErrorIcon = Icons.Material.Rounded.Error;
            configuration.SnackbarConfiguration.PositionClass = Defaults
                .Classes
                .Position
                .BottomRight;
        });

        services.AddSingleton(gameData);
        services.AddSingleton<StorageFactory>();
        services.AddSingleton<PlayerPageViewModel>();
        services.AddSingleton<ArmorPageViewModel>();
        services.AddSingleton<InventoryPageViewModel>();
        services.AddSingleton<StoragePageViewModel>();
        services.AddSingleton<FollowerPageViewModel>();
        services.AddSingleton<GameSetupPageViewModel>();
        services.AddSingleton<GameStatePageViewModel>();
        services.AddSingleton<WeatherPageViewModel>();
        services.AddSingleton<NpcsPageViewModel>();
        services.AddSingleton<StructuresPageViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MapViewModel>();
        services.AddSingleton<ActorModifier>();
        services.AddSingleton<MapManager>();
        services.AddSingleton<PoiLoader>();

        var serviceProvider = services.BuildServiceProvider();
        var window = new MainWindow(
            serviceProvider,
            serviceProvider.GetRequiredService<MainViewModel>()
        );
        MainWindow = window;
        ThreadPool.RegisterWaitForSingleObject(
            _showSignal!,
            (_, _) => Dispatcher.InvokeAsync(window.BringToFront),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false
        );
        window.Show();
    }

    [Conditional("DEBUG")]
    private static void CheckTextKeys(GameData gameData)
    {
        // A Missing Runtime Key Shows Its Raw Name
        var missingKeys = TextKeys.MissingKeys(gameData);
        if (missingKeys.Count > 0)
        {
            s_logger.Warn($"Found Missing Text Keys {string.Join(", ", missingKeys)}");
        }

        Debug.Assert(missingKeys.Count == 0, string.Join(", ", missingKeys));
    }
}
