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
        ArmourTypes =
        [
            .. _itemList.Where(item => item.IsEquippableArmor).OrderBy(item => item.Name),
        ];
    }

    public IEnumerable<ArmourData> Armour => _armour.OrderBy(armourData => armourData.Slot);
    public List<Item> ArmourTypes { get; }

    public NewArmourPiece NewArmour { get; }

    public void RemoveArmour(ArmourData armourData)
    {
        _armour.Remove(armourData);
    }

    public void Load(Savegame? savegame)
    {
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
            if (!CanAddArmor())
            {
                return;
            }

            var armourPiece = new ArmourPieceModel
            {
                ItemId = SelectedItem!.Id,
                RemainingArmourpoints = RemainingArmourpoints,
                Slot = PlayerArmourSystemModel.ArmorSlots.First(slot =>
                    _sink.All(piece => piece.Slot != slot)
                ),
            };
            _sink.Add(new ArmourData(armourPiece, SelectedItem));
        }

        public bool CanAddArmor()
        {
            return SelectedItem != null
                && RemainingArmourpoints > 0
                && _sink.Count < PlayerArmourSystemModel.ArmorSlots.Count;
        }
    }
}
