using NLog;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Services.Savegames;

public static class SavegameManager
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static Dictionary<string, Savegame> GetSavegames()
    {
        var savesPath = GetSavePath();
        s_logger.Info($"Detected Savegame Folder {savesPath}");
        return FindSaveGames(savesPath);
    }

    public static string GetSavePath()
    {
        return !string.IsNullOrWhiteSpace(UserSettings.Current.SavegamePath)
            ? UserSettings.Current.SavegamePath
            : GetSavegamePathFromAppData();
    }

    private static Dictionary<string, Savegame> FindSaveGames(string savesPath)
    {
        s_logger.Info($"Started Reading Savegames From {savesPath}");
        if (!Directory.Exists(savesPath))
        {
            return [];
        }

        try
        {
            var fileInfos = new DirectoryInfo(savesPath).GetFiles(
                "SaveData.*",
                SearchOption.AllDirectories
            );
            return fileInfos
                .Select(file => CreateSaveInfo(file.Directory))
                .OfType<Savegame>()
                .OrderByDescending(savegame => savegame.LastSaveTime)
                .DistinctBy(savegame => savegame.FullPath)
                .ToDictionary(savegame => savegame.FullPath, savegame => savegame);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, $"Failed To Read Savegames From {savesPath}");
        }

        return [];
    }

    public static Savegame? CreateSaveInfo(DirectoryInfo? directory)
    {
        return directory is { Exists: true }
            ? new Savegame(
                directory.FullName,
                directory.Name,
                new SavegameStore(directory.FullName)
            )
            : null;
    }

    private static string GetSavegamePathFromAppData()
    {
        // LocalLow Sits Beside Local, Windows Names No Folder For It
        var appDataPath = Path.GetDirectoryName(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        );
        return Path.Combine(appDataPath ?? "", "LocalLow", "Endnight", "SonsOfTheForest", "Saves");
    }

    public static Savegame? ReloadSavegame(Savegame selectedSavegame)
    {
        return CreateSaveInfo(new DirectoryInfo(selectedSavegame.FullPath));
    }
}
