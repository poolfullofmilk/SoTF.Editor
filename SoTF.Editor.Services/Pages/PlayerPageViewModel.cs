using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class PlayerPageViewModel
{
    private const string PlayerAreaMaskEntry = "player.areaMask";

    private readonly List<Item> _availableClothes = [];

    // Worn Pieces That Are No Outfit, Like The Rebreather
    private readonly List<int> _otherWornItemIds = [];

    private readonly GameData _gameData;

    public PlayerPageViewModel(GameData gameData)
    {
        _gameData = gameData;
    }

    public PlayerState PlayerState { get; } = new();

    public IEnumerable<Item> AvailableClothes => _availableClothes.OrderBy(item => item.Name);

    public IEnumerable<int> WornItemIds =>
        PlayerState.SelectedCloth is { } cloth
            ? _otherWornItemIds.Append(cloth.Id)
            : _otherWornItemIds;

    public void Load(Savegame? savegame)
    {
        if (savegame is { } selectedSavegame)
        {
            LoadPlayerData(selectedSavegame);
            LoadClothes(selectedSavegame);
        }
        else
        {
            PlayerState.Reset();
            _otherWornItemIds.Clear();
        }
    }

    private void LoadClothes(Savegame savegame)
    {
        _availableClothes.Clear();
        _availableClothes.AddRange(
            _gameData
                .Items.Select(pair => pair.Value)
                .Where(item => item.Type == "clothes" || item.IsWearableCloth)
        );
        _otherWornItemIds.Clear();
        PlayerState.SelectedCloth = null;

        var clothing =
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerClothingSystemSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.PlayerClothingSystem)
                ?["Clothing"] as JArray;
        foreach (var itemId in clothing?.Select(token => token.Value<int>()) ?? [])
        {
            // The First Outfit Is Worn, The Rest Stays As Is
            if (PlayerState.SelectedCloth == null && OutfitOf(itemId) is { } outfit)
            {
                PlayerState.SelectedCloth = outfit;
            }
            else
            {
                _otherWornItemIds.Add(itemId);
            }
        }

        // No Outfit Listed Means The Starting Jacket
        PlayerState.SelectedCloth ??= OutfitOf(Constants.Items.DefaultPlayerClothItemId);
    }

    private Item? OutfitOf(int itemId)
    {
        return _availableClothes.FirstOrDefault(cloth => cloth.Id == itemId);
    }

    public void MoveToFollower(FollowerState followerState)
    {
        var followerPos = followerState.Pos;
        var playerPos = PlayerState.Pos;

        Teleporter.MovePlayerToPos(ref playerPos, ref followerPos);

        followerState.Pos = followerPos;
        PlayerState.Pos = playerPos;
    }

    public void FillAllBars()
    {
        PlayerState.CurrentHealth = PlayerState.MaxHealth;
        PlayerState.Fullness = 100;
        PlayerState.Hydration = 100;
        PlayerState.Rest = 100;
        PlayerState.Stamina = 100;
        PlayerState.HydrationBuff = 65535;
        PlayerState.RestBuff = 1;
        PlayerState.FullnessBuff = 65535;
        PlayerState.Sickness = 0;
    }

    private void LoadPlayerData(Savegame savegame)
    {
        // A Missing Field Must Not Keep The Last Save's Value
        PlayerState.Reset();

        if (
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.PlayerState)
                is not JObject playerState
            || playerState["_entries"] is not JArray entries
        )
        {
            return;
        }

        Position? playerPos = null;
        var areaMask = 0;

        foreach (var entry in entries)
        {
            var name = entry["Name"]?.ToString();

            switch (name)
            {
                case "player.position":
                    var playerPosFloatArray = entry["FloatArrayValue"]?.ToObject<float[]>();

                    if (playerPosFloatArray is { Length: 3 })
                    {
                        playerPos = new Position(
                            playerPosFloatArray[0],
                            playerPosFloatArray[1],
                            playerPosFloatArray[2]
                        );
                    }

                    break;
                case PlayerAreaMaskEntry:
                    areaMask = SettingReader.ReadInt(entry) ?? 0;
                    break;
                case "StrengthLevel":
                    PlayerState.StrengthLevel = SettingReader.ReadInt(entry) ?? 0;
                    break;
                case "MaxHealth":
                    PlayerState.MaxHealth = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "CurrentHealth":
                    PlayerState.CurrentHealth = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "Hydration":
                    PlayerState.Hydration = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "HydrationBuff":
                    PlayerState.HydrationBuff = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "Fullness":
                    PlayerState.Fullness = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "FullnessBuff":
                    PlayerState.FullnessBuff = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "Rest":
                    PlayerState.Rest = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "RestBuff":
                    PlayerState.RestBuff = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "Stamina":
                    PlayerState.Stamina = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
                case "Sickness":
                    PlayerState.Sickness = SettingReader.ReadFloat(entry) ?? 0f;
                    break;
            }
        }

        if (playerPos is { } pos)
        {
            PlayerState.Pos = pos.WithoutOffset(_gameData.AreaManager.GetAreaForAreaMask(areaMask));
        }
    }

    public bool Update(Savegame? savegame)
    {
        var hasChanges = UpdatePlayerState(savegame);
        hasChanges = UpdateClothes(savegame) || hasChanges;
        return hasChanges;
    }

    private bool UpdateClothes(Savegame? savegame)
    {
        if (
            savegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerClothingSystemSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper
                .GetJsonBasedToken(Constants.JsonKeys.PlayerClothingSystem)
                ?["Clothing"]
                is not JArray clothing
        )
        {
            return false;
        }

        // Only The Outfit Changes, Other Worn Pieces Keep Their Place
        var outfitToken = clothing.FirstOrDefault(token => OutfitOf(token.Value<int>()) != null);
        var wornId = outfitToken?.Value<int>();
        var selectedId = PlayerState.SelectedCloth?.Id;
        var selectedIsDefault = selectedId is null or Constants.Items.DefaultPlayerClothItemId;
        if (wornId == selectedId || (wornId == null && selectedIsDefault))
        {
            return false;
        }

        if (selectedIsDefault)
        {
            outfitToken?.Remove();
        }
        else if (outfitToken == null)
        {
            clothing.Insert(0, selectedId!.Value);
        }
        else
        {
            outfitToken.Replace(selectedId!.Value);
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerClothingSystem);

        return true;
    }

    private bool UpdatePlayerState(Savegame? savegame)
    {
        if (
            savegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
                is not { } saveDataWrapper
            || saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState)
                is not JObject playerState
            || playerState["_entries"] is not JArray entries
        )
        {
            return false;
        }

        var hasChanges = false;

        foreach (var entry in entries)
        {
            var name = entry["Name"]?.ToString();

            switch (name)
            {
                case "player.position":
                    if (
                        entry["FloatArrayValue"] is { } floatArrayToken
                        && floatArrayToken.ToObject<float[]>() is { Length: 3 } playerPosFloatArray
                    )
                    {
                        var oldPlayerPos = new Position(
                            playerPosFloatArray[0],
                            playerPosFloatArray[1],
                            playerPosFloatArray[2]
                        );
                        // The Area Lives In Its Own Entry Below
                        if (!oldPlayerPos.HasSameCoordinates(PlayerState.Pos))
                        {
                            floatArrayToken.Replace(
                                JToken.FromObject(
                                    new[]
                                    {
                                        PlayerState.Pos.X,
                                        PlayerState.Pos.Y,
                                        PlayerState.Pos.Z,
                                    }
                                )
                            );
                            hasChanges = true;
                        }
                    }

                    break;
                case PlayerAreaMaskEntry:
                    hasChanges =
                        SettingWriter.WriteInt(entry, PlayerState.Pos.Area.AreaMask) || hasChanges;
                    break;
                case "StrengthLevel":
                    hasChanges =
                        SettingWriter.WriteInt(entry, PlayerState.StrengthLevel) || hasChanges;
                    break;
                case "MaxHealth":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.MaxHealth) || hasChanges;
                    break;
                case "CurrentHealth":
                case "TargetHealth":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.CurrentHealth) || hasChanges;
                    break;
                case "Hydration":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.Hydration) || hasChanges;
                    break;
                case "HydrationBuff":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.HydrationBuff) || hasChanges;
                    break;
                case "Fullness":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.Fullness) || hasChanges;
                    break;
                case "FullnessBuff":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.FullnessBuff) || hasChanges;
                    break;
                case "Rest":
                    hasChanges = SettingWriter.WriteFloat(entry, PlayerState.Rest) || hasChanges;
                    break;
                case "RestBuff":
                    hasChanges =
                        SettingWriter.WriteFloat(entry, PlayerState.RestBuff) || hasChanges;
                    break;
                case "Stamina":
                    hasChanges = SettingWriter.WriteFloat(entry, PlayerState.Stamina) || hasChanges;
                    break;
                case "Sickness":
                    if (PlayerState.Sickness == 0)
                    {
                        hasChanges = SettingWriter.RemoveFloat(entry) || hasChanges;
                    }
                    else
                    {
                        hasChanges =
                            SettingWriter.WriteFloat(entry, PlayerState.Sickness) || hasChanges;
                    }

                    break;
            }
        }

        if (!hasChanges)
        {
            return false;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);

        return true;
    }
}
