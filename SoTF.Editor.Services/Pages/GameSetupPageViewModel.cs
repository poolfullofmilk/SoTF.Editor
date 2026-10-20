using Newtonsoft.Json.Linq;
using SoTF.Editor.Services.Savegames;
using static SoTF.Editor.Data.Helpers.Constants;
using static SoTF.Editor.Data.Helpers.Constants.Settings;

namespace SoTF.Editor.Services.Pages;

public class GameSetupPageViewModel
{
    private const string SettingsKey = "_settings";
    public const string CustomGameMode = "Custom";
    public const string CreativeGameMode = "Creative";
    public const string PeacefulGameMode = "Peaceful";
    public const string HardGameMode = "Hard";
    public const string HardSurvivalGameMode = "HardSurvival";
    public const string SettingValueHigh = "High";
    public const string SettingValueDefault = "Default";
    public const string SettingValueLOW = "LOW";
    public const string SettingValueNORMAL = "NORMAL";
    public const string SettingValueNormal = "Normal";
    public const string SettingValueReduced = "Reduced";
    public const string SettingValueOff = "Off";
    public const string SettingValueHard = "Hard";
    private const string NameKey = "Name";
    private const string SettingTypeKey = "SettingType";

    // The Settings The Editor Knows, Each With Its Value Type
    private static readonly Dictionary<string, int> s_settingTypes = new()
    {
        { GameSetupKeys.Mode, SettingTypeString },
        { GameSetupKeys.Uid, SettingTypeString },
        { GameSetupKeys.EnemyHealth, SettingTypeString },
        { GameSetupKeys.EnemyDamage, SettingTypeString },
        { GameSetupKeys.EnemyArmour, SettingTypeString },
        { GameSetupKeys.EnemyAggression, SettingTypeString },
        { GameSetupKeys.AnimalSpawnRate, SettingTypeString },
        { GameSetupKeys.StartingSeason, SettingTypeString },
        { GameSetupKeys.SeasonLength, SettingTypeString },
        { GameSetupKeys.DayLength, SettingTypeString },
        { GameSetupKeys.PrecipitationFrequency, SettingTypeString },
        { GameSetupKeys.ConsumableEffects, SettingTypeString },
        { GameSetupKeys.PlayerStatsDamage, SettingTypeString },
        { GameSetupKeys.EnemySpawn, SettingTypeBool },
        { GameSetupKeys.PausedInInventory, SettingTypeBool },
        { GameSetupKeys.ReducedFoodInContainers, SettingTypeBool },
        { GameSetupKeys.ReducedAmmoInContainers, SettingTypeBool },
        { GameSetupKeys.SingleUseContainers, SettingTypeBool },
        { GameSetupKeys.ColdPenalties, SettingTypeString },
        { GameSetupKeys.EnemySearchParties, SettingTypeString },
        { GameSetupKeys.PlayersTriggerTraps, SettingTypeBool },
        { GameSetupKeys.BuildingResistance, SettingTypeString },
        { GameSetupKeys.CreativeMode, SettingTypeBool },
        { GameSetupKeys.PlayersImmortalMode, SettingTypeBool },
        { GameSetupKeys.OneHitToCutTrees, SettingTypeBool },
        { GameSetupKeys.ForcePlaceFullLoad, SettingTypeBool },
        { GameSetupKeys.NoCuttingsSpawn, SettingTypeBool },
        { GameSetupKeys.PvpDamage, SettingTypeString },
        { GameSetupKeys.StatRegenerationPenalty, SettingTypeString },
        { GameSetupKeys.MultiplayerCheats, SettingTypeBool },
    };

    private readonly Dictionary<string, string> _stringSettings = [];
    private readonly Dictionary<string, bool> _boolSettings = [];
    private readonly Dictionary<string, string> _loadedStrings = [];
    private readonly Dictionary<string, bool> _loadedBools = [];

    public string? SelectedMode
    {
        get => GetString(GameSetupKeys.Mode, CustomGameMode);
        set => SetString(GameSetupKeys.Mode, value);
    }

    public string Uid => GetString(GameSetupKeys.Uid, "");

    public string GetString(string key, string fallback)
    {
        return _stringSettings.GetValueOrDefault(key) ?? fallback;
    }

    public void SetString(string key, string? value)
    {
        if (value == null)
        {
            _stringSettings.Remove(key);
        }
        else
        {
            _stringSettings[key] = value;
        }
    }

    public bool GetBool(string key, bool fallback)
    {
        return _boolSettings.TryGetValue(key, out var value) ? value : fallback;
    }

    public void SetBool(string key, bool value)
    {
        _boolSettings[key] = value;
    }

