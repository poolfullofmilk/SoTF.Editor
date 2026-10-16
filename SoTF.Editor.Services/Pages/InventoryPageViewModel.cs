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
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly HashSet<int> _blueprintItemIds =
    [
        665, //Item Plating
        666, //Powered Cross
        670, //Gore Chair
        671, //Gore Couch
        672, //Uber Trap
        679, //Grind Trap
        680, //Spotlight
        681, //Clock
        682, //Spin Trap
        688, //Spear Thrower Trap
        701, //Leg Lamp
        709, //Teleporter
        710, //Repel Shrine
        711, //Attract Shrine
        713, //Plater Counter
        714, //Gold Armor Plater
        726,
    ];

    private readonly List<InventoryItem> _inventory = [];

    private readonly ItemList _itemList;
    private readonly List<InventoryItem> _unassignedItems = [];

    public InventoryPageViewModel(GameData gameData)
    {
        _itemList = gameData.Items;

        var categories = gameData
            .Items.Where(item => item.Value.IsInventoryItem)
            .Select(item => item.Value.Type)
            .Distinct()
            .OrderBy(type => type)
            .Select(type => new Category(type));

        Categories.AddRange(categories);
    }

    public List<Category> Categories { get; } = [];

    public IReadOnlyList<InventoryItem> Inventory => _inventory;

    public IEnumerable<InventoryItem> FilterInventory(string? filter)
    {
        return Filter(_inventory, filter);
    }

    public IEnumerable<InventoryItem> FilterUnassignedItems(string? filter)
    {
        return Filter(_unassignedItems, filter);
    }

    private static IEnumerable<InventoryItem> Filter(
        IEnumerable<InventoryItem> items,
        string? filter
    )
    {
        var normalizedFilter = TranslationHelper.Normalize(filter ?? "").ToLower();
        return items
            .Where(item =>
                string.IsNullOrWhiteSpace(normalizedFilter) || FilterItem(item, normalizedFilter)
            )
            .OrderBy(item => item.Name);
    }

    public void RemoveItemFromInventory(InventoryItem? inventoryItem)
    {
        if (inventoryItem == null)
        {
            return;
        }

        _unassignedItems.Add(inventoryItem);
        _inventory.Remove(inventoryItem);
    }

    public void AddUnassignedItem(InventoryItem? inventoryItem)
    {
        if (inventoryItem == null)
        {
            return;
        }

        _inventory.Add(inventoryItem);
        _unassignedItems.Remove(inventoryItem);
    }

    private static bool FilterItem(InventoryItem item, string filter)
    {
        return item.NormalizedName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || item.TypeRendered.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || item.Id.ToString().Contains(filter);
    }

    public void Load(Savegame? savegame)
    {
        if (savegame == null)
        {
            _inventory.Clear();
            _unassignedItems.Clear();
            return;
        }

        HashSet<int> assignedItems = [];

        var saveData = savegame
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerInventorySaveData)
            ?.Parent.ToObject<PlayerInventoryDataModel>();
        if (saveData != null)
        {
            var playerInventoryModel = saveData.Data.PlayerInventory;
            var inventoryItems = playerInventoryModel
                .ItemInstanceManagerData.ItemBlocks.Select(itemBlock =>
                {
                    var item = _itemList.GetItem(itemBlock.ItemId);
                    if (
                        item?.StorageMax?.Inventory is { } maxInInventory
                        && maxInInventory < itemBlock.TotalCount
                    )
                    {
                        item = (Item)item.Clone();
                        item.StorageMax = new StorageMax(
                            itemBlock.TotalCount,
                            item.StorageMax?.Shelf ?? 0,
                            item.StorageMax?.Holder
                        );
                        s_logger.Info(
                            $"Found Item {item.Id} Count {itemBlock.TotalCount} Above Its Max {maxInInventory}"
                        );
                    }

                    return new InventoryItem(itemBlock, item);
                })
                .ToList();

            var equippedItems = playerInventoryModel
                .EquippedItems?.Select(itemBlock =>
                {
                    var item = _itemList.GetItem(itemBlock.ItemId);
                    if (
                        item?.StorageMax?.Inventory is { } maxInInventory
                        && maxInInventory < itemBlock.TotalCount
                    )
                    {
                        s_logger.Info(
                            $"Found Item {item.Id} Count {itemBlock.TotalCount} Above Its Max {maxInInventory}"
                        );
                    }

                    itemBlock.TotalCount = 1;

                    return new InventoryItem(itemBlock, item, true);
                })
                .ToList();

            if (equippedItems != null)
            {
                inventoryItems.AddRange(equippedItems);
            }

            _inventory.Clear();
            _inventory.AddRange(inventoryItems);

            foreach (var inventoryItem in inventoryItems)
            {
                assignedItems.Add(inventoryItem.Id);
            }
        }
        else
        {
            _inventory.Clear();
        }

        var unassignedItems = _itemList
            .Where(item => !assignedItems.Contains(item.Value.Id))
            .Where(item => item.Value is { IsInventoryItem: true, StorageMax.Inventory: > 0 })
            .Select(item => BuildUnassignedItem(item.Value));

        _unassignedItems.Clear();
        _unassignedItems.AddRange(unassignedItems);
    }

    private static InventoryItem BuildUnassignedItem(Item item)
    {
        var itemBlock = ItemBlockModel.Unassigned(item);
        return new InventoryItem(itemBlock, item);
    }

    public bool Update(Savegame savegame)
    {
        var saveDataWrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.PlayerInventorySaveData
        );

        if (saveDataWrapper == null)
        {
            return false;
        }

        var selectedItems = new List<InventoryItem>(_inventory);

        var hasChanged = MergeInventory(saveDataWrapper, selectedItems);
        return EnsureBlueprintStatusUpdated(savegame.SavegameStore, selectedItems) || hasChanged;
    }

    private static bool MergeInventory(
        SaveDataWrapper saveDataWrapper,
        List<InventoryItem> selectedItems
    )
    {
        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerInventory)
            is not { } playerInventory
        )
        {
            return false;
        }

        if (!PlayerInventoryModel.Merge(playerInventory, selectedItems))
        {
            return false;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerInventory);
        return true;
    }

    private bool EnsureBlueprintStatusUpdated(
        SavegameStore savegameStore,
        List<InventoryItem> inventoryItems
    )
    {
        if (
            savegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
            is not { } saveDataWrapper
        )
        {
            return false;
        }

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState) is not { } playerState
            || playerState["_entries"] is not JArray entries
        )
        {
            return false;
        }

        var addedBlueprintItems = new HashSet<int>();

        var hasChanges = false;

        foreach (
            var inventoryItem in inventoryItems.Where(inventoryItem =>
                _blueprintItemIds.Contains(inventoryItem.Id)
            )
        )
        {
            addedBlueprintItems.Add(inventoryItem.Id);
        }

        var blueprintTokensExisting = new HashSet<int>();

        foreach (var pageEntry in entries)
        {
            var name = pageEntry["Name"]?.Value<string>();

            foreach (
                var itemId in _blueprintItemIds.Where(itemId =>
                    name == $"DiscoverablePageUnlocked_{itemId}"
                )
            )
            {
                blueprintTokensExisting.Add(itemId);

                var isEnabled = pageEntry["BoolValue"]?.Value<bool>() ?? false;
                var isAdded = addedBlueprintItems.Contains(itemId);
                if (isEnabled != isAdded)
                {
                    pageEntry["BoolValue"] = isAdded;
                    hasChanges = true;
                }

                break;
            }
        }

        foreach (var blueprintItemId in _blueprintItemIds)
        {
            if (
                blueprintTokensExisting.Contains(blueprintItemId)
                || !addedBlueprintItems.Contains(blueprintItemId)
            )
            {
                continue;
            }

            entries.Add(
                new JObject
                {
                    { "Name", $"DiscoverablePageUnlocked_{blueprintItemId}" },
                    { "BoolValue", true },
                }
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
        foreach (var inventoryItem in _inventory)
        {
            inventoryItem.TotalCount = 1;
        }
    }

    public void SetAllEquippedToMax()
    {
        foreach (var inventoryItem in _inventory)
        {
            inventoryItem.TotalCount = inventoryItem.Max;
        }
    }

    public void RemoveAllEquipped()
    {
        foreach (var inventoryItem in _inventory.ToList())
        {
            RemoveItemFromInventory(inventoryItem);
        }
    }

    public bool HasInventoryItems()
    {
        return _inventory.Count > 0;
    }

    public void AddAllFromCategory(Category category)
    {
        var items = _unassignedItems.Where(item => item.Type == category.Type).ToList();
        foreach (var inventoryItem in items)
        {
            _unassignedItems.Remove(inventoryItem);
        }

        _inventory.AddRange(items);
    }
}

public class Category(string type)
{
    public string Type { get; } = type;

    public string TypeRendered => AppText.Get(TextKeys.ItemType(Type));
}
