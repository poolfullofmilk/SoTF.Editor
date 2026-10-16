using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData.Armour;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class ArmorPageViewModel
{
    private readonly List<ArmourData> _armour = [];
    private readonly ItemList _itemList;

    public ArmorPageViewModel(GameData gameData)
    {
        NewArmour = new NewArmourPiece(_armour);
        _itemList = gameData.Items;
        foreach (
            var armorItem in _itemList
                .Where(item => item.Value.IsEquippableArmor)
                .OrderBy(item => item.Value.Name)
        )
        {
            ArmourTypes.Add(armorItem.Value);
        }
    }

    public IEnumerable<ArmourData> Armour => _armour.OrderBy(armourData => armourData.Slot);
    public List<Item> ArmourTypes { get; } = [];

    public NewArmourPiece NewArmour { get; }

    public void RemoveArmour(ArmourData armourData)
    {
        _armour.Remove(armourData);
    }

    public void Load(Savegame? savegame)
    {
        _armour.Clear();
        var armour = savegame
            ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerArmourSystemSaveData)
            ?.Parent.ToObject<PlayerArmourDataModel>();

        if (armour != null)
        {
            foreach (var armourPiece in armour.Data.PlayerArmourSystem.ArmourPieces)
            {
                _armour.Add(new ArmourData(armourPiece, _itemList.GetItem(armourPiece.ItemId)));
            }
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

        var selectedArmorPieces = _armour.Select(armourData => armourData.ArmourPiece).ToList();
        var hasChanges = PlayerArmourSystemModel.Merge(
            playerArmourData.Data.PlayerArmourSystem,
            selectedArmorPieces
        );

        if (!hasChanges)
        {
            return hasChanges;
        }

        saveDataWrapper
            .GetJsonBasedToken(Constants.JsonKeys.PlayerArmourSystem)
            ?["ArmourPieces"]?.Replace(
                JToken.FromObject(playerArmourData.Data.PlayerArmourSystem.ArmourPieces)
            );
        saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerArmourSystem);

        return hasChanges;
    }

    public bool HasArmorPieces()
    {
        return _armour.Count > 0;
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

    public class NewArmourPiece
    {
        private readonly List<ArmourData> _sink;

        internal NewArmourPiece(List<ArmourData> sink)
        {
            _sink = sink;
        }

        public int RemainingArmourpoints { get; set; }

        public Item? SelectedItem
        {
            get;
            set
            {
                field = value;
                RemainingArmourpoints = value?.Durability?.Default ?? RemainingArmourpoints;
            }
        }

        public void AddArmor()
        {
            if (_sink.Count == 10)
            {
                return;
            }

            var nextSlot = PlayerArmourSystemModel.ArmorSlots.First(slot =>
                _sink.All(piece => piece.Slot != slot)
            );

            var armourPiece = new ArmourPieceModel
            {
                ItemId = SelectedItem!.Id,
                RemainingArmourpoints = RemainingArmourpoints,
                Slot = nextSlot,
            };
            _sink.Add(new ArmourData(armourPiece, SelectedItem));
        }

        public bool CanAddArmor()
        {
            return HasItemSelected() && RemainingArmourpoints > 0 && _sink.Count < 10;
        }

        public bool HasItemSelected()
        {
            return SelectedItem != null;
        }
    }
}
