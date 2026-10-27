using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Armour;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class ArmorPageViewModel
{
    private readonly List<ArmourData> _armour = [];
    private readonly Dictionary<ArmourData, (int ItemId, float Durability, int Slot)> _loaded = new(
        ReferenceEqualityComparer.Instance
    );
    private readonly ItemList _itemList;
    private readonly InventoryPageViewModel _inventoryPage;

    public ArmorPageViewModel(GameData gameData, InventoryPageViewModel inventoryPage)
    {
        _inventoryPage = inventoryPage;
        _itemList = gameData.Items;
        ArmourTypes =
        [
            .. _itemList
                .Where(item => item.IsEquippableArmor)
                .OrderBy(item => item.Durability?.Default ?? 0)
                .ThenBy(item => item.Name),
        ];
    }

    // Weakest To Strongest, Like The Pickers
    public IEnumerable<ArmourData> Armour =>
        _armour
            .OrderBy(armourData => armourData.DefaultDurability)
            .ThenBy(armourData => armourData.Name)
            .ThenBy(armourData => armourData.Slot);

    // A Move Through The Backpack Ties This To The Inventory
    public bool MovedThroughInventory { get; private set; }
    public List<Item> ArmourTypes { get; }

    // New Piece, A Picked Type Starts At Its Default
    public Item? NewType
    {
        get;
        set
        {
            field = value;
            NewDurability = value?.Durability?.Default ?? NewDurability;
        }
    }
    public int NewDurability { get; set; }

    public bool HasChanges => _loaded.Count != _armour.Count || _armour.Any(IsChanged);

    // Removed, Added And Changed Pieces Alike
    public IEnumerable<Change> Changes =>
        _loaded
            .Keys.Where(armourData => !_armour.Contains(armourData))
            .Select(armourData => new Change(armourData.Name, ChangeKind.Removed))
            .Concat(
                _armour
                    .Where(IsChanged)
                    .Select(armourData => new Change(
                        armourData.Name,
                        _loaded.ContainsKey(armourData) ? ChangeKind.Changed : ChangeKind.Added
                    ))
            );

    public bool IsChanged(ArmourData armourData)
    {
        // A Piece Added Since The Load Was Never Remembered
        return !_loaded.TryGetValue(armourData, out var loaded) || loaded != ValueOf(armourData);
    }

    public bool IsTypeChanged(ArmourData armourData)
    {
        return !_loaded.TryGetValue(armourData, out var loaded)
            || loaded.ItemId != armourData.Id
            || loaded.Slot != armourData.Slot;
    }

    public bool IsDurabilityChanged(ArmourData armourData)
    {
        return _loaded.TryGetValue(armourData, out var loaded)
            && loaded.Durability != armourData.ArmourPiece.RemainingArmourpoints;
    }

    private static (int ItemId, float Durability, int Slot) ValueOf(ArmourData armourData)
    {
        return (armourData.Id, armourData.ArmourPiece.RemainingArmourpoints, armourData.Slot);
    }

    // Empty Slots, In The Order The Game Fills Them
    public IEnumerable<int> FreeSlots =>
        PlayerArmourSystemModel.ArmorSlots.Where(slot =>
            _armour.TrueForAll(armourData => armourData.Slot != slot)
        );

    // Armour In The Backpack, And Armour Worn From It Since The Load
    public IEnumerable<InventoryItem> InventoryArmour =>
        _inventoryPage
            .Filter(null, null, false)
            .Where(item =>
                item.Item?.IsEquippableArmor == true
                && (_inventoryPage.IsHeld(item) || _inventoryPage.IsChanged(item))
            )
            .OrderBy(item => item.Item?.Durability?.Default ?? 0);

    // Types The Backpack Does Not Hold Yet, Its Own Included
    public IEnumerable<Item> TypesFor(InventoryItem item)
    {
        return ArmourTypes.Where(type =>
            type.Id == item.Id
            || _inventoryPage.ItemOf(type.Id) is { } other && !_inventoryPage.IsHeld(other)
        );
    }

    public void ChangeType(InventoryItem item, Item? type)
    {
        // The Whole Stack Turns Into The Other Type
        if (
            type == null
            || type.Id == item.Id
            || !_inventoryPage.IsHeld(item)
            || _inventoryPage.ItemOf(type.Id) is not { } target
            || _inventoryPage.IsHeld(target)
        )
        {
            return;
        }

        _inventoryPage.RemoveItemFromInventory(item);
        _inventoryPage.AddUnassignedItem(target);
        target.TotalCount = Math.Min(item.TotalCount, target.Max);
    }

    public bool CanWear(InventoryItem item)
    {
        return item.Item != null && FreeSlots.Any() && _inventoryPage.IsHeld(item);
    }

    public void Wear(InventoryItem item)
    {
        if (!CanWear(item) || item.Item is not { } armour)
        {
            return;
        }

        // A Piece From The Backpack Starts At Default Durability
        Add(armour, armour.Durability?.Default ?? 1);
        TakeOneFrom(item);
        MovedThroughInventory = true;
    }

    public bool CanAddNew()
    {
        return NewType != null && NewDurability > 0 && FreeSlots.Any();
    }

    public void AddNew()
    {
        if (CanAddNew() && NewType is { } type)
        {
            Add(type, NewDurability);
        }
    }

    private void Add(Item type, float durability)
    {
        _armour.Add(
            new ArmourData(
                new ArmourPieceModel
                {
                    ItemId = type.Id,
                    RemainingArmourpoints = durability,
                    Slot = FreeSlots.First(),
                },
                type
            )
        );
    }

    public void DeleteFromInventory(InventoryItem item)
    {
        // Gone For Good, Only The Inventory Changes
        if (_inventoryPage.IsHeld(item))
        {
            TakeOneFrom(item);
        }
    }

    private void TakeOneFrom(InventoryItem item)
    {
        if (item.TotalCount > 1)
        {
            item.TotalCount--;
        }
        else
        {
            _inventoryPage.RemoveItemFromInventory(item);
        }
    }

    public bool CanMoveToInventory(ArmourData armourData)
    {
        // The Backpack Needs Room For One More
        return _inventoryPage.ItemOf(armourData.Id) is { } item
            && (!_inventoryPage.IsHeld(item) || item.TotalCount < item.Max);
    }

    public void MoveToInventory(ArmourData armourData)
    {
        if (!CanMoveToInventory(armourData) || _inventoryPage.ItemOf(armourData.Id) is not { } item)
        {
            return;
        }

        if (_inventoryPage.IsHeld(item))
        {
            item.TotalCount++;
        }
        else
        {
            _inventoryPage.AddUnassignedItem(item);
            item.TotalCount = 1;
        }

        _armour.Remove(armourData);
        MovedThroughInventory = true;
    }

    public void RemoveArmour(ArmourData armourData)
    {
        _armour.Remove(armourData);
    }

    public void Load(Savegame? savegame)
    {
        MovedThroughInventory = false;
        _armour.Clear();
        var armourPieces =
            savegame
                ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerArmourSystemSaveData)
                ?.Parent.ToObject<PlayerArmourDataModel>()
                ?.Data.PlayerArmourSystem.ArmourPieces
            ?? [];
        _armour.AddRange(
            armourPieces.Select(armourPiece => new ArmourData(
                armourPiece,
                _itemList.GetItem(armourPiece.ItemId)
            ))
        );

        _loaded.Clear();
        foreach (var armourData in _armour)
        {
            _loaded[armourData] = ValueOf(armourData);
        }
    }

    public bool Update(Savegame savegame)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerArmourSystemSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.Parent.ToObject<PlayerArmourDataModel>() is not { } playerArmourData
        )
        {
            return false;
        }

        var armourSystem = playerArmourData.Data.PlayerArmourSystem;
        if (
            !PlayerArmourSystemModel.Merge(
                armourSystem,
                [.. _armour.Select(armourData => armourData.ArmourPiece)]
            )
        )
        {
            return false;
        }

        saveDataWrapper
            .GetJsonBasedToken(Constants.JsonKeys.PlayerArmourSystem)
            ?["ArmourPieces"]?.Replace(JToken.FromObject(armourSystem.ArmourPieces));
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerArmourSystem);
        return true;
    }

    public bool HasArmorPieces()
    {
        return _armour.Count > 0;
    }

    public bool CanSetAllToDefault()
    {
        return _armour.Any(armourData =>
            armourData.ArmourPiece.RemainingArmourpoints != armourData.DefaultDurability
        );
    }

    public bool CanSetAllToMax()
    {
        return _armour.Any(armourData =>
            armourData.ArmourPiece.RemainingArmourpoints < armourData.MaxDurability
        );
    }

    public void SetAllToDefault()
    {
        foreach (var armourData in _armour)
        {
            armourData.ArmourPiece.RemainingArmourpoints = armourData.DefaultDurability;
        }
    }

    public void SetAllToMax()
    {
        foreach (var armourData in _armour)
        {
            armourData.ArmourPiece.RemainingArmourpoints = armourData.MaxDurability;
        }
    }
}
