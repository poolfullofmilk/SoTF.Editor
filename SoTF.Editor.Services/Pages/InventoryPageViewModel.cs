using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Inventory;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class InventoryPageViewModel
{
    private const string BlueprintPagePrefix = "DiscoverablePageUnlocked_";
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    // Blueprints, From Item Plating To Gold Armor Plater
    private static readonly int[] s_blueprintItemIds =
    [
        665,
        666,
        670,
        671,
        672,
        679,
        680,
        681,
        682,
        688,
        701,
        709,
        710,
        711,
        713,
        714,
        726,
    ];

    private readonly List<InventoryItem> _inventory = [];
    private readonly ItemList _itemList;
    private readonly List<InventoryItem> _unassignedItems = [];

    // Each Item's Count At Load, None When Not Held
    private readonly Dictionary<InventoryItem, int?> _loadedCounts = new(
        ReferenceEqualityComparer.Instance
    );

    public InventoryPageViewModel(GameData gameData)
    {
        _itemList = gameData.Items;
        Categories =
        [
            .. _itemList
                .Where(item => item.IsInventoryItem)
                .Select(item => item.Type)
                .Distinct()
                .Order()
                .Select(type => new Category(type)),
        ];
    }

    public List<Category> Categories { get; }

    public IReadOnlyList<InventoryItem> Inventory => _inventory;

    public bool IsHeld(InventoryItem item)
    {
        return _inventory.Contains(item);
    }

    public IEnumerable<InventoryItem> Filter(string? filter, string? type, bool heldOnly)
    {
        // One List, Held Items And Everything That Could Be Added
        var normalizedFilter = TranslationHelper.Normalize(filter ?? "");
        return (heldOnly ? _inventory : _inventory.Concat(_unassignedItems))
            .Where(item => type == null || item.Type == type)
            .Where(item =>
                string.IsNullOrWhiteSpace(normalizedFilter)
                || item.NormalizedName.Contains(
                    normalizedFilter,
                    StringComparison.OrdinalIgnoreCase
                )
                || item.TypeRendered.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase)
                || item.Id.ToString().Contains(normalizedFilter)
            )
            .OrderBy(item => item.Name);
    }

    public bool HasChanges => _loadedCounts.Keys.Any(IsChanged);

    public IEnumerable<string> ChangedNames =>
        _loadedCounts.Keys.Where(IsChanged).Select(item => item.Name);

    public bool IsChanged(InventoryItem item)
    {
        return !_loadedCounts.TryGetValue(item, out var loaded) || loaded != CountOf(item);
    }

    private int? CountOf(InventoryItem item)
    {
        return IsHeld(item) ? item.TotalCount : null;
    }

    public void RemoveItemFromInventory(InventoryItem? inventoryItem)
    {
        if (inventoryItem != null && _inventory.Remove(inventoryItem))
        {
            _unassignedItems.Add(inventoryItem);
        }
    }

    public void AddUnassignedItem(InventoryItem? inventoryItem)
    {
        if (inventoryItem != null && _unassignedItems.Remove(inventoryItem))
        {
            _inventory.Add(inventoryItem);
        }
    }

    public void Load(Savegame? savegame)
    {
        _inventory.Clear();
        _unassignedItems.Clear();
        if (savegame == null)
        {
            return;
        }

        if (
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)
                ?.Parent.ToObject<PlayerInventoryDataModel>()
                ?.Data.PlayerInventory is
            { } playerInventory
        )
        {
            _inventory.AddRange(
                playerInventory.ItemInstanceManagerData.ItemBlocks.Select(
                    itemBlock => new InventoryItem(itemBlock, ItemHolding(itemBlock))
                )
            );

            // Equipped Items Count One Each
            foreach (var itemBlock in playerInventory.EquippedItems ?? [])
            {
                itemBlock.TotalCount = 1;
                _inventory.Add(
                    new InventoryItem(itemBlock, _itemList.GetItem(itemBlock.ItemId), true)
                );
            }
        }

        var heldItemIds = _inventory.Select(item => item.Id).ToHashSet();
        _unassignedItems.AddRange(
            _itemList
                .Where(item =>
                    !heldItemIds.Contains(item.Id)
                    && item is { IsInventoryItem: true, StorageMax.Inventory: > 0 }
                )
                .Select(item => new InventoryItem(ItemBlockModel.Unassigned(item), item))
        );

        _loadedCounts.Clear();
        foreach (var item in _inventory.Concat(_unassignedItems))
        {
            _loadedCounts[item] = CountOf(item);
        }
    }

    private Item? ItemHolding(ItemBlockModel itemBlock)
    {
        // A Count Above Max Raises The Max For This Stack
        var item = _itemList.GetItem(itemBlock.ItemId);
        if (item?.StorageMax is not { } storageMax || storageMax.Inventory >= itemBlock.TotalCount)
        {
            return item;
        }

        s_logger.Info(
            $"Found Item {item.Id} Count {itemBlock.TotalCount} Above Its Max {storageMax.Inventory}"
        );
        var raisedItem = (Item)item.Clone();
        raisedItem.StorageMax = storageMax with { Inventory = itemBlock.TotalCount };
        return raisedItem;
    }

    public bool Update(Savegame savegame)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)
            is not { } saveDataWrapper
        )
        {
            return false;
        }

        var hasChanges =
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)
                is { } playerInventory
            && PlayerInventoryModel.Merge(playerInventory, _inventory);
        if (hasChanges)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerInventory);
        }

        return UpdateBlueprintPages(savegame.SavegameStore) | hasChanges;
    }

    private bool UpdateBlueprintPages(SavegameStore savegameStore)
    {
        if (
            savegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState)?["_entries"]
                is not JArray entries
        )
        {
            return false;
        }

        // Only A Blueprint Added Or Removed Here Moves Its Page
        var heldItemIds = _inventory.Select(item => item.Id).ToHashSet();
        var heldAtLoad = _loadedCounts
            .Where(pair => pair.Value != null)
            .Select(pair => pair.Key.Id)
            .ToHashSet();
        var blueprintItemIdsByPage = s_blueprintItemIds
            .Where(itemId => heldItemIds.Contains(itemId) != heldAtLoad.Contains(itemId))
            .ToDictionary(itemId => BlueprintPagePrefix + itemId);
        var listedItemIds = new HashSet<int>();
        var hasChanges = false;
        foreach (var pageEntry in entries)
        {
            if (
                pageEntry["Name"]?.Value<string>() is not { } name
                || !blueprintItemIdsByPage.TryGetValue(name, out var itemId)
            )
            {
                continue;
            }

            listedItemIds.Add(itemId);
            var isHeld = heldItemIds.Contains(itemId);
            if ((pageEntry["BoolValue"]?.Value<bool>() ?? false) != isHeld)
            {
                pageEntry["BoolValue"] = isHeld;
                hasChanges = true;
            }
        }

        foreach (
            var itemId in blueprintItemIdsByPage.Values.Where(itemId =>
                heldItemIds.Contains(itemId) && !listedItemIds.Contains(itemId)
            )
        )
        {
            entries.Add(
                new JObject { { "Name", BlueprintPagePrefix + itemId }, { "BoolValue", true } }
            );
            hasChanges = true;
        }

        if (hasChanges)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);
        }

        return hasChanges;
    }

    public void SetAllEquippedToMin()
    {
        _inventory.ForEach(inventoryItem => inventoryItem.TotalCount = 1);
    }

    public void SetAllEquippedToMax()
    {
        _inventory.ForEach(inventoryItem => inventoryItem.TotalCount = inventoryItem.Max);
    }

    public void RemoveAllEquipped()
    {
        _unassignedItems.AddRange(_inventory);
        _inventory.Clear();
    }

    public bool HasUnassignedItems()
    {
        return _unassignedItems.Count > 0;
    }

    public void AddAllUnassigned()
    {
        _inventory.AddRange(_unassignedItems);
        _unassignedItems.Clear();
    }

    public bool HasInventoryItems()
    {
        return _inventory.Count > 0;
    }

    public void AddAllFromCategory(Category category)
    {
        _inventory.AddRange(_unassignedItems.Where(item => item.Type == category.Type));
        _unassignedItems.RemoveAll(item => item.Type == category.Type);
    }
}

public class Category(string type)
{
    public string Type { get; } = type;

    public string TypeRendered => AppText.Get(TextKeys.ItemType(Type));
}
