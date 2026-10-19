using System.Globalization;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Savegames;

public static class BackupManager
{
    private const string FolderTimeFormat = "yyyy-MM-dd HH-mm-ss";

    // All Backups Together, Past It The Oldest Go
    public const long SizeLimit = 500L * 1024 * 1024;

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    // Outside The Game Folder, Steam Cloud Never Touches It
    public static readonly string BackupDirectory = Path.Combine(
        AppInfo.DocumentsDirectory,
        "Backups"
    );

    // Raised With The Number Of Old Backups Removed
    public static event Action<int>? Pruned;

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
        Prune(CopyAll(savegamePath));
    }

    private static string CopyAll(string savegamePath)
    {
        var target = NewFolder(FolderOf(savegamePath));
        foreach (var file in Directory.GetFiles(savegamePath, "*", SearchOption.AllDirectories))
        {
            CopyVerified(file, Path.Combine(target, Path.GetRelativePath(savegamePath, file)));
        }

        s_logger.Info($"Created Backup {target}");
        return target;
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
        Prune(target);
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
        var safetyCopy = CopyAll(savegamePath);

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

        // Pruning Waits, Or It Could Remove The Chosen Backup
        Prune(safetyCopy);
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
                    .Select(path => new SavegameBackup(path, TakenAt(path), SizeOf(path))),
            ]
            : [];
    }

    public static long TotalSize()
    {
        return Directory.Exists(BackupDirectory) ? SizeOf(BackupDirectory) : 0;
    }

    public static void Delete(string backup)
    {
        // Only A Dated Folder Inside The Backups Ever Goes
        var fullPath = Path.GetFullPath(backup);
        if (
            !fullPath.StartsWith(
                Path.GetFullPath(BackupDirectory) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            ) || !IsBackupFolder(fullPath)
        )
        {
            throw new ArgumentException($"Not A Backup {backup}", nameof(backup));
        }

        Directory.Delete(fullPath, true);
        s_logger.Info($"Deleted Backup {fullPath}");
    }

    private static void Prune(string newest)
    {
        var total = TotalSize();
        if (total <= SizeLimit)
        {
            return;
        }

        // The Oldest Go First, Each Savegame Keeps Its First Backup
        var candidates = Directory
            .EnumerateDirectories(BackupDirectory, "*", SearchOption.AllDirectories)
            .Where(IsBackupFolder)
            .GroupBy(Path.GetDirectoryName)
            .SelectMany(group => group.OrderBy(TakenAt).Skip(1))
            .Where(folder => folder != newest)
            .OrderBy(TakenAt)
            .ToList();
        var removed = 0;
        foreach (var folder in candidates.TakeWhile(_ => total > SizeLimit))
        {
            try
            {
                var size = SizeOf(folder);
                Directory.Delete(folder, true);
                total -= size;
                removed++;
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // A Locked Backup Stays, Saving Never Fails Over It
                s_logger.Warn(exception, $"Failed To Remove Old Backup {folder}");
            }
        }

        if (removed > 0)
        {
            s_logger.Warn(
                $"Removed {removed} Old Backups, Backups Passed {SizeLimit / 1048576} MB"
            );
            Pruned?.Invoke(removed);
        }
    }

    private static long SizeOf(string folder)
    {
        return new DirectoryInfo(folder)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(file => file.Length);
    }

    private static bool IsBackupFolder(string folder)
    {
        return TryParseTakenAt(Path.GetFileName(folder), out _);
    }

    private static DateTime TakenAt(string backup)
    {
        return TryParseTakenAt(Path.GetFileName(backup), out var takenAt)
            ? takenAt
            : Directory.GetCreationTime(backup);
    }

    private static bool TryParseTakenAt(string name, out DateTime takenAt)
    {
        takenAt = default;
        return name.Length >= FolderTimeFormat.Length
            && DateTime.TryParseExact(
                name[..FolderTimeFormat.Length],
                FolderTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out takenAt
            );
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
