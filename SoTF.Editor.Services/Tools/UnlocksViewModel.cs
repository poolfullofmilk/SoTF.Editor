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
    private readonly string _playerProfilePath;

    // Each Profile Setting With Its Label, In Dialog Order
    public static IReadOnlyList<(string Key, string LabelKey)> Settings { get; } =
    [
        ("Stats.CoreGameCompleted", "CoreGameCompleted"),
        ("Stats.EscapedIsland", "EscapedIsland"),
        ("Stats.CreativeModeUnlocked", "CreativeMode"),
        ("Stats.CreativeModeItemUnlocked", "CreativeModeItem"),
    ];

    public UnlocksViewModel(string playerProfilePath)
    {
        _playerProfilePath = playerProfilePath;
        if (
            !File.Exists(playerProfilePath)
            || SaveJson.DeserializeJObjectFromFile(playerProfilePath)[SettingsKey]
                is not JArray settings
        )
        {
            return;
        }

        foreach (var setting in settings)
        {
            if (setting[NameKey]?.Value<string>() is { } name && Values.ContainsKey(name))
            {
                Values[name] = GetBoolSetting(setting);
            }
        }

        _loaded = new Dictionary<string, bool>(Values);
    }

    public Dictionary<string, bool> Values { get; } =
        Settings.ToDictionary(setting => setting.Key, _ => false);

    private readonly Dictionary<string, bool> _loaded = [];

    public bool HasChanges => Values.Any(pair => _loaded.GetValueOrDefault(pair.Key) != pair.Value);

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

        var settingsToSave = new Dictionary<string, bool>(Values);

        var hasChanges = false;
        foreach (var setting in settings)
        {
            if (
                setting[NameKey]?.Value<string>() is not { } settingName
                || !settingsToSave.Remove(settingName, out var settingValue)
                || GetBoolSetting(setting) == settingValue
            )
            {
                continue;
            }

            setting[BoolValueKey] = settingValue;
            hasChanges = true;
        }

        // Missing Reads As False, Only True Settings Are Added
        foreach (
            var settingKey in settingsToSave.Where(pair => pair.Value).Select(pair => pair.Key)
        )
        {
            settings.Add(new JObject { [NameKey] = settingKey, [BoolValueKey] = true });
            hasChanges = true;
        }

        // Nothing Toggled Writes Nothing, Not Even A Backup
        if (!hasChanges)
        {
            return;
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
