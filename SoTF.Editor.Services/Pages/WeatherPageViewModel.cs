using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class WeatherPageViewModel
{
    public List<GenericSetting> Settings { get; } = [];

    private IEnumerable<GenericSetting> AllSettings => Settings;

    private readonly LoadedValues _loaded = new();

    public bool HasChanges => AllSettings.Any(IsChanged);

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

    public void Load(Savegame? savegame)
    {
        LoadSettings(savegame);
        Remember();
    }

    private void LoadSettings(Savegame? savegame)
    {
        Settings.Clear();
        if (
            savegame
                ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.WeatherSystemSaveData)
                ?.GetJsonBasedToken(Constants.JsonKeys.WeatherSystem)
            is not { } weatherSystem
        )
        {
            return;
        }

        // Seasons Follow Days Plus This Offset, Saved As 5.0
        Settings.Add(
            new GenericSetting(
                "_startingDayOffset",
                GenericSetting.DataType.Decimal,
                "_startingDayOffset"
            )
            {
                DecimalValue = weatherSystem["_startingDayOffset"]?.Value<double>() ?? 0,
                MaxInt = 1000,
            }
        );
        Settings.Add(BoolSetting("_isRaining", weatherSystem));
        Settings.Add(BoolSetting("_rainBlocked", weatherSystem));
        Settings.Add(
            EnumSetting(
                "_cloudState",
                weatherSystem,
                [
                    (0, "CloudsClear"),
                    (1, "CloudsGrowing"),
                    (2, "CloudsClearing"),
                    (3, "CloudsRaining"),
                ]
            )
        );
        Settings.Add(
            EnumSetting(
                "_currentRainType",
                weatherSystem,
                [(0, "None"), (1, "RainLight"), (2, "RainMedium"), (3, "RainHeavy")]
            )
        );
    }

    private static GenericSetting BoolSetting(string name, JToken weatherSystem)
    {
        return new GenericSetting(name, GenericSetting.DataType.Boolean, name)
        {
            BoolValue = weatherSystem[name]?.Value<bool>() ?? false,
        };
    }

    private static GenericSetting EnumSetting(
        string name,
        JToken weatherSystem,
        (int Value, string LabelKey)[] options
    )
    {
        var setting = new GenericSetting(name, GenericSetting.DataType.Enum, name)
        {
            SelectedItem = weatherSystem[name]?.Value<int>() ?? 0,
        };
        foreach (var (value, labelKey) in options)
        {
            setting.PossibleValues[value] = AppText.Get(labelKey);
        }

        return setting;
    }

    public bool Update(Savegame savegame)
    {
        var saveDataWrapper = savegame.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.WeatherSystemSaveData
        );
        if (
            saveDataWrapper?.GetJsonBasedToken(Constants.JsonKeys.WeatherSystem)
            is not { } weatherSystem
        )
        {
            return false;
        }

        var hasChanges = false;
        foreach (var setting in Settings)
        {
            hasChanges |= setting.MergeTo(weatherSystem);
        }

        if (hasChanges)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.WeatherSystem);
        }

        return hasChanges;
    }
}
