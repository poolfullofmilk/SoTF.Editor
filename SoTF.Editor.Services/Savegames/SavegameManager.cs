using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using Microsoft.VisualBasic.FileIO;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Services.Settings;
using SearchOption = System.IO.SearchOption;

namespace SoTF.Editor.Services.Savegames;

public static class SavegameManager
{
    public const string SinglePlayerFolder = "SinglePlayer";
    public const string MultiplayerFolder = "Multiplayer";

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

        // One Unreadable Folder Never Empties The Whole List
        return
        [
            .. Directory
                .EnumerateFiles(
                    savesPath,
                    "SaveData.*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                    }
                )
                .Select(Path.GetDirectoryName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(TryLoad)
                .OfType<Savegame>()
                .OrderByDescending(savegame => savegame.LastSaveTime),
        ];
    }

    private static Savegame? TryLoad(string? path)
    {
        try
        {
            return Load(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            s_logger.Error(exception, $"Failed To Read Savegame {path}");
            return null;
        }
    }

    public static bool IsGameRunning()
    {
        // Its Next Save Would Write Over What The Editor Saved
        var processes = Process.GetProcessesByName("SonsOfTheForest");
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
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

    public static bool IsCopy(Savegame savegame)
    {
        return UserSettings.Current.CopiedSavegames.Contains(
            savegame.FullPath,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public static string Duplicate(Savegame savegame, string modeFolder)
    {
        // A Joined Save Holds No World To Play On
        if (savegame.IsMultiPlayerClient())
        {
            throw new IOException($"Cannot Duplicate Joined Save {savegame.FullPath}");
        }

        // A New Save Id In That Mode's Folder
        var source = new DirectoryInfo(savegame.FullPath);
        var steamFolder =
            source.Parent?.Parent
            ?? throw new IOException($"No Steam Id Folder Above {source.FullName}");
        string target;
        do
        {
            target = Path.Combine(
                steamFolder.FullName,
                modeFolder,
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

        // Only Copies The Editor Made Can Be Deleted
        UserSettings.Current.CopiedSavegames.Add(target);
        UserSettings.Current.Save();
        s_logger.Info($"Copied {source.FullName} To {target}");
        return target;
    }

    public static string NewExportPath(string name, string extension)
    {
        // An Export Never Overwrites, A Second One Gets A Number
        Directory.CreateDirectory(AppInfo.ExportsDirectory);
        var safeName = string.Concat(
            name.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character
            )
        );
        // Invariant, Or A Thai Windows Names The Year 2569
        var stem = Path.Combine(
            AppInfo.ExportsDirectory,
            $"{safeName} {DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture)}"
        );
        var target = stem + extension;
        for (var number = 2; File.Exists(target); number++)
        {
            target = $"{stem} {number}{extension}";
        }

        return target;
    }

    public static string Export(Savegame savegame)
    {
        // The Save Id Folder Inside, Ready For A Mode Folder
        var target = NewExportPath(savegame.Name, ".zip");
        ZipFile.CreateFromDirectory(
            savegame.FullPath,
            target,
            CompressionLevel.Optimal,
            includeBaseDirectory: true
        );
        s_logger.Info($"Exported {savegame.FullPath} To {target}");
        return target;
    }

    public static void DeleteCopy(Savegame savegame)
    {
        if (!IsCopy(savegame))
        {
            throw new IOException($"Not A Copy {savegame.FullPath}");
        }

        // To The Recycle Bin, With The Game's Own Backups
        var directory = new DirectoryInfo(savegame.FullPath);
        FileSystem.DeleteDirectory(
            directory.FullName,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin
        );
        foreach (
            var gameBackup in directory.Parent?.GetFiles($"{directory.Name}_backup_*.zip") ?? []
        )
        {
            FileSystem.DeleteFile(
                gameBackup.FullName,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin
            );
        }

        // Still Listed, Steam Cloud May Bring It Back
        s_logger.Info($"Moved Copy {directory.FullName} To The Recycle Bin");
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
