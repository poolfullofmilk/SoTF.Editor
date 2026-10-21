using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.SaveData;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public partial class GameStatePageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private readonly GameData _gameData;

    // The Order The Page Shows Them In
    private static readonly string[] s_settingNames =
    [
        "GameName",
        "GameType",
        "SaveTime",
        "GameDays",
        "GameHours",
        "GameMinutes",
        "GameSeconds",
        "GameMilliseconds",
        "CrashSite",
        "IsRobbyDead",
        "IsVirginiaDead",
    ];

    public GameStatePageViewModel(GameData gameData)
    {
        _gameData = gameData;
        PrefillNamedIntDatas(gameData.NamedIntKeys);
    }

    public List<GenericSetting> Settings { get; } = [];
    public List<GenericSetting> NamedIntDatas { get; } = [];

    private IEnumerable<GenericSetting> AllSettings => Settings.Concat(NamedIntDatas);

    private readonly LoadedValues _loaded = new();

    public bool HasChanges => AllSettings.Any(IsChanged);

    public int ChangeCount => AllSettings.Count(IsChanged);

    public string ValueState =>
        string.Join('\n', AllSettings.Select(setting => LoadedValues.Format(setting.GetValue())));

    public bool IsChanged(GenericSetting setting)
    {
        return _loaded.IsChanged(setting.Name, setting.GetValue());
    }

    private void Remember()
    {
        _loaded.Clear();
        foreach (var setting in AllSettings)
        {
            _loaded.Remember(setting.Name, setting.GetValue());
        }
    }

    private void PrefillNamedIntDatas(List<string> namedIntKeys)
    {
        foreach (var namedIntKey in namedIntKeys)
        {
            NamedIntDatas.Add(
                new GenericSetting(namedIntKey, GenericSetting.DataType.Integer)
                {
                    IntValue = null,
                    MaxInt = 3,
                }
            );
        }
    }

    public string ResetContainers()
    {
        // A Container Without A State Is Closed And Full Again
        var numFixed = NamedIntDatas.RemoveAll(setting =>
            ResetCrateNameIdPattern().IsMatch(setting.Name)
        );
        return numFixed < 1
            ? AppText.Get("NothingToReset")
            : AppText.Format("ContainersReset", numFixed);
    }

    public void SetToEndgame()
    {
        foreach (var name in new[] { "Endgame.StayedOnIsland", "Endgame.CoreGameCompleted" })
        {
            var matches = NamedIntDatas.Where(setting => setting.Name == name).ToList();
            if (matches.Count == 0)
            {
                var setting = new GenericSetting(name, GenericSetting.DataType.Integer);
                NamedIntDatas.Add(setting);
                matches.Add(setting);
            }

            matches.ForEach(setting => setting.IntValue = 1);
        }
    }

    public void Load(Savegame? savegame)
    {
        LoadSettings(savegame);
        Remember();
    }

    public void RefreshDeathFlags(Savegame savegame)
    {
        // Revive Writes These Straight Into The Save, The Rows Follow
        if (
            savegame
                .SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.GameState)
            is not { } gameState
        )
        {
            return;
        }

        for (var index = 0; index < Settings.Count; index++)
        {
            if (
                Settings[index].Name is "IsRobbyDead" or "IsVirginiaDead"
                && CreateSetting(Settings[index].Name, gameState[Settings[index].Name])
                    is { } refreshed
            )
            {
                Settings[index] = refreshed;
            }
        }
    }

    private void LoadSettings(Savegame? savegame)
    {
        Settings.Clear();
        NamedIntDatas.Clear();
        PrefillNamedIntDatas(_gameData.NamedIntKeys);
        if (
            savegame
                ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.GameState)
            is not { } gameState
        )
        {
            return;
        }

        // A Field The Game Left Out Shows At Its Default
        Settings.AddRange(
            s_settingNames
                .Select(name => CreateSetting(name, gameState[name]))
                .OfType<GenericSetting>()
        );
        LoadNamedIntDatas(gameState);
    }

    private static GenericSetting? CreateSetting(string name, JToken? value)
    {
        return name switch
        {
            "GameName" when value != null => new GenericSetting(
                name,
                GenericSetting.DataType.String,
                name
            )
            {
                StringValue = value.ToString(),
            },
            "GameType" or "SaveTime" when value != null => new GenericSetting(
                name,
                GenericSetting.DataType.ReadOnly,
                name
            )
            {
                StringValue = ReadOnlyText(value),
            },
            "IsRobbyDead" or "IsVirginiaDead" => new GenericSetting(
                name,
                GenericSetting.DataType.ReadOnly,
                name
            )
            {
                StringValue = AppText.Get(value?.Value<bool>() == true ? "Yes" : "No"),
            },
            "GameDays" => IntegerSetting(name, value, 999999),
            "GameHours" => IntegerSetting(name, value, 23),
            "GameMinutes" or "GameSeconds" => IntegerSetting(name, value, 59),
            "GameMilliseconds" => IntegerSetting(name, value, 999),
            "CrashSite" when value != null => new GenericSetting(
                name,
                GenericSetting.DataType.Enum,
                name
            )
            {
                StringValue = value.ToString(),
                SelectedItem = value.ToString(),
                PossibleValues =
                {
                    { "tree", AppText.Get("CrashSiteTree") },
                    { "ocean", AppText.Get("CrashSiteOcean") },
                    { "snow", AppText.Get("CrashSiteSnow") },
                },
            },
            _ => null,
        };
    }

    private static GenericSetting IntegerSetting(string name, JToken? value, int max)
    {
        return new GenericSetting(name, GenericSetting.DataType.Integer, name)
        {
            IntValue = value?.Value<int>() ?? 0,
            MinInt = 0,
            MaxInt = max,
        };
    }

    private static string ReadOnlyText(JToken value)
    {
        return value.Type switch
        {
            JTokenType.Date => Formatter.FormatDateTime(value.Value<DateTime>()),
            JTokenType.Boolean => AppText.Get(value.Value<bool>() ? "Yes" : "No"),
            _ => value.ToString(),
        };
    }

    private void LoadNamedIntDatas(JToken gameState)
    {
        if (gameState["NamedIntDatas"] is not { } namedIntDatas)
        {
            return;
        }

        var namedIntDatasByName = NamedIntDatas.ToDictionary(setting => setting.Name);
        foreach (var namedIntData in namedIntDatas)
        {
            if (namedIntData["SaveObjectNameId"]?.Value<string>() is not { } nameId)
            {
                continue;
            }

            // The Game Leaves Out A Zero Value
            var saveValue = namedIntData["SaveValue"]?.Value<int>() ?? 0;
            if (saveValue > 3)
            {
                s_logger.Warn($"Found Save Value {saveValue} Above 3 For {nameId}");
            }

            if (namedIntDatasByName.TryGetValue(nameId, out var known))
            {
                known.IntValue = saveValue;
                continue;
            }

            s_logger.Info($"Found New Named Int Data {nameId}");
            NamedIntDatas.Add(
                new GenericSetting(nameId, GenericSetting.DataType.Integer, namedIntData.Path)
                {
                    IntValue = saveValue,
                    MaxInt = Math.Max(saveValue, 3),
                }
            );
        }
    }

    public bool Update(Savegame savegame)
    {
        var saveDataWrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.GameStateSaveData
        );
        if (saveDataWrapper?.GetJsonBasedToken(Constants.JsonKeys.GameState) is not { } gameState)
        {
            return false;
        }

        if (!Merge(gameState, Settings, NamedIntDatas))
        {
            return false;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.GameState);
        return true;
    }

    private static bool Merge(
        JToken gameState,
        IEnumerable<GenericSetting> newSettings,
        IEnumerable<GenericSetting> newNamedIntDatas
    )
    {
        var hasChanges = false;
        foreach (var setting in newSettings)
        {
            hasChanges |= setting.MergeTo(gameState);
        }

        if (gameState["NamedIntDatas"] is { } namedIntDataToken)
        {
            hasChanges |= MergeNamedIntDatas(newNamedIntDatas, namedIntDataToken);
        }

        return hasChanges;
    }

    private static bool MergeNamedIntDatas(
        IEnumerable<GenericSetting> newNamedIntDatas,
        JToken namedIntDataToken
    )
    {
        var existingNamedIntData = namedIntDataToken.ToObject<List<NamedIntData>>() ?? [];
        var newNamedIntData = newNamedIntDatas
            .Where(setting => setting.IntValue != null)
            .Select(setting => new NamedIntData(setting.Name, setting.IntValue!.Value))
            .ToList();

        // Sorting Alone Is No Change, The Game Keeps Its Order
        if (newNamedIntData.ToHashSet().SetEquals(existingNamedIntData))
        {
            return false;
        }

        namedIntDataToken.Replace(JToken.FromObject(newNamedIntData));

        return true;
    }

    [GeneratedRegex(@"(\..*Crate.*\.)|(\..*Storage.*\.)|(\..*Case.*\.)|(.*\.Meds\..*)")]
    private static partial Regex ResetCrateNameIdPattern();
}