    public bool HasAny(IEnumerable<string> keys)
    {
        return keys.Any(key => _stringSettings.ContainsKey(key) || _boolSettings.ContainsKey(key));
    }

    public void ResetSettings(IEnumerable<string> keys)
    {
        // A Setting The Save Lacks Reads As Its Default
        foreach (var key in keys)
        {
            _stringSettings.Remove(key);
            _boolSettings.Remove(key);
        }
    }

    public void ApplyPreset(string mode)
    {
        // A Preset Fills Every Setting, Custom Keeps Them Editable
        SelectedMode = mode;
        PrepareSettingsBeforeMerge();
        if (mode == CreativeGameMode)
        {
            _boolSettings[GameSetupKeys.CreativeMode] = true;
        }

        SelectedMode = CustomGameMode;
    }

    public bool HasChanges =>
        !SameEntries(_stringSettings, _loadedStrings) || !SameEntries(_boolSettings, _loadedBools);

    public int ChangeCount =>
        _stringSettings
            .Keys.Union(_loadedStrings.Keys)
            .Count(key =>
                _stringSettings.GetValueOrDefault(key) != _loadedStrings.GetValueOrDefault(key)
            )
        + _boolSettings
            .Keys.Union(_loadedBools.Keys)
            .Count(key =>
                _boolSettings.TryGetValue(key, out var current)
                    != _loadedBools.TryGetValue(key, out var loaded)
                || current != loaded
            );

    public bool IsChanged(string key, string fallback)
    {
        return GetString(key, fallback) != (_loadedStrings.GetValueOrDefault(key) ?? fallback);
    }

    public bool IsChanged(string key, bool fallback)
    {
        return GetBool(key, fallback)
            != (_loadedBools.TryGetValue(key, out var loaded) ? loaded : fallback);
    }

    private static bool SameEntries<TValue>(
        Dictionary<string, TValue> current,
        Dictionary<string, TValue> loaded
    )
    {
        return current.Count == loaded.Count
            && current.All(pair =>
                loaded.TryGetValue(pair.Key, out var value) && Equals(value, pair.Value)
            );
    }

    public void Load(Savegame? savegame)
    {
        LoadSettings(savegame);
        _loadedStrings.Clear();
        _loadedBools.Clear();
        foreach (var (key, value) in _stringSettings)
        {
            _loadedStrings[key] = value;
        }

        foreach (var (key, value) in _boolSettings)
        {
            _loadedBools[key] = value;
        }
    }

    private void LoadSettings(Savegame? savegame)
    {
        _stringSettings.Clear();
        _boolSettings.Clear();

        if (GetSettings(savegame) is not { } settings)
        {
            return;
        }

        foreach (var setting in settings)
        {
            if (
                setting[NameKey]?.Value<string>() is not { } name
                || !s_settingTypes.TryGetValue(name, out var knownType)
            )
            {
                continue;
            }

            switch ((setting[SettingTypeKey] ?? setting["SettingsType"])?.Value<int>() ?? knownType)
            {
                case SettingTypeString:
                    if (SettingReader.ReadString(setting) is { } stringValue)
                    {
                        _stringSettings[name] = stringValue;
                    }

                    break;
                case SettingTypeBool:
                    // The Game Leaves Out A False Value
                    _boolSettings[name] = SettingReader.ReadBool(setting) ?? false;
                    break;
            }
        }
    }

