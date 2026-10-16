using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class StoragePageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<int, AdvancedStorageDefinition> _advancedStorageDefinitions;
    private readonly Dictionary<int, StorageDefinition> _storageDefinitions;
    private readonly StorageFactory _storageFactory;

    public StoragePageViewModel(StorageFactory storageFactory, GameData gameData)
    {
        _storageFactory = storageFactory;
        _storageDefinitions = gameData.StorageDefinitions.ToDictionary(definition => definition.Id);
        _advancedStorageDefinitions = gameData.AdvancedStorageDefinitions.ToDictionary(definition =>
            definition.Id
        );
    }

    public List<StorageCollection> StorageCollections { get; } = [];

    public IStorage? SelectedStorage { get; set; }

    public void ApplyToAllOfSameType(IStorage source)
    {
        var collection = StorageCollections.FirstOrDefault(collection =>
            collection.StorageTypeId == source.GetStorageTypeId()
        );
        if (collection == null)
        {
            return;
        }

        foreach (var storage in collection.Storages.Where(storage => storage != source))
        {
            storage.ApplyFrom(source);
        }
    }

    public bool HasStorages()
    {
        return StorageCollections.Any(storageCollection => storageCollection.Storages.Count > 0);
    }

    public void FillAllStorages()
    {
        foreach (var storageCollection in StorageCollections)
        foreach (var storage in storageCollection.Storages)
        {
            storage.SetAllToMax();
        }
    }

    public static IEnumerable<StoredItem> StoredItemsOf(IStorage storage)
    {
        var slots = storage switch
        {
            BaseStorage baseStorage => baseStorage.Slots,
            AdvancedItemsStorage advancedItemsStorage => advancedItemsStorage.Slots,
            _ => [],
        };

        return slots.SelectMany(slot => slot.StoredItems);
    }

    public static void ClearAll(IStorage storage)
    {
        foreach (var storedItem in StoredItemsOf(storage))
        {
            storedItem.Clear();
        }
    }

    public static void FillAll(IStorage storage, ItemWrapper selectedItem, int count)
    {
        foreach (var storedItem in StoredItemsOf(storage))
        {
            // Advanced Storages Only Take Items Their Slot Supports
            var candidate =
                storage is AdvancedItemsStorage
                    ? storedItem.SupportedItems.FirstOrDefault(item =>
                        item.Item.Id == selectedItem.Item.Id
                    )
                    : selectedItem;
            if (candidate == null)
            {
                continue;
            }

            storedItem.SelectedItem = candidate;
            storedItem.Count = count;
        }
    }

    public static void SetAllToMax(IStorage storage)
    {
        foreach (var storedItem in StoredItemsOf(storage))
        {
            if (storedItem.SelectedItem != null)
            {
                storedItem.Count = storedItem.Max;
            }
        }
    }

    public void Load(Savegame? savegame)
    {
        SelectedStorage = null;
        StorageCollections.Clear();

        if (savegame is not { } selectedSavegame)
        {
            return;
        }

        if (ReadStorageCollections(selectedSavegame) is not { } storageCollectionsById)
        {
            return;
        }

        foreach (
            var storageCollection in storageCollectionsById.Values.OrderBy(collection =>
                collection.Name
            )
        )
        {
            StorageCollections.Add(storageCollection);
        }
    }

    private List<StorageSaveData>? LoadStorageSaveData(Savegame selectedSavegame)
    {
        if (
            selectedSavegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.ScrewStructureInstancesSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"]
            is not JArray structures
        )
        {
            return null;
        }

        var result = new List<StorageSaveData>();

        foreach (var structure in structures)
        {
            if (
                structure.ToObject<StorageSaveData>() is not { } storageSaveData
                || !StorageIdKnown(storageSaveData.Id)
            )
            {
                continue;
            }

            result.Add(storageSaveData);
        }

        return result;
    }

    private bool StorageIdKnown(int id)
    {
        return _storageDefinitions.ContainsKey(id) || _advancedStorageDefinitions.ContainsKey(id);
    }

    private Dictionary<int, StorageCollection>? ReadStorageCollections(Savegame selectedSavegame)
    {
        if (LoadStorageSaveData(selectedSavegame) is not { } storageSaveData)
        {
            return null;
        }

        var storageCollectionsById = new Dictionary<int, StorageCollection>();

        foreach (var saveData in storageSaveData)
        {
            var storageDefinition =
                _storageDefinitions.GetValueOrDefault(saveData.Id)
                ?? (IStorageDefinition?)_advancedStorageDefinitions.GetValueOrDefault(saveData.Id);
            if (storageDefinition == null)
            {
                continue;
            }

            StorageCollection storageCollection;

            if (!storageCollectionsById.TryGetValue(saveData.Id, out var value))
            {
                storageCollection = new StorageCollection(saveData.Id, storageDefinition.Name);
                storageCollectionsById.Add(saveData.Id, storageCollection);
            }
            else
            {
                storageCollection = value;
            }

            var storage = _storageFactory.Build(
                storageDefinition,
                storageCollection.Storages.Count + 1
            );
            storage.SetSaveData(saveData);

            storageCollection.Storages.Add(storage);
        }

        return storageCollectionsById;
    }

    public bool Update(Savegame savegame)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(
                SavegameStore.FileType.ScrewStructureInstancesSaveData
            )
                is not { } saveDataWrapper
            || saveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"]
                is not JArray structures
        )
        {
            return false;
        }

        var newStorageSaveDataById = StorageCollections
            .SelectMany(collection => collection.Storages)
            .Select(storage => storage.ToStorageSaveData())
            .GroupBy(saveData => saveData.Id)
            .ToDictionary(group => group.Key, group => group.ToList());

        var numProcessedById = new Dictionary<int, int>();

        var hasChanges = false;

        foreach (var structureToken in structures)
        {
            if (
                structureToken["Id"]?.Value<int>() is not { } structureId
                || !StorageIdKnown(structureId)
            )
            {
                continue;
            }

            if (newStorageSaveDataById.GetValueOrDefault(structureId) is not { } newStorageSaveData)
            {
                s_logger.Warn($"Skipped Saving Storages, Found No New Data For {structureId}");
                return false;
            }

            var numProcessed = numProcessedById.GetValueOrDefault(structureId, 0);
            if (
                newStorageSaveData.Count <= numProcessed
                || newStorageSaveData[numProcessed] is not { } newSaveData
            )
            {
                s_logger.Warn(
                    $"Skipped Saving Storages, Found No New Data For {structureId} At {numProcessed}"
                );
                return false;
            }

            if (structureToken.ToObject<StorageSaveData>() is not { } oldStorageSaveData)
            {
                s_logger.Warn(
                    $"Skipped Saving Storages, Failed To Read {structureId} At {numProcessed}"
                );
                return false;
            }

            if (oldStorageSaveData.Equals(newSaveData))
            {
                s_logger.Info($"Kept Unchanged Storage {structureId} At {numProcessed}");
                numProcessedById[structureId] = numProcessed + 1;
                continue;
            }

            hasChanges = MergeIntoStorageBlock(structureToken, newSaveData) || hasChanges;
            numProcessedById[structureId] = numProcessed + 1;
        }

        if (hasChanges)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return hasChanges;
    }

    private static bool MergeIntoStorageBlock(JToken target, StorageSaveData newData)
    {
        if (target["Storages"] is not { } storagesToken)
        {
            return false;
        }

        storagesToken.Replace(JToken.FromObject(newData.Storages));
        return true;
    }

    public class StorageCollection(int storageTypeId, string name)
    {
        public int StorageTypeId { get; } = storageTypeId;
        public string Name { get; } = name;

        public List<IStorage> Storages { get; } = [];
    }
}
