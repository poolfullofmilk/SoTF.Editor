using Microsoft.Win32;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Home : EditorPageBase
{
    private List<Savegame> _savegames = [];

    private static string SavegamePath => SavegameManager.GetSavePath();

    private IEnumerable<SavegameGroup> SavegameGroups =>
        new[]
        {
            new SavegameGroup(
                Localizer["Singleplayer"],
                [
                    .. _savegames.Where(savegame =>
                        savegame.IsSinglePlayer() || savegame.HasUnknownParentDir()
                    ),
                ]
            ),
            new SavegameGroup(
                Localizer["Multiplayer"],
                [.. _savegames.Where(savegame => savegame.IsMultiPlayer())]
            ),
            new SavegameGroup(
                Localizer["MultiplayerClient"],
                [.. _savegames.Where(savegame => savegame.IsMultiPlayerClient())],
                Localizer["MultiplayerClientInfo"]
            ),
        }.Where(group => group.Savegames.Count > 0);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        LoadSavegames();
    }

    private void LoadSavegames()
    {
        _savegames = [.. SavegameManager.GetSavegames().Values];
    }

    private bool IsOpen(Savegame savegame)
    {
        return MainViewModel.SelectedSavegame?.FullPath == savegame.FullPath;
    }

    private string CardClass(Savegame savegame)
    {
        // The Open Savegame Reads As Selected
        var selected = IsOpen(savegame) ? " list-row-selected" : "";
        return "rounded-panel overflow-hidden hover-card" + selected;
    }

    private void Open(Savegame savegame)
    {
        MainViewModel.RequestOpen(savegame);
    }

    private void ChangeFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Localizer["SelectSavesFolder"],
            InitialDirectory = Directory.Exists(SavegamePath) ? SavegamePath : "",
        };

        if (dialog.ShowDialog() == true)
        {
            UserSettings.Current.SavegamePath = dialog.FolderName;
            UserSettings.Current.Save();
            LoadSavegames();
        }
    }

    private void SetDefaultFolder()
    {
        UserSettings.Current.SavegamePath = "";
        UserSettings.Current.Save();
        LoadSavegames();
    }

    private sealed record SavegameGroup(
        string Title,
        List<Savegame> Savegames,
        string? Info = null
    );
}
