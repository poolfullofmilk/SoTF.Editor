using System.Globalization;
using NLog;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Services.Savegames;

public static class SavegameManager
{
    private const string SinglePlayerFolder = "SinglePlayer";

    // Ten Digits Below The Largest Int, The Game Skips Larger
    private const int FirstSaveId = 1_000_000_000;

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static List<Savegame> GetSavegames()
    {
        var savesPath = GetSavePath();
        s_logger.Info($"Started Reading Savegames From {savesPath}");
        if (!Directory.Exists(savesPath))
        {
            return [];
        }

        try
        {
            return
            [
                .. new DirectoryInfo(savesPath)
                    .GetFiles("SaveData.*", SearchOption.AllDirectories)
                    .Select(file => Load(file.DirectoryName))
                    .OfType<Savegame>()
                    .OrderByDescending(savegame => savegame.LastSaveTime)
                    .DistinctBy(savegame => savegame.FullPath),
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, $"Failed To Read Savegames From {savesPath}");
        }

        return [];
    }

    public static string GetSavePath()
    {
        return !string.IsNullOrWhiteSpace(UserSettings.Current.SavegamePath)
            ? UserSettings.Current.SavegamePath
            : GetSavegamePathFromAppData();
    }

    public static Savegame? Load(string? path)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }

        var directory = new DirectoryInfo(path);
        return new Savegame(
            directory.FullName,
            directory.Name,
            new SavegameStore(directory.FullName)
        );
    }

    public static string CopyToSinglePlayer(Savegame savegame)
    {
        // The Steam Id Folder Gets A New SinglePlayer Save Id
        var source = new DirectoryInfo(savegame.FullPath);
        var steamFolder =
            source.Parent?.Parent
            ?? throw new IOException($"No Steam Id Folder Above {source.FullName}");
        string target;
        do
        {
            target = Path.Combine(
                steamFolder.FullName,
                SinglePlayerFolder,
                Random.Shared.Next(FirstSaveId, int.MaxValue).ToString(CultureInfo.InvariantCulture)
            );
        } while (Directory.Exists(target) || File.Exists(target));

        Directory.CreateDirectory(target);
        try
        {
            foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
            {
                var copy = Path.Combine(
                    target,
                    Path.GetRelativePath(source.FullName, file.FullName)
                );
                BackupManager.CopyVerified(file.FullName, copy);
                File.SetLastWriteTimeUtc(copy, file.LastWriteTimeUtc);
            }

            // The Copy Keeps The Save Time Its Card Shows
            Directory.SetLastWriteTimeUtc(target, source.LastWriteTimeUtc);
        }
        catch
        {
            // A Failed Copy Leaves No Half Savegame Behind
            Directory.Delete(target, true);
            throw;
        }

        s_logger.Info($"Copied {source.FullName} To {target}");
        return target;
    }

    private static string GetSavegamePathFromAppData()
    {
        // LocalLow Sits Beside Local, Windows Names No Folder For It
        var appDataPath = Path.GetDirectoryName(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        );
        return Path.Combine(appDataPath ?? "", "LocalLow", "Endnight", "SonsOfTheForest", "Saves");
    }
}