    private static JToken? GetSettings(Savegame? savegame)
    {
        return savegame
            ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameSetupSaveData)
            ?.GetJsonBasedToken(JsonKeys.GameSetup)
            ?[SettingsKey];
    }

    public bool Update(Savegame savegame)
    {
        PrepareSettingsBeforeMerge();
        var hasChanges = Merge(savegame);

        // A Setup Without A Mode Keeps Its Game Type
        if (!_stringSettings.TryGetValue(GameSetupKeys.Mode, out var mode))
        {
            return hasChanges;
        }

        return savegame.ModifyGameState(new Dictionary<string, object> { { "GameType", mode } })
            || hasChanges;
    }

    private void PrepareSettingsBeforeMerge()
    {
        if (SelectedMode == CustomGameMode)
        {
            return;
        }

        // A Preset Mode Keeps Only Its Own Settings
        foreach (
            var key in _stringSettings
                .Keys.Where(key => key is not (GameSetupKeys.Uid or GameSetupKeys.Mode))
                .ToList()
        )
        {
            _stringSettings.Remove(key);
        }

        _boolSettings.Clear();

        switch (SelectedMode)
        {
            case CreativeGameMode:
                _boolSettings[GameSetupKeys.EnemySpawn] = false;
                _boolSettings[GameSetupKeys.ReducedFoodInContainers] = false;
                _boolSettings[GameSetupKeys.SingleUseContainers] = false;
                _boolSettings[GameSetupKeys.PausedInInventory] = true;
                _boolSettings[GameSetupKeys.PlayersTriggerTraps] = false;
                _stringSettings[GameSetupKeys.AnimalSpawnRate] = SettingValueHigh;
                _stringSettings[GameSetupKeys.ConsumableEffects] = SettingValueOff;
                _stringSettings[GameSetupKeys.PlayerStatsDamage] = SettingValueOff;
                _stringSettings[GameSetupKeys.ColdPenalties] = SettingValueOff;
                _stringSettings[GameSetupKeys.BuildingResistance] = SettingValueHigh;
                break;
            case PeacefulGameMode:
                _boolSettings[GameSetupKeys.EnemySpawn] = false;
                _stringSettings[GameSetupKeys.AnimalSpawnRate] = SettingValueHigh;
                break;
            case HardSurvivalGameMode:
                _boolSettings[GameSetupKeys.EnemySpawn] = true;
                _boolSettings[GameSetupKeys.ReducedFoodInContainers] = true;
                _boolSettings[GameSetupKeys.SingleUseContainers] = true;
                _boolSettings[GameSetupKeys.PausedInInventory] = false;
                _boolSettings[GameSetupKeys.PlayersTriggerTraps] = true;
                _stringSettings[GameSetupKeys.EnemyAggression] = SettingValueHigh;
                _stringSettings[GameSetupKeys.EnemyArmour] = SettingValueHigh;
                _stringSettings[GameSetupKeys.EnemyDamage] = SettingValueHigh;
                _stringSettings[GameSetupKeys.EnemyHealth] = SettingValueNORMAL;
                _stringSettings[GameSetupKeys.AnimalSpawnRate] = SettingValueLOW;
                _stringSettings[GameSetupKeys.ConsumableEffects] = SettingValueHard;
                _stringSettings[GameSetupKeys.PlayerStatsDamage] = SettingValueHard;
                _stringSettings[GameSetupKeys.ColdPenalties] = SettingValueHard;
                break;
            case HardGameMode:
                _boolSettings[GameSetupKeys.EnemySpawn] = true;
                _stringSettings[GameSetupKeys.EnemyAggression] = SettingValueHigh;
                _stringSettings[GameSetupKeys.EnemyArmour] = SettingValueHigh;
                _stringSettings[GameSetupKeys.EnemyDamage] = SettingValueHigh;
                _stringSettings[GameSetupKeys.EnemyHealth] = SettingValueHigh;
                _stringSettings[GameSetupKeys.AnimalSpawnRate] = SettingValueLOW;
                break;
        }
    }

    private bool Merge(Savegame savegame)
    {
        var saveDataWrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.GameSetupSaveData
        );
        if (GetSettings(savegame) is not JArray settings || saveDataWrapper == null)
        {
            return false;
        }

        List<JToken> finalSettings = [];
        HashSet<string> existingSettings = [];
        var hasChanges = false;

        foreach (var setting in settings)
        {
            // Settings The Editor Does Not Know Stay In The Save
            if (
                setting[NameKey]?.Value<string>() is not { } name
                || name.Length == 0
                || !s_settingTypes.ContainsKey(name)
            )
            {
                finalSettings.Add(setting);
                continue;
            }

            existingSettings.Add(name);
            if (_stringSettings.TryGetValue(name, out var newStringValue))
            {
                hasChanges |= SettingReader.ReadString(setting) != newStringValue;
            }
            else if (_boolSettings.TryGetValue(name, out var newBoolValue))
            {
                hasChanges |= (SettingReader.ReadBool(setting) ?? false) != newBoolValue;
            }
            else
            {
                // Back To Its Default, So It Leaves The Save
                hasChanges = true;
            }
        }

        // Known Settings Are Written Fresh As Name, Type And Value
        foreach (var (key, value) in _stringSettings)
        {
            hasChanges |= !existingSettings.Contains(key);
            if (value != "")
            {
                finalSettings.Add(
                    new JObject
                    {
                        { NameKey, key },
                        { SettingTypeKey, SettingTypeString },
                        { SettingValueKeys.StringValue, value },
                    }
                );
            }
        }

        foreach (var (key, value) in _boolSettings)
        {
            hasChanges |= !existingSettings.Contains(key);
            finalSettings.Add(
                new JObject
                {
                    { NameKey, key },
                    { SettingTypeKey, SettingTypeBool },
                    { SettingValueKeys.BoolValue, value },
                }
            );
        }

        if (hasChanges)
        {
            settings.ReplaceAll(finalSettings.ToArray());
            saveDataWrapper.MarkAsModified(JsonKeys.GameSetup);
        }

        return hasChanges;
    }
}
