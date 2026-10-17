using System.Globalization;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Savegames;

public static class BackupManager
{
    private const string FolderTimeFormat = "yyyy-MM-dd HH-mm-ss";
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    // Outside The Game Folder, Steam Cloud Never Touches It
    public static readonly string BackupDirectory = Path.Combine(
        AppInfo.DocumentsDirectory,
        "Backups"
    );

    public static string FolderOf(string savegamePath)
    {
        // Steam Id, Mode And Save Id Keep Every Savegame Apart
        var save = new DirectoryInfo(savegamePath);
        var mode = save.Parent;
        var steamId = mode?.Parent;
        return Path.Combine(BackupDirectory, steamId?.Name ?? "", mode?.Name ?? "", save.Name);
    }

    public static void Create(string savegamePath)
    {
        var target = NewFolder(FolderOf(savegamePath));
        foreach (var file in Directory.GetFiles(savegamePath, "*", SearchOption.AllDirectories))
        {
            CopyVerified(file, Path.Combine(target, Path.GetRelativePath(savegamePath, file)));
        }

        s_logger.Info($"Created Backup {target}");
    }

    public static void CreateForFile(string filePath)
    {
        // Steam Id And File Name, For Files Beside The Savegames
        var target = NewFolder(
            Path.Combine(
                BackupDirectory,
                Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "",
                Path.GetFileNameWithoutExtension(filePath)
            )
        );
        CopyVerified(filePath, Path.Combine(target, Path.GetFileName(filePath)));
        s_logger.Info($"Created Backup {target}");
    }

    private static string NewFolder(string parent)
    {
        var target = Path.Combine(
            parent,
            DateTime.Now.ToString(FolderTimeFormat, CultureInfo.InvariantCulture)
        );

        // A Backup Never Overwrites Another
        var candidate = target;
        for (var attempt = 2; Directory.Exists(candidate); attempt++)
        {
            candidate = $"{target} ({attempt})";
        }

        Directory.CreateDirectory(candidate);
        return candidate;
    }

    public static void Restore(string savegamePath, string backup)
    {
        // The Current State Is Kept, So Restores Can Be Undone
        Create(savegamePath);

        // Files The Backup Lacks Stay In That Safety Copy
        foreach (var current in Directory.GetFiles(savegamePath))
        {
            if (!File.Exists(Path.Combine(backup, Path.GetFileName(current))))
            {
                File.Delete(current);
            }
        }

        foreach (var file in Directory.GetFiles(backup, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(savegamePath, Path.GetRelativePath(backup, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temporary = target + ".tmp";
            File.Copy(file, temporary, true);
            File.Move(temporary, target, true);
        }

        s_logger.Info($"Restored Backup {backup}");
    }

    public static List<SavegameBackup> List(string savegamePath)
    {
        // Newest First, A Suffixed Name Sorts After Its Twin
        var folder = FolderOf(savegamePath);
        return Directory.Exists(folder)
            ?
            [
                .. Directory
                    .GetDirectories(folder)
                    .OrderDescending(StringComparer.Ordinal)
                    .Select(path => new SavegameBackup(path, TakenAt(path))),
            ]
            : [];
    }

    private static DateTime TakenAt(string backup)
    {
        var name = Path.GetFileName(backup);
        return
            name.Length >= FolderTimeFormat.Length
            && DateTime.TryParseExact(
                name[..FolderTimeFormat.Length],
                FolderTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var takenAt
            )
            ? takenAt
            : Directory.GetCreationTime(backup);
    }

    private static void CopyVerified(string file, string copy)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(file, copy);

        // Saving Waits Until The Copy Reads Back Identical
        if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(copy)))
        {
            throw new IOException($"Backup Copy Differs From {file}");
        }
    }
}
