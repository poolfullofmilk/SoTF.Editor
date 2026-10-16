using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Pages;

public class WeatherPageViewModel
{
    public List<GenericSetting> Settings { get; } = [];

    public void Load(Savegame? savegame)
    {
        Settings.Clear();
        var saveDataWrapper = savegame?.SavegameStore.LoadJsonRaw(
            SavegameStore.FileType.WeatherSystemSaveData
        );
        if (
            saveDataWrapper?.GetJsonBasedToken(Constants.JsonKeys.WeatherSystem)
            is not { } weatherSystem
        )
        {
            return;
        }

        LoadSettings(weatherSystem);
    }

    private void LoadSettings(JToken weatherSystem)
    {
        // Missing Fields Show Their Default, Spring Has No Season
        Settings.Add(
            EnumSetting(
                "_currentSeason",
                weatherSystem,
                [(0, "Spring"), (1, "Summer"), (2, "Autumn"), (3, "Winter")]
            )
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

        if (!Merge(weatherSystem, Settings))
        {
            return false;
        }

        saveDataWrapper.MarkAsModified(Constants.JsonKeys.WeatherSystem);
        return true;
    }

    private static bool Merge(JToken weatherSystem, IEnumerable<GenericSetting> settings)
    {
        return settings.Aggregate(
            false,
            (current, setting) => setting.MergeTo(weatherSystem) || current
        );
    }
}
