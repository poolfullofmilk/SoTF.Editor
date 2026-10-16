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
        var children = weatherSystem.Children();
        foreach (var child in children.OfType<JProperty>())
        {
            var setting = child.Name switch
            {
                "_isRaining" or "_rainBlocked" => new GenericSetting(
                    child.Name,
                    GenericSetting.DataType.Boolean,
                    child.Path
                )
                {
                    BoolValue = child.Value.Value<bool>(),
                },
                "_cloudState" => new GenericSetting(
                    child.Name,
                    GenericSetting.DataType.Enum,
                    child.Path
                )
                {
                    SelectedItem = child.Value.Value<int>(),
                    PossibleValues =
                    {
                        { 0, AppText.Get("CloudsClear") },
                        { 1, AppText.Get("CloudsGrowing") },
                        { 2, AppText.Get("CloudsClearing") },
                        { 3, AppText.Get("CloudsRaining") },
                    },
                },
                "_currentRainType" => new GenericSetting(
                    child.Name,
                    GenericSetting.DataType.Enum,
                    child.Path
                )
                {
                    SelectedItem = child.Value.Value<int>(),
                    PossibleValues =
                    {
                        { 0, AppText.Get("None") },
                        { 1, AppText.Get("RainLight") },
                        { 2, AppText.Get("RainMedium") },
                        { 3, AppText.Get("RainHeavy") },
                    },
                },
                "_currentSeason" => new GenericSetting(
                    child.Name,
                    GenericSetting.DataType.Enum,
                    child.Path
                )
                {
                    SelectedItem = child.Value.Value<int>(),
                    PossibleValues =
                    {
                        { 0, AppText.Get("Spring") },
                        { 1, AppText.Get("Summer") },
                        { 2, AppText.Get("Autumn") },
                        { 3, AppText.Get("Winter") },
                    },
                },
                _ => null,
            };

            if (setting != null)
            {
                Settings.Add(setting);
            }
        }
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
