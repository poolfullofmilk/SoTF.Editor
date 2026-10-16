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
        var savesPath = !string.IsNullOrWhiteSpace(UserSettings.Current.SavegamePath)
            ? UserSettings.Current.SavegamePath
            : GetSavegamePathFromAppData();
        return savesPath;
    }

    private static Dictionary<string, Savegame> FindSaveGames(
        string? savesPath,
        string? idFilter = null
    )
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
                .Where(savegame => savegame != null)
                .Select(savegame => savegame!)
                .Where(savegame => idFilter == null || idFilter == savegame.FullPath)
                .OrderByDescending(savegame => savegame.LastSaveTime)
                .DistinctBy(savegame => savegame.FullPath)
                .ToDictionary(savegame => savegame.FullPath, savegame => savegame);
        }
        catch (Exception exception)
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

    public static string GetSavegamePathFromAppData()
    {
        var localLowPath = Environment
            .GetFolderPath(Environment.SpecialFolder.ApplicationData)
            .Replace("Roaming", "LocalLow");
        return Path.Combine(localLowPath, "Endnight", "SonsOfTheForest", "Saves");
    }

    public static Savegame? ReloadSavegame(Savegame selectedSavegame)
    {
        var directoryInfo = new DirectoryInfo(selectedSavegame.FullPath);

        return !directoryInfo.Exists ? null : CreateSaveInfo(directoryInfo);
    }
}
