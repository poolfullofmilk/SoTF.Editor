using System.Reflection;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Savegames;
using static SoTF.Editor.Data.Helpers.Constants.Settings;
using static SoTF.Editor.Data.Helpers.Constants.SettingValueKeys;

namespace SoTF.Editor.Services.Pages;

public class PlayerPageViewModel
{
    private const string PlayerPositionEntry = "player.position";
    private const string PlayerAreaMaskEntry = "player.areaMask";
    private const string PlayerRotationEntry = "player.rotation";
    private const string StrengthLevelEntry = "StrengthLevel";
    private const string SicknessEntry = "Sickness";
    private const string CurrentHealthEntry = "CurrentHealth";
    private const string RaftEntry = "player.localToRaftUniqueId";
    private const string TargetHealthEntry = "TargetHealth";
    private const float FullBar = 100;
    private const float FullBuff = 65535;

    // Float Entries The Page Edits, Each Named Like Its Property
    private static readonly Dictionary<string, PropertyInfo> s_floatEntries = new[]
    {
        nameof(PlayerState.MaxHealth),
        nameof(PlayerState.CurrentHealth),
        nameof(PlayerState.Hydration),
        nameof(PlayerState.HydrationBuff),
        nameof(PlayerState.Fullness),
        nameof(PlayerState.FullnessBuff),
        nameof(PlayerState.Rest),
        nameof(PlayerState.RestBuff),
        nameof(PlayerState.Stamina),
        nameof(PlayerState.Sickness),
    }.ToDictionary(name => name, name => typeof(PlayerState).GetProperty(name)!);

    private readonly List<Item> _availableClothes = [];

    // Worn Pieces That Are No Outfit, Like The Rebreather
    private readonly List<int> _otherWornItemIds = [];

    private readonly GameData _gameData;
    private readonly LoadedValues _loaded = new();

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

    public bool HasChanges => _loaded.AnyPropertyChanged(nameof(PlayerState), PlayerState);

    public IEnumerable<string> ChangedNames =>
        _loaded
            .ChangedProperties(nameof(PlayerState), PlayerState)
            .Select(name =>
                AppText.Get(
                    name switch
                    {
                        nameof(PlayerState.SelectedCloth) => "Outfit",
                        nameof(PlayerState.Pos) => "Position",
                        _ => name,
                    }
                )
            );

    public bool IsChanged(string propertyName)
    {
        return _loaded.IsPropertyChanged(nameof(PlayerState), PlayerState, propertyName);
    }

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

