using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class PlayerPageViewModel
{
    private const string PlayerPositionEntry = "player.position";
    private const string PlayerAreaMaskEntry = "player.areaMask";
    private const string StrengthLevelEntry = "StrengthLevel";
    private const string SicknessEntry = "Sickness";

    // Float Entries The Page Edits, Read And Written Alike
    private static readonly Dictionary<
        string,
        (Func<PlayerState, float> Get, Action<PlayerState, float> Set)
    > s_floatEntries = new()
    {
        ["MaxHealth"] = (state => state.MaxHealth, (state, value) => state.MaxHealth = value),
        ["CurrentHealth"] = (
            state => state.CurrentHealth,
            (state, value) => state.CurrentHealth = value
        ),
        ["Hydration"] = (state => state.Hydration, (state, value) => state.Hydration = value),
        ["HydrationBuff"] = (
            state => state.HydrationBuff,
            (state, value) => state.HydrationBuff = value
        ),
        ["Fullness"] = (state => state.Fullness, (state, value) => state.Fullness = value),
        ["FullnessBuff"] = (
            state => state.FullnessBuff,
            (state, value) => state.FullnessBuff = value
        ),
        ["Rest"] = (state => state.Rest, (state, value) => state.Rest = value),
        ["RestBuff"] = (state => state.RestBuff, (state, value) => state.RestBuff = value),
        ["Stamina"] = (state => state.Stamina, (state, value) => state.Stamina = value),
        [SicknessEntry] = (state => state.Sickness, (state, value) => state.Sickness = value),
    };

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
            _gameData.Items.Where(item => item.Type == "clothes" || item.IsWearableCloth)
        );
        _otherWornItemIds.Clear();
        PlayerState.SelectedCloth = null;

        foreach (var itemId in ClothingOf(savegame)?.Select(token => token.Value<int>()) ?? [])
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

    private static JArray? ClothingOf(Savegame? savegame)
    {
        return savegame
                ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerClothingSystemSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.PlayerClothingSystem)
                ?["Clothing"] as JArray;
    }

    private static JArray? EntriesOf(Savegame? savegame)
    {
        return (
                savegame
                    ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
                    ?.GetJsonBasedToken(Constants.JsonKeys.PlayerState) as JObject
            )?["_entries"] as JArray;
    }

    private Item? OutfitOf(int itemId)
    {
        return _availableClothes.FirstOrDefault(cloth => cloth.Id == itemId);
    }

    public void MoveToFollower(FollowerState followerState)
    {
        // The Player Takes The Spot, The Follower Steps Up Beside
        PlayerState.Pos = followerState.Pos.WithoutOffset();
        followerState.Pos = Teleporter.MoveToPos(followerState.Pos);
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

    private static Position? PositionOf(JToken entry)
    {
        return entry["FloatArrayValue"]?.ToObject<float[]>() is { Length: 3 } values
            ? new Position(values[0], values[1], values[2])
            : null;
    }

    private void LoadPlayerData(Savegame savegame)
    {
        // A Missing Field Must Not Keep The Last Save's Value
        PlayerState.Reset();

        if (EntriesOf(savegame) is not { } entries)
        {
            return;
        }

        Position? playerPos = null;
        var areaMask = 0;

        foreach (var entry in entries)
        {
            switch (entry["Name"]?.ToString())
            {
                case PlayerPositionEntry:
                    playerPos = PositionOf(entry) ?? playerPos;
                    break;
                case PlayerAreaMaskEntry:
                    areaMask = SettingReader.ReadInt(entry) ?? 0;
                    break;
                case StrengthLevelEntry:
                    PlayerState.StrengthLevel = SettingReader.ReadInt(entry) ?? 0;
                    break;
                case { } name when s_floatEntries.TryGetValue(name, out var accessor):
                    accessor.Set(PlayerState, SettingReader.ReadFloat(entry) ?? 0f);
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
        return UpdatePlayerState(savegame) | UpdateClothes(savegame);
    }

    private bool UpdateClothes(Savegame? savegame)
    {
        if (ClothingOf(savegame) is not { } clothing)
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

        savegame!
            .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerClothingSystemSaveData)!
            .MarkAsModified(Constants.JsonKeys.PlayerClothingSystem);
        return true;
    }

    private bool UpdatePlayerState(Savegame? savegame)
    {
        if (EntriesOf(savegame) is not { } entries)
        {
            return false;
        }

        var hasChanges = false;
        foreach (var entry in entries)
        {
            switch (entry["Name"]?.ToString())
            {
                // The Area Lives In Its Own Entry Below
                case PlayerPositionEntry
                    when PositionOf(entry) is { } oldPlayerPos
                        && !oldPlayerPos.HasSameCoordinates(PlayerState.Pos):
                    entry["FloatArrayValue"]!.Replace(
                        JToken.FromObject(
                            new[] { PlayerState.Pos.X, PlayerState.Pos.Y, PlayerState.Pos.Z }
                        )
                    );
                    hasChanges = true;
                    break;
                case PlayerAreaMaskEntry:
                    hasChanges |= SettingWriter.WriteInt(entry, PlayerState.Pos.Area.AreaMask);
                    break;
                case StrengthLevelEntry:
                    hasChanges |= SettingWriter.WriteInt(entry, PlayerState.StrengthLevel);
                    break;
                // The Game Leaves Out A Healthy Player's Sickness
                case SicknessEntry when PlayerState.Sickness == 0:
                    hasChanges |= SettingWriter.RemoveFloat(entry);
                    break;
                case "TargetHealth":
                    hasChanges |= SettingWriter.WriteFloat(entry, PlayerState.CurrentHealth);
                    break;
                case { } name when s_floatEntries.TryGetValue(name, out var accessor):
                    hasChanges |= SettingWriter.WriteFloat(entry, accessor.Get(PlayerState));
                    break;
            }
        }

        if (hasChanges)
        {
            savegame!
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)!
                .MarkAsModified(Constants.JsonKeys.PlayerState);
        }

        return hasChanges;
    }
}
