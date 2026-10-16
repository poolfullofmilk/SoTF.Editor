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

    private readonly Regex _resetCrateNameIdPattern = ResetCrateNameIdPattern();

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
        var filtered = NamedIntDatas
            .Where(setting => !_resetCrateNameIdPattern.IsMatch(setting.Name))
            .ToList();

        var numFixed = NamedIntDatas.Count - filtered.Count;

        if (numFixed < 1)
        {
            return AppText.Get("NothingToReset");
        }

        NamedIntDatas.Clear();

        filtered.ForEach(setting => NamedIntDatas.Add(setting));

        return AppText.Format("ContainersReset", numFixed);
    }

    public void SetToEndgame()
    {
        GenericSetting? stayedOnIslandSetting = null;
        GenericSetting? coreGameCompletedSetting = null;

        foreach (var namedIntData in NamedIntDatas)
        {
            switch (namedIntData.Name)
            {
                case "Endgame.StayedOnIsland":
                    stayedOnIslandSetting = namedIntData;
                    stayedOnIslandSetting.IntValue = 1;
                    break;
                case "Endgame.CoreGameCompleted":
                    coreGameCompletedSetting = namedIntData;
                    coreGameCompletedSetting.IntValue = 1;
                    break;
            }
        }

        if (stayedOnIslandSetting == null)
        {
            var namedIntData = CreateNamedIntData("Endgame.StayedOnIsland");
            namedIntData.IntValue = 1;
        }

        if (coreGameCompletedSetting == null)
        {
            var namedIntData = CreateNamedIntData("Endgame.CoreGameCompleted");
            namedIntData.IntValue = 1;
        }
    }

    private GenericSetting CreateNamedIntData(string name)
    {
        var setting = new GenericSetting(name, GenericSetting.DataType.Integer);
        NamedIntDatas.Add(setting);
        return setting;
    }

    public void Load(Savegame? savegame)
    {
        Settings.Clear();
        NamedIntDatas.Clear();
        PrefillNamedIntDatas(_gameData.NamedIntKeys);
        var saveDataWrapper = savegame?.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.GameStateSaveData
        );
        if (saveDataWrapper?.GetJsonBasedToken(Constants.JsonKeys.GameState) is not { } gameState)
        {
            return;
        }

        LoadSettings(gameState);
    }

    private void LoadSettings(JToken gameState)
    {
        // A Field The Game Left Out Shows At Its Default
        foreach (var name in s_settingNames)
        {
            if (CreateSetting(name, gameState[name]) is { } setting)
            {
                Settings.Add(setting);
            }
        }

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

        var storedNamedIntSettings = new List<GenericSetting>();

        foreach (var namedIntData in namedIntDatas)
        {
            var nameId = namedIntData["SaveObjectNameId"]?.Value<string>();
            if (nameId == null)
            {
                continue;
            }

            // The Game Leaves Out A Zero Value
            var saveValue = namedIntData["SaveValue"]?.Value<int>() ?? 0;

            if (saveValue > 3)
            {
                s_logger.Warn($"Found Save Value {saveValue} Above 3 For {nameId}");
            }

            storedNamedIntSettings.Add(
                new GenericSetting(nameId, GenericSetting.DataType.Integer, namedIntData.Path)
                {
                    IntValue = saveValue,
                    MaxInt = saveValue > 3 ? saveValue : 3,
                }
            );
        }

        var namedIntDatasByName = NamedIntDatas.ToDictionary(setting => setting.Name);

        foreach (var storedNamedIntSetting in storedNamedIntSettings)
        {
            if (!namedIntDatasByName.TryGetValue(storedNamedIntSetting.Name, out var value))
            {
                s_logger.Info($"Found New Named Int Data {storedNamedIntSetting.Name}");
                NamedIntDatas.Add(storedNamedIntSetting);
            }
            else
            {
                value.IntValue = storedNamedIntSetting.IntValue;
            }
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
        hasChanges =
            newSettings.Aggregate(
                false,
                (current, setting) => setting.MergeTo(gameState) || current
            ) || hasChanges;

        if (gameState["NamedIntDatas"] is { } namedIntDataToken)
        {
            hasChanges = MergeNamedIntDatas(newNamedIntDatas, namedIntDataToken) || hasChanges;
        }

        return hasChanges;
    }

    private static bool MergeNamedIntDatas(
        IEnumerable<GenericSetting> newNamedIntDatas,
        JToken namedIntDataToken
    )
    {
        var existingNamedIntData =
            namedIntDataToken.ToObject<List<NamedIntData>>()
            ?? Enumerable.Empty<NamedIntData>().ToList();
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
