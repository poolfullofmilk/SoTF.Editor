using System.IO.Compression;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;

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
    private readonly ReaderWriterLockSlim _readerWriterLockSlim = new();

    public SavegameStore(string path)
    {
        _path = path;
        LastWriteTime = ReadLastWriteTime();
    }

    public DateTime LastWriteTime { get; private set; }

    private DateTime ReadLastWriteTime()
    {
        return Directory.GetLastWriteTime(_path);
    }

    public SaveDataWrapper? LoadJsonRaw(FileType fileType)
    {
        _readerWriterLockSlim.EnterUpgradeableReadLock();
        try
        {
            if (_rawData.TryGetValue(fileType, out var saveDataWrapper))
            {
                return saveDataWrapper;
            }

            _readerWriterLockSlim.EnterWriteLock();
            try
            {
                if (_rawData.TryGetValue(fileType, out var wrapper))
                {
                    return wrapper;
                }

                var data = LoadJson<JToken>(fileType);
                _rawData[fileType] = data == null ? null : new SaveDataWrapper(data);
                return _rawData[fileType];
            }
            finally
            {
                _readerWriterLockSlim.ExitWriteLock();
            }
        }
        finally
        {
            _readerWriterLockSlim.ExitUpgradeableReadLock();
        }
    }

    private T? LoadJson<T>(FileType fileType)
    {
        var fileName = fileType.GetFilename();
        var path = ResolvePath(fileName);
        return File.Exists(path)
            ? SaveJson.DeserializeFromFile<T>(path)
            : LoadJsonFromArchiveIfExists<T>(fileName);
    }

    private T? LoadJsonFromArchiveIfExists<T>(string fileName)
    {
        var archivePath = ResolvePath(FileType.SaveDataArchive.GetFilename());
        if (!File.Exists(archivePath))
        {
            return default;
        }

        using var archive = ZipFile.OpenRead(archivePath);
        var fileEntry = archive.GetEntry(fileName);
        if (fileEntry == null)
        {
            return default;
        }

        using var stream = fileEntry.Open();
        using var streamReader = new StreamReader(stream);
        var json = streamReader.ReadToEnd();
        return SaveJson.DeserializeFromString<T>(json);
    }

    private string ResolvePath(string fileName)
    {
        return Path.Combine(_path, fileName);
    }

    public bool HasChanged()
    {
        return Directory.GetLastWriteTime(_path) != LastWriteTime;
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

    public DateTime? RestoreBackup(bool newest)
    {
        return BackupManager.Restore(_path, newest);
    }

    public bool HasModified()
    {
        return _rawData.Values.Any(saveDataWrapper => saveDataWrapper?.HasModified() ?? false);
    }

    public bool SaveAllModified()
    {
        var changedWrappers = _rawData
            .Where(kvp => kvp.Value != null && kvp.Value.HasModified())
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

        if (changedWrappers.Count == 0)
        {
            s_logger.Info("Found No Changes");
            return false;
        }

        SaveWrappers(changedWrappers);

        return true;
    }

    public void SaveWrappers(Dictionary<FileType, SaveDataWrapper> changedWrappers)
    {
        // Nothing Is Written Until A Verified Copy Exists
        BackupManager.Create(_path);

        var archivePath = ResolvePath(FileType.SaveDataArchive.GetFilename());
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

        if (archiveEntries.Count == 0)
        {
            return;
        }

        WriteReplacing(
            archivePath,
            temporary =>
            {
                File.Copy(archivePath, temporary, true);
                using var archive = ZipFile.Open(temporary, ZipArchiveMode.Update);
                foreach (var (fileName, json) in archiveEntries)
                {
                    archive.GetEntry(fileName)?.Delete();
                    using var streamWriter = new StreamWriter(archive.CreateEntry(fileName).Open());
                    streamWriter.Write(json);
                }
            }
        );
    }

    private static void WriteReplacing(string path, Action<string> write)
    {
        // A Crash Mid Write Leaves The Old File Whole
        var temporary = path + ".tmp";
        write(temporary);
        File.Move(temporary, path, true);
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
