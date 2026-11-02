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

    // One Unreadable Folder Never Empties The Whole List
    private static readonly EnumerationOptions s_saveDataSearch = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    public static List<Savegame> GetSavegames()
    {
        var savesPath = GetSavePath();
        s_logger.Info($"Started Reading Savegames From {savesPath}");
        if (!Directory.Exists(savesPath))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateFiles(savesPath, "SaveData.*", s_saveDataSearch)
                .Select(Path.GetDirectoryName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(TryLoad)
                .OfType<Savegame>()
                .OrderByDescending(savegame => savegame.LastSaveTime),
        ];
    }

    public static string SaveDataStamps()
    {
        // Every Game Save Rewrites A SaveData File
        var savesPath = GetSavePath();
        return Directory.Exists(savesPath)
            ? string.Join(
                "|",
                new DirectoryInfo(savesPath)
                    .EnumerateFiles("SaveData.*", s_saveDataSearch)
                    .Select(file => $"{file.FullName} {file.Length} {file.LastWriteTimeUtc.Ticks}")
            )
            : "";
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
        var custom = UserSettings.Current.SavegamePath;
        if (string.IsNullOrWhiteSpace(custom))
        {
            return GetSavegamePathFromAppData();
        }

        // A Sandbox Never Reads A Folder Picked Outside It
        if (AppInfo.SandboxRoot != null && !AppInfo.IsInSandbox(custom))
        {
            s_logger.Warn($"Ignored Saves Folder Outside The Sandbox {custom}");
            return GetSavegamePathFromAppData();
        }

        return custom;
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

    public static string CreateExport(string name, string extension, Action<Stream> write)
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
        for (var number = 1; ; number++)
        {
            // Created Exclusively, So Two Exports Never Share A Name
            var target = number == 1 ? stem + extension : $"{stem} {number}{extension}";
            FileStream stream;
            try
            {
                stream = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite);
            }
            catch (IOException) when (File.Exists(target))
            {
                continue;
            }

            try
            {
                using (stream)
                {
                    write(stream);
                }

                return target;
            }
            catch
            {
                // Only The File This Export Created Goes
                File.Delete(target);
                throw;
            }
        }
    }

    public static string Export(Savegame savegame)
    {
        // The Save Id Folder Inside, Ready For A Mode Folder
        var target = CreateExport(
            savegame.Name,
            ".zip",
            stream =>
                ZipFile.CreateFromDirectory(
                    savegame.FullPath,
                    stream,
                    CompressionLevel.Optimal,
                    includeBaseDirectory: true
                )
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

        // A Sandbox Deletes For Good, Never Touching The Recycle Bin
        var directory = new DirectoryInfo(savegame.FullPath);
        if (AppInfo.SandboxRoot != null)
        {
            directory.Delete(true);
            s_logger.Info($"Deleted Copy {directory.FullName}");
            return;
        }

        // To The Recycle Bin, With The Game's Own Backups
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
        // A Sandbox Never Falls Back To The Game's Real Saves
        if (AppInfo.SandboxRoot is { } sandbox)
        {
            return Path.Combine(sandbox, "Saves");
        }

        // LocalLow Sits Beside Local, Windows Names No Folder For It
        var appDataPath = Path.GetDirectoryName(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        );
        return Path.Combine(appDataPath ?? "", "LocalLow", "Endnight", "SonsOfTheForest", "Saves");
    }
}
