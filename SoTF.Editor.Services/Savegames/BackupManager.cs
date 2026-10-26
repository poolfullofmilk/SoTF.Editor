using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Savegames;

public static partial class BackupManager
{
    private const string FolderTimeFormat = "yyyy-MM-dd HH-mm-ss";

    // Kept Beside The Copied Files, Never Restored Into The Savegame
    public const string ChangesFileName = "SoTF.Editor.Changes.json";

    // A Restore's Change List Key, Beside The Page Routes
    public const string RestoreKey = "Backups";

    // The Game Renames Its Name File, So Its Name Counts
    private const string NameFileKey = ".name";
    private const string SaveDataSuffix = "SaveData";

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

    public static void Create(string savegamePath, Dictionary<string, List<Change>>? changes = null)
    {
        var backup = CopyAll(savegamePath);
        if (changes is { Count: > 0 })
        {
            WriteChanges(backup, changes);
        }

        Prune(backup);
    }

    private static void WriteChanges(string backup, Dictionary<string, List<Change>> changes)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(backup, ChangesFileName),
                JsonConvert.SerializeObject(changes, Formatting.Indented, new StringEnumConverter())
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The List Is Extra, Saving Never Fails Over It
            s_logger.Warn(exception, $"Failed To Write Changes Of {backup}");
        }
    }

    public static Dictionary<string, List<Change>> ChangesOf(string backup)
    {
        var path = Path.Combine(backup, ChangesFileName);
        try
        {
            return File.Exists(path)
                ? JsonConvert.DeserializeObject<Dictionary<string, List<Change>>>(
                    File.ReadAllText(path)
                ) ?? []
                : [];
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A Damaged List Shows As Not Recorded
            s_logger.Warn(exception, $"Failed To Read Changes Of {backup}");
            return [];
        }
    }

    public static List<Change> Compare(string savegamePath, string backup)
    {
        return
        [
            .. DifferingFiles(savegamePath, backup)
                .Select(file => new Change(DisplayNameOf(file.Key), file.Kind)),
        ];
    }

    internal static List<(string Key, ChangeKind Kind)> DifferingFiles(
        string savegamePath,
        string backup
    )
    {
        // What Restoring Would Add, Change Or Remove, Entry By Entry
        var now = ContentsOf(savegamePath);
        var then = ContentsOf(backup);
        var differences = new List<(string Key, ChangeKind Kind)>();
        foreach (var key in then.Keys.Union(now.Keys).Order(StringComparer.Ordinal))
        {
            var isNow = now.TryGetValue(key, out var current);
            var isThen = then.TryGetValue(key, out var saved);
            if (isNow && isThen && current == saved)
            {
                continue;
            }

            differences.Add(
                (
                    key,
                    !isNow ? ChangeKind.Added
                    : !isThen ? ChangeKind.Removed
                    : ChangeKind.Changed
                )
            );
        }

        return differences;
    }

    private static Dictionary<string, string> ContentsOf(string folder)
    {
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(folder, file);
            if (name == ChangesFileName)
            {
                continue;
            }

            if (name.EndsWith(NameFileKey, StringComparison.OrdinalIgnoreCase))
            {
                contents[NameFileKey] = name;
            }
            else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(file);
                foreach (var entry in archive.Entries.Where(entry => entry.Name.Length > 0))
                {
                    using var stream = entry.Open();
                    contents[$"{name}/{entry.FullName}"] = Convert.ToHexString(
                        SHA256.HashData(stream)
                    );
                }
            }
            else
            {
                using var stream = File.OpenRead(file);
                contents[name] = Convert.ToHexString(SHA256.HashData(stream));
            }
        }

        return contents;
    }

    internal static string DisplayNameOf(string key)
    {
        if (key == NameFileKey)
        {
            return AppText.Get("SaveName");
        }

        var fileName = Path.GetFileName(key);
        if (
            fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        )
        {
            return AppText.Get("Thumbnail");
        }

        // PlayerInventorySaveData Reads As Player Inventory
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (
            name.Length > SaveDataSuffix.Length
            && name.EndsWith(SaveDataSuffix, StringComparison.Ordinal)
        )
        {
            name = name[..^SaveDataSuffix.Length];
        }

        return WordStart().Replace(name.Replace('_', ' '), " ");
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex WordStart();

    private static string CopyAll(string savegamePath)
    {
        return NewBackup(
            FolderOf(savegamePath),
            target =>
            {
                foreach (
                    var file in Directory.GetFiles(savegamePath, "*", SearchOption.AllDirectories)
                )
                {
                    CopyVerified(
                        file,
                        Path.Combine(target, Path.GetRelativePath(savegamePath, file))
                    );
                }
            }
        );
    }

    public static void CreateForFile(string filePath)
    {
        // Steam Id And File Name, For Files Beside The Savegames
        Prune(
            NewBackup(
                Path.Combine(
                    BackupDirectory,
                    Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "",
                    Path.GetFileNameWithoutExtension(filePath)
                ),
                target => CopyVerified(filePath, Path.Combine(target, Path.GetFileName(filePath)))
            )
        );
    }

    private static string NewBackup(string parent, Action<string> copy)
    {
        var target = NewFolder(parent);
        try
        {
            copy(target);
        }
        catch
        {
            // A Failed Copy Never Shows Up As A Backup
            Directory.Delete(target, true);
            throw;
        }

        s_logger.Info($"Created Backup {target}");
        return target;
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
        // A Missing Or Empty Backup Fails Before Anything Is Touched
        var files = Directory
            .GetFiles(backup, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != ChangesFileName)
            .ToArray();
        if (files.Length == 0)
        {
            throw new IOException($"Found No Files In Backup {backup}");
        }

        // Kept So Restores Can Be Undone, Noting What Came After
        var safetyCopy = CopyAll(savegamePath);
        WriteChanges(
            safetyCopy,
            new()
            {
                [RestoreKey] =
                [
                    new Change(
                        AppText.Format(
                            "RestoredBackup",
                            Formatter.FormatDateTimeWithSeconds(TakenAt(backup))
                        )
                    ),
                ],
            }
        );

        // Files The Backup Lacks Stay In That Safety Copy
        foreach (var current in Directory.GetFiles(savegamePath))
        {
            if (!File.Exists(Path.Combine(backup, Path.GetFileName(current))))
            {
                File.Delete(current);
            }
        }

        foreach (var file in files)
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

    public static int DeleteAllButNewest(string savegamePath)
    {
        // The Newest Stays, So The Last Save Can Be Undone
        var failed = 0;
        foreach (var backup in List(savegamePath).Skip(1))
        {
            try
            {
                Delete(backup.Path);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // One Locked Or Foreign Folder Never Stops The Rest
                s_logger.Warn(exception, $"Failed To Delete Backup {backup.Path}");
                failed++;
            }
        }

        return failed;
    }

    public static bool HasAny(string savegamePath)
    {
        var folder = FolderOf(savegamePath);
        return Directory.Exists(folder) && Directory.EnumerateDirectories(folder).Any();
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
