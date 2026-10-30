using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Storage;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class StoragePageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<int, AdvancedStorageDefinition> _advancedStorageDefinitions;
    private readonly Dictionary<int, StorageDefinition> _storageDefinitions;
    private readonly ItemList _items;

    public StoragePageViewModel(GameData gameData)
    {
        _items = gameData.Items;
        _storageDefinitions = gameData.StorageDefinitions.ToDictionary(definition => definition.Id);
        _advancedStorageDefinitions = gameData.AdvancedStorageDefinitions.ToDictionary(definition =>
            definition.Id
        );
    }

    public List<StorageCollection> StorageCollections { get; } = [];

    // Every Slot As Loaded, Per Storage In Slot Order
    private readonly Dictionary<IStorage, List<string>> _loaded = new(
        ReferenceEqualityComparer.Instance
    );

    public bool HasChanges => _loaded.Keys.Any(IsChanged);

    public IEnumerable<string> ChangedNames =>
        _loaded.Keys.Where(IsChanged).Select(storage => storage.Description);

    public bool IsChanged(IStorage storage)
    {
        return !_loaded.TryGetValue(storage, out var loaded)
            || !loaded.SequenceEqual(StoredItemsOf(storage).Select(ValueOf));
    }

    public bool IsChanged(IStorage storage, StoredItem storedItem)
    {
        var index = StoredItemsOf(storage).ToList().IndexOf(storedItem);
        return !_loaded.TryGetValue(storage, out var loaded)
            || index < 0
            || index >= loaded.Count
            || loaded[index] != ValueOf(storedItem);
    }

    private static List<string> ValuesOf(IStorage storage)
    {
        return [.. StoredItemsOf(storage).Select(ValueOf)];
    }

    private static string ValueOf(StoredItem storedItem)
    {
        // Every Food State Shares An Id, So The State Counts
        var item = storedItem.SelectedItem;
        var state = item?.FoodSpoilStorageModuleWrapper?.FoodSpoilStorageModule.CurrentState;
        return LoadedValues.Format($"{item?.Item.Id} {state} {storedItem.Count}");
    }

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

    public bool HasSameTypeStorages(IStorage source)
    {
        return StorageCollections.Any(collection =>
            collection.StorageTypeId == source.GetStorageTypeId() && collection.Storages.Count > 1
        );
    }

    public static bool CanSetAllToMax(IStorage storage)
    {
        return StoredItemsOf(storage)
            .Any(storedItem => storedItem.HasItem() && storedItem.Count < storedItem.Max);
    }

    public static bool HasStoredItems(IStorage storage)
    {
        return StoredItemsOf(storage).Any(storedItem => storedItem.HasItem());
    }

    // Some Slot Still Empty Or Below Its Max
    public bool CanFillAllStorages()
    {
        return StorageCollections
            .SelectMany(collection => collection.Storages)
            .SelectMany(StoredItemsOf)
            .Any(storedItem => storedItem.CanFill());
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

    private bool IsStorage(int id)
    {
        return _storageDefinitions.ContainsKey(id) || _advancedStorageDefinitions.ContainsKey(id);
    }

    private IStorage NewStorage(int id, int index)
    {
        if (!_storageDefinitions.TryGetValue(id, out var definition))
        {
            return new AdvancedItemsStorage(_advancedStorageDefinitions[id], _items, index);
        }

        return definition.Type == StorageType.Food
            ? new FoodStorage(definition, _items, index)
            : new BaseStorage(definition, _items, index);
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
        LoadStorages(savegame);
        _loaded.Clear();
        foreach (var storage in StorageCollections.SelectMany(collection => collection.Storages))
        {
            _loaded[storage] = ValuesOf(storage);
        }
    }

    public void Revert(Savegame savegame)
    {
        // The Same Storage Stays Picked, Found By Its Place
        var index = AllStorages().ToList().IndexOf(SelectedStorage!);
        Load(savegame);
        SelectedStorage = AllStorages().ElementAtOrDefault(index);
    }

    private IEnumerable<IStorage> AllStorages()
    {
        return StorageCollections.SelectMany(collection => collection.Storages);
    }

    private void LoadStorages(Savegame? savegame)
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
            if (saveData == null || !IsStorage(saveData.Id))
            {
                continue;
            }

            if (!storageCollectionsById.TryGetValue(saveData.Id, out var storageCollection))
            {
                storageCollection = new StorageCollection(
                    saveData.Id,
                    GameNames.Get(TextKeys.Structure(saveData.Id))
                );
                storageCollectionsById.Add(saveData.Id, storageCollection);
            }

            var storage = NewStorage(saveData.Id, storageCollection.Storages.Count + 1);
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
                || !IsStorage(structureId)
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
