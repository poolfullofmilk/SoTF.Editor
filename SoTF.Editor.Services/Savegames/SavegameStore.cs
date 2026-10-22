using System.IO.Compression;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Savegames;

public class SavegameStore
{
    public enum FileType
    {
        SaveDataThumbnail,
        GameSetupSaveData,
        PlayerInventorySaveData,
        PlayerArmourSystemSaveData,
        SaveData,
        SaveDataArchive,
        GameStateSaveData,
        WorldObjectLocatorManagerSaveData,
        WeatherSystemSaveData,
        PlayerStateSaveData,
        ScrewStructureInstancesSaveData,
        PlayerClothingSystemSaveData,
        FiresSaveData,
        ScrewStructureNodeInstancesSaveData,
        StructureDestructionSaveData,
        WorldItemManagerSaveData,
        ZipLineManagerSaveData,
        ScrewTrapsSaveData,
        ConstructionsSaveData,
    }

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly string _path;

    private readonly Dictionary<FileType, SaveDataWrapper?> _rawData = [];

    private readonly string _loadedFiles;

    public SavegameStore(string path)
    {
        _path = path;
        LastWriteTime = Directory.GetLastWriteTime(_path);
        _loadedFiles = FileStamps();
    }

    public DateTime LastWriteTime { get; }

    public SaveDataWrapper? LoadJsonRaw(FileType fileType)
    {
        if (!_rawData.TryGetValue(fileType, out var saveDataWrapper))
        {
            saveDataWrapper = LoadJson(fileType) is { } data ? new SaveDataWrapper(data) : null;
            _rawData[fileType] = saveDataWrapper;
        }

        return saveDataWrapper;
    }

    private JToken? LoadJson(FileType fileType)
    {
        // A Loose File Wins Over The Copy In The Archive
        var fileName = fileType.GetFilename();
        var path = ResolvePath(fileName);
        if (File.Exists(path))
        {
            return SaveJson.DeserializeFromFile<JToken>(path);
        }

        var archivePath = ResolvePath(FileType.SaveDataArchive.GetFilename());
        if (!File.Exists(archivePath))
        {
            return null;
        }

        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.GetEntry(fileName) is not { } fileEntry)
        {
            return null;
        }

        using var streamReader = new StreamReader(fileEntry.Open());
        return SaveJson.DeserializeFromString<JToken>(streamReader.ReadToEnd());
    }

    private string ResolvePath(string fileName)
    {
        return Path.Combine(_path, fileName);
    }

    public bool HasChanged()
    {
        return FileStamps() != _loadedFiles;
    }

    private string FileStamps()
    {
        // A File Rewritten In Place Leaves The Folder Time Alone
        return string.Join(
            "|",
            new DirectoryInfo(_path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Select(file => $"{file.FullName} {file.Length} {file.LastWriteTimeUtc.Ticks}")
        );
    }

    public string? GetThumbPath()
    {
        var thumbPath = ResolvePath(FileType.SaveDataThumbnail.GetFilename());
        return File.Exists(thumbPath) ? thumbPath : null;
    }

    public DirectoryInfo? GetParentDirectory()
    {
        return new DirectoryInfo(_path).Parent;
    }

    public bool HasModified(FileType fileType)
    {
        return _rawData.GetValueOrDefault(fileType)?.HasModified() ?? false;
    }

    public bool HasModified()
    {
        return _rawData.Values.Any(saveDataWrapper => saveDataWrapper?.HasModified() ?? false);
    }

    public string ChangedState()
    {
        return string.Concat(
            _rawData.Values.Select(saveDataWrapper => saveDataWrapper?.ChangedState())
        );
    }

    public bool SaveAllModified(Dictionary<string, List<Change>>? changes = null)
    {
        return SaveWrappers(
            _rawData
                .Where(pair => pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value!),
            changes
        );
    }

    public bool SaveWrappers(
        Dictionary<FileType, SaveDataWrapper> wrappers,
        Dictionary<string, List<Change>>? changes = null
    )
    {
        var changedWrappers = wrappers
            .Where(pair => pair.Value.HasModified())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (changedWrappers.Count == 0)
        {
            s_logger.Info("Found No Changes");
            return false;
        }

        // A File Gone Since Loading Fails Before Any Write
        var archivePath = ResolvePath(FileType.SaveDataArchive.GetFilename());
        foreach (var fileName in changedWrappers.Keys.Select(fileType => fileType.GetFilename()))
        {
            if (!File.Exists(ResolvePath(fileName)) && !File.Exists(archivePath))
            {
                throw new IOException($"Found No {fileName} To Write");
            }
        }

        // Nothing Is Written Until A Verified Copy Exists
        BackupManager.Create(_path, changes);

        var archiveEntries = new List<(string FileName, string Json)>();
        foreach (var (fileType, saveDataWrapper) in changedWrappers)
        {
            s_logger.Info($"Marked {fileType} As Modified");
            saveDataWrapper.SerializeAllModified();
            var fileName = fileType.GetFilename();
            var json = SaveJson.Serialize(saveDataWrapper.Parent);
            var fullPath = ResolvePath(fileName);
            if (File.Exists(fullPath))
            {
                WriteReplacing(
                    fullPath,
                    temporary => File.WriteAllText(temporary, json, SaveJson.Encoding)
                );
            }
            else if (File.Exists(archivePath))
            {
                archiveEntries.Add((fileName, json));
            }
        }

        if (archiveEntries.Count > 0)
        {
            WriteReplacing(
                archivePath,
                temporary =>
                {
                    File.Copy(archivePath, temporary, true);
                    using var archive = ZipFile.Open(temporary, ZipArchiveMode.Update);
                    foreach (var (fileName, json) in archiveEntries)
                    {
                        archive.GetEntry(fileName)?.Delete();
                        using var streamWriter = new StreamWriter(
                            archive.CreateEntry(fileName).Open()
                        );
                        streamWriter.Write(json);
                    }
                }
            );
        }

        // A Failed Write Keeps Edits Marked, Saving Again Retries
        foreach (var saveDataWrapper in changedWrappers.Values)
        {
            saveDataWrapper.ClearModified();
        }

        return true;
    }

    private static void WriteReplacing(string path, Action<string> write)
    {
        // A Crash Mid Write Leaves The Old File Whole
        var temporary = path + ".tmp";
        try
        {
            write(temporary);
            File.Move(temporary, path, true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}

public static class FileTypeExtensions
{
    public static string GetFilename(this SavegameStore.FileType fileType)
    {
        return fileType switch
        {
            SavegameStore.FileType.SaveDataThumbnail => fileType + ".png",
            SavegameStore.FileType.SaveDataArchive => "SaveData.zip",
            _ => fileType + ".json",
        };
    }
}
