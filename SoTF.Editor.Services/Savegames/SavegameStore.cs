using System.IO.Compression;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Savegames;

public sealed class SavegameStore
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
        GardenPlotManagerSaveData,
        Resin3dPrinterBunkerASaveData,
        Resin3dPrinterBunkerBSaveData,
        Resin3dPrinterEntertainmentSaveData,
        Resin3dPrinterResidentialSaveData,
        Resin3dPrinterResistanceSaveData,
        MultiplayerPlayerRolesSaveData,
    }

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly string _path;

    private readonly Dictionary<FileType, SaveDataWrapper?> _rawData = [];

    private readonly string _loadedFiles;

    public SavegameStore(string path)
    {
        _path = path;
        LastWriteTime = Directory.GetLastWriteTime(_path);
        _loadedFiles = Stamps();
    }

    private SavegameStore(SavegameStore loaded)
    {
        _path = loaded._path;
        LastWriteTime = loaded.LastWriteTime;
        _loadedFiles = loaded._loadedFiles;
        foreach (var (fileType, saveDataWrapper) in loaded._rawData)
        {
            _rawData[fileType] = saveDataWrapper?.Copy();
        }
    }

    public SavegameStore Draft()
    {
        // Pages Merge Into A Copy, A Failed Save Changes Nothing
        return new SavegameStore(this);
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
        // A Folder Gone Since The Load Changed Too
        return !Directory.Exists(_path) || HasChanged(Stamps());
    }

    public bool HasChanged(string stamps)
    {
        return stamps != _loadedFiles;
    }

    public string Stamps()
    {
        // Each File Sorted, Rewrites Keep The Folder Time
        return string.Join(
            "|",
            new DirectoryInfo(_path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
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

    public void BackUpLoaded(Dictionary<string, List<Change>>? changes)
    {
        // Pages Merge After This, So A Failure Merges Nothing
        CheckFilesExist(_rawData.Where(pair => pair.Value != null).Select(pair => pair.Key));
        BackupManager.Create(_path, changes);
    }

    public bool SaveAllModified()
    {
        return Write(
            _rawData
                .Where(pair => pair.Value?.HasModified() == true)
                .ToDictionary(pair => pair.Key, pair => pair.Value!)
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
        if (changedWrappers.Count > 0)
        {
            CheckFilesExist(changedWrappers.Keys);

            // Nothing Is Written Until A Verified Copy Exists
            BackupManager.Create(_path, changes);
        }

        return Write(changedWrappers);
    }

    private void CheckFilesExist(IEnumerable<FileType> fileTypes)
    {
        // A File Gone Since Loading Fails Before Any Write
        var archivePath = ResolvePath(FileType.SaveDataArchive.GetFilename());
        foreach (var fileName in fileTypes.Select(fileType => fileType.GetFilename()))
        {
            if (!File.Exists(ResolvePath(fileName)) && !File.Exists(archivePath))
            {
                throw new IOException($"Found No {fileName} To Write");
            }
        }
    }

    private bool Write(Dictionary<FileType, SaveDataWrapper> changedWrappers)
    {
        if (changedWrappers.Count == 0)
        {
            s_logger.Info("Found No Changes");
            return false;
        }

        var archivePath = ResolvePath(FileType.SaveDataArchive.GetFilename());
        var writes = new List<(string Target, Action<string> Write)>();
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
                writes.Add(
                    (fullPath, temporary => File.WriteAllText(temporary, json, SaveJson.Encoding))
                );
            }
            else if (File.Exists(archivePath))
            {
                archiveEntries.Add((fileName, json));
            }
            else
            {
                // A File Gone Since Loading Is Never Skipped Silently
                throw new IOException($"Found No {fileName} To Write");
            }
        }

        if (archiveEntries.Count > 0)
        {
            writes.Add(
                (archivePath, temporary => WriteArchive(archivePath, temporary, archiveEntries))
            );
        }

        // Every File Or None, A Failure Keeps Edits Marked
        FileTransaction.Commit(writes, []);
        foreach (var saveDataWrapper in changedWrappers.Values)
        {
            saveDataWrapper.ClearModified();
        }

        return true;
    }

    private static void WriteArchive(
        string archivePath,
        string temporary,
        List<(string FileName, string Json)> entries
    )
    {
        File.Copy(archivePath, temporary);
        using var archive = ZipFile.Open(temporary, ZipArchiveMode.Update);
        foreach (var (fileName, json) in entries)
        {
            // An Entry Gone Since Loading Fails, Never Added Anew
            var entry =
                archive.GetEntry(fileName)
                ?? throw new IOException($"Found No {fileName} In The Archive");
            entry.Delete();
            using var streamWriter = new StreamWriter(archive.CreateEntry(fileName).Open());
            streamWriter.Write(json);
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
            // The Game Names Each Printer With An Underscore
            _ when fileType.ToString().StartsWith("Resin3dPrinter", StringComparison.Ordinal) =>
                fileType.ToString().Insert("Resin3dPrinter".Length, "_") + ".json",
            _ => fileType + ".json",
        };
    }
}
