using Microsoft.Win32;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Home : EditorPageBase
{
    // Empty While The Folder Is Still Being Read
    private List<Savegame>? _savegames;
    private int _scan;

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
        var savegames = await Task.Run(() =>
        {
            var found = SavegameManager.GetSavegames();
            found.ForEach(savegame => _ = savegame.Summary);
            return found;
        });
        if (scan == _scan)
        {
            _savegames = savegames;
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
        return string.Join(
            " · ",
            new[]
            {
                AppText.Format("SavegameDay", summary.Days),
                summary.GameType is { } gameType ? AppText.Get(TextKeys.GameMode(gameType)) : null,
                summary.CrashSite is { } crashSite
                    ? AppText.Format(
                        "SavegameCrashSite",
                        AppText.Get(TextKeys.CrashSite(crashSite))
                    )
                    : null,
            }.OfType<string>()
        );
    }

    private static string PlayerText(SavegameSummary summary)
    {
        return $"{AppText.Format("SavegameStrength", summary.StrengthLevel)} · {AppText.Format("SavegameItems", summary.ItemCount)}";
    }

    private void Open(Savegame savegame)
    {
        // The Card Was Read When Listed, Opening Reads It Again
        if (SavegameManager.Load(savegame.FullPath) is { } fresh)
        {
            MainViewModel.RequestOpen(fresh);
        }
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
