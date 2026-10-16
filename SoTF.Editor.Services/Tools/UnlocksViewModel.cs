using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public class UnlocksViewModel
{
    private const string SettingsKey = "_settings";
    private const string BoolValueKey = "BoolValue";
    private const string NameKey = "Name";
    private const string CreativeModeItemUnlockedKey = "Stats.CreativeModeItemUnlocked";
    private const string CoreGameCompletedKey = "Stats.CoreGameCompleted";
    private const string CreativeModeUnlockedKey = "Stats.CreativeModeUnlocked";
    private const string EscapedIslandKey = "Stats.EscapedIsland";
    private readonly string _playerProfilePath;

    public UnlocksViewModel(string playerProfilePath)
    {
        _playerProfilePath = playerProfilePath;
        Load(playerProfilePath);
    }

    public bool CoreGameCompleted { get; set; }

    public bool CreativeMode { get; set; }

    public bool CreativeModeItem { get; set; }

    public bool EscapedIsland { get; set; }

    private void Load(string playerProfilePath)
    {
        if (!File.Exists(playerProfilePath))
        {
            return;
        }

        if (
            SaveJson.DeserializeJObjectFromFile(playerProfilePath)[SettingsKey]
            is not JArray settings
        )
        {
            return;
        }

        foreach (var setting in settings)
        {
            if (setting[NameKey]?.Value<string>() is not { } propertyName)
            {
                continue;
            }

            switch (propertyName)
            {
                case CreativeModeItemUnlockedKey:
                    CreativeModeItem = GetBoolSetting(setting);
                    break;
                case CreativeModeUnlockedKey:
                    CreativeMode = GetBoolSetting(setting);
                    break;
                case CoreGameCompletedKey:
                    CoreGameCompleted = GetBoolSetting(setting);
                    break;
                case EscapedIslandKey:
                    EscapedIsland = GetBoolSetting(setting);
                    break;
            }
        }
    }

    private static bool GetBoolSetting(JToken setting)
    {
        return setting[BoolValueKey]?.Value<bool>() ?? false;
    }

    public void Save()
    {
        var model = File.Exists(_playerProfilePath)
            ? SaveJson.DeserializeJObjectFromFile(_playerProfilePath)
            : new JObject();

        if (model[SettingsKey] is not JArray settings)
        {
            settings = [];
            model[SettingsKey] = settings;
        }

        var settingsToSave = new Dictionary<string, bool>
        {
            { CreativeModeItemUnlockedKey, CreativeModeItem },
            { CreativeModeUnlockedKey, CreativeMode },
            { CoreGameCompletedKey, CoreGameCompleted },
            { EscapedIslandKey, EscapedIsland },
        };

        foreach (var setting in settings)
        {
            if (
                setting[NameKey]?.Value<string>() is not { } settingName
                || !settingsToSave.TryGetValue(settingName, out var settingValue)
            )
            {
                continue;
            }

            setting[BoolValueKey] = settingValue;
            settingsToSave.Remove(settingName);
        }

        foreach (var (settingKey, settingValue) in settingsToSave)
        {
            var newSetting = new JObject { [NameKey] = settingKey, [BoolValueKey] = settingValue };

            settings.Add(newSetting);
        }

        // A Verified Copy First, Then A Write That Cannot Tear
        if (File.Exists(_playerProfilePath))
        {
            BackupManager.CreateForFile(_playerProfilePath);
        }

        var temporary = _playerProfilePath + ".tmp";
        File.WriteAllText(
            temporary,
            JsonConvert.SerializeObject(model, Formatting.Indented),
            SaveJson.Encoding
        );
        File.Move(temporary, _playerProfilePath, true);
    }
}
