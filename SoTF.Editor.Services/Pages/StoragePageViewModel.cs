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
        return storage.Slots.SelectMany(slot => slot.StoredItems);
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
            storedItem.SetToMax();
        }
    }

    private IStorageDefinition? DefinitionOf(int id)
    {
        return _storageDefinitions.GetValueOrDefault(id)
            ?? (IStorageDefinition?)_advancedStorageDefinitions.GetValueOrDefault(id);
    }

    private static JArray? StructuresOf(Savegame savegame)
    {
        return savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.ScrewStructureInstancesSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.ScrewStructureInstances)
                ?["_structures"] as JArray;
    }

    public void Load(Savegame? savegame)
    {
        SelectedStorage = null;
        StorageCollections.Clear();
        if (savegame == null || StructuresOf(savegame) is not { } structures)
        {
            return;
        }

        // One Collection Per Storage Type, Numbered In Save Order
        var storageCollectionsById = new Dictionary<int, StorageCollection>();
        foreach (
            var saveData in structures.Select(structure => structure.ToObject<StorageSaveData>())
        )
        {
            if (saveData == null || DefinitionOf(saveData.Id) is not { } storageDefinition)
            {
                continue;
            }

            if (!storageCollectionsById.TryGetValue(saveData.Id, out var storageCollection))
            {
                storageCollection = new StorageCollection(saveData.Id, storageDefinition.Name);
                storageCollectionsById.Add(saveData.Id, storageCollection);
            }

            var storage = _storageFactory.Build(
                storageDefinition,
                storageCollection.Storages.Count + 1
            );
            storage.SetSaveData(saveData);
            storageCollection.Storages.Add(storage);
        }

        StorageCollections.AddRange(
            storageCollectionsById.Values.OrderBy(collection => collection.Name)
        );
    }

    public bool Update(Savegame savegame)
    {
        if (StructuresOf(savegame) is not { } structures)
        {
            return false;
        }

        var newStorageSaveDataById = StorageCollections
            .SelectMany(collection => collection.Storages)
            .Select(storage => storage.ToStorageSaveData())
            .GroupBy(saveData => saveData.Id)
            .ToDictionary(group => group.Key, group => group.ToList());

        // Storages Of One Type Match Their Rows In Save Order
        var processedCountById = new Dictionary<int, int>();
        var hasChanges = false;
        foreach (var structureToken in structures)
        {
            if (
                structureToken["Id"]?.Value<int>() is not { } structureId
                || DefinitionOf(structureId) == null
            )
            {
                continue;
            }

            var processedCount = processedCountById.GetValueOrDefault(structureId, 0);
            if (
                newStorageSaveDataById.GetValueOrDefault(structureId) is not { } newStorageSaveData
                || newStorageSaveData.Count <= processedCount
            )
            {
                s_logger.Warn(
                    $"Skipped Saving Storages, Found No New Data For {structureId} At {processedCount}"
                );
                return false;
            }

            if (structureToken.ToObject<StorageSaveData>() is not { } oldStorageSaveData)
            {
                s_logger.Warn(
                    $"Skipped Saving Storages, Failed To Read {structureId} At {processedCount}"
                );
                return false;
            }

            processedCountById[structureId] = processedCount + 1;
            var newSaveData = newStorageSaveData[processedCount];
            if (oldStorageSaveData.Equals(newSaveData))
            {
                s_logger.Info($"Kept Unchanged Storage {structureId} At {processedCount}");
                continue;
            }

            if (structureToken["Storages"] is { } storagesToken)
            {
                storagesToken.Replace(JToken.FromObject(newSaveData.Storages));
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.ScrewStructureInstancesSaveData)!
                .MarkAsModified(Constants.JsonKeys.ScrewStructureInstances);
        }

        return hasChanges;
    }

    public class StorageCollection(int storageTypeId, string name)
    {
        public int StorageTypeId { get; } = storageTypeId;
        public string Name { get; } = name;

        public List<IStorage> Storages { get; } = [];
    }
}