        _loaded.Clear();
        _loaded.RememberProperties(nameof(PlayerState), PlayerState);
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
        followerState.Pos = followerState.Pos.ForTeleport();
    }

    public void FillAllBars()
    {
        // Strength First, Then Health Fills To Its New Max
        PlayerState.ChangeStrengthLevel(PlayerState.MaxStrengthLevel);
        PlayerState.CurrentHealth = PlayerState.MaxHealth;
        PlayerState.Fullness = FullBar;
        PlayerState.Hydration = FullBar;
        PlayerState.Rest = FullBar;
        PlayerState.Stamina = FullBar;
        PlayerState.HydrationBuff = FullBuff;
        PlayerState.RestBuff = 1;
        PlayerState.FullnessBuff = FullBuff;
        PlayerState.Sickness = 0;
    }

    // Every Bar Already Full Leaves The Button Nothing To Do
    public bool CanFillAllBars =>
        PlayerState.StrengthLevel != PlayerState.MaxStrengthLevel
        || PlayerState.MaxHealth != PlayerState.MaxHealthAt(PlayerState.MaxStrengthLevel)
        || PlayerState.CurrentHealth < PlayerState.MaxHealth
        || PlayerState.Fullness != FullBar
        || PlayerState.Hydration != FullBar
        || PlayerState.Rest != FullBar
        || PlayerState.Stamina != FullBar
        || PlayerState.HydrationBuff != FullBuff
        || PlayerState.RestBuff != 1
        || PlayerState.FullnessBuff != FullBuff
        || PlayerState.Sickness != 0;

    private static Position? PositionOf(JToken entry)
    {
        return entry[FloatArrayValue]?.ToObject<float[]>() is { Length: 3 } values
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
                    areaMask = SettingEntry.ReadInt(entry) ?? 0;
                    break;
                case PlayerRotationEntry
                    when entry[FloatArrayValue]?.ToObject<float[]>()
                        is [var x, var y, var z, var w]:
                    // Unity's Quaternion Turned Into A Turn About The Up Axis
                    PlayerState.Heading =
                        MathF.Atan2(2 * (w * y + x * z), 1 - 2 * (x * x + y * y)) * 180 / MathF.PI;
                    break;
                case StrengthLevelEntry:
                    PlayerState.StrengthLevel = SettingEntry.ReadInt(entry) ?? 0;
                    break;
                case { } name when s_floatEntries.TryGetValue(name, out var property):
                    property.SetValue(PlayerState, SettingEntry.ReadFloat(entry) ?? 0f);
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

    private bool AddMissingEntries(JArray entries)
    {
        // A Missing Entry Is Added Only Off Its Default
        var names = entries.Select(entry => entry["Name"]?.ToString()).OfType<string>().ToHashSet();
        var added = false;
        void Add(string name, int settingType, string valueKey, JToken value)
        {
            if (names.Add(name))
            {
                entries.Add(
                    new JObject
                    {
                        ["Name"] = name,
                        ["SettingType"] = settingType,
                        [valueKey] = value,
                    }
                );
                added = true;
            }
        }

        foreach (var (name, property) in s_floatEntries)
        {
            if (
                !names.Contains(name)
                && (float)property.GetValue(PlayerState)! is not 0 and var value
            )
            {
                // A New Current Health Brings Its Target, As Edits Do
                if (name == CurrentHealthEntry)
                {
                    Add(TargetHealthEntry, SettingTypeFloat, FloatValue, value);
                }

                Add(name, SettingTypeFloat, FloatValue, value);
            }
        }

        if (PlayerState.StrengthLevel != 0)
        {
            Add(StrengthLevelEntry, SettingTypeInt, IntValue, PlayerState.StrengthLevel);
        }

        if (PlayerState.Pos.Area.AreaMask != 0)
        {
            Add(PlayerAreaMaskEntry, SettingTypeInt, IntValue, PlayerState.Pos.Area.AreaMask);
        }

        if (PlayerState.Pos is not { X: 0, Y: 0, Z: 0 })
        {
            Add(
                PlayerPositionEntry,
                SettingTypeFloatArray,
                FloatArrayValue,
                JToken.FromObject(new[] { PlayerState.Pos.X, PlayerState.Pos.Y, PlayerState.Pos.Z })
            );
        }

        return added;
    }

    private bool UpdatePlayerState(Savegame? savegame)
    {
        if (EntriesOf(savegame) is not { } entries)
        {
            return false;
        }

        // Target Health Follows Only A Changed Current Health
        var currentHealthChanged = entries.Any(entry =>
            entry["Name"]?.ToString() == CurrentHealthEntry
            && Math.Abs((SettingEntry.ReadFloat(entry) ?? 0f) - PlayerState.CurrentHealth) >= 0.001
        );
        var hasChanges = false;
        var positionChanged = false;
        foreach (var entry in entries)
        {
            switch (entry["Name"]?.ToString())
            {
                // The Area Lives In Its Own Entry Below
                case PlayerPositionEntry
                    when PositionOf(entry) is { } oldPlayerPos
                        && !oldPlayerPos.HasSameCoordinates(PlayerState.Pos):
                    entry[FloatArrayValue]!.Replace(
                        JToken.FromObject(
                            new[] { PlayerState.Pos.X, PlayerState.Pos.Y, PlayerState.Pos.Z }
                        )
                    );
                    hasChanges = true;
                    positionChanged = true;
                    break;
                case PlayerAreaMaskEntry:
                    hasChanges |= SettingEntry.WriteInt(entry, PlayerState.Pos.Area.AreaMask);
                    break;
                case StrengthLevelEntry:
                    hasChanges |= SettingEntry.WriteInt(entry, PlayerState.StrengthLevel);
                    break;
                // The Game Leaves Out A Healthy Player's Sickness
                case SicknessEntry when PlayerState.Sickness == 0:
                    hasChanges |= SettingEntry.RemoveFloat(entry);
                    break;
                case TargetHealthEntry when currentHealthChanged:
                    hasChanges |= JsonModifier.SetFloat(
                        entry,
                        Constants.SettingValueKeys.FloatValue,
                        PlayerState.CurrentHealth
                    );
                    break;
                case { } name when s_floatEntries.TryGetValue(name, out var property):
                    hasChanges |= JsonModifier.SetFloat(
                        entry,
                        Constants.SettingValueKeys.FloatValue,
                        (float)property.GetValue(PlayerState)!
                    );
                    break;
            }
        }

        hasChanges |= AddMissingEntries(entries);

        // Moving Clears The Raft Link, Else The Game Snaps Back
        if (
            positionChanged
            && entries.FirstOrDefault(entry => entry["Name"]?.ToString() == RaftEntry)
                is JObject raftEntry
            && SettingEntry.ReadString(raftEntry) is { } raftId
            && raftId != Guid.Empty.ToString()
        )
        {
            raftEntry["StringValue"] = Guid.Empty.ToString();
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
