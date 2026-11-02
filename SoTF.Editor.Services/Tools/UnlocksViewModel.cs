using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public sealed class UnlocksViewModel
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
        foreach (var (key, value) in ValuesOn(ReadProfile()))
        {
            Values[key] = value;
        }

        _loaded = new Dictionary<string, bool>(Values);
    }

    public Dictionary<string, bool> Values { get; } =
        Settings.ToDictionary(setting => setting.Key, _ => false);

    private readonly Dictionary<string, bool> _loaded;

    public bool HasChanges => Values.Any(pair => _loaded[pair.Key] != pair.Value);

    private JObject ReadProfile()
    {
        // A Missing Profile Reads As Every Unlock Off
        var profile = File.Exists(_playerProfilePath)
            ? SaveJson.DeserializeJObjectFromFile(_playerProfilePath)
            : new JObject();
        SettingsOf(profile);
        return (JObject)profile;
    }

    private static JArray SettingsOf(JToken profile)
    {
        // Only The Shape The Game Writes, Others Stay Untouched
        if (profile[SettingsKey] is not (null or JArray))
        {
            throw new InvalidDataException("Found Unexpected Profile Settings");
        }

        var settings = profile[SettingsKey] as JArray ?? [];
        foreach (var setting in settings)
        {
            if (
                setting is not JObject entry
                || entry[NameKey] is not { Type: JTokenType.String } name
                || (
                    Settings.Any(known => known.Key == name.ToString())
                    && entry[BoolValueKey] is not (null or { Type: JTokenType.Boolean })
                )
            )
            {
                throw new InvalidDataException("Found An Unexpected Profile Setting");
            }
        }

        return settings;
    }

    private static Dictionary<string, bool> ValuesOn(JObject profile)
    {
        // A Repeat Reads As Its Last Entry, Missing As Off
        var values = new Dictionary<string, bool>();
        foreach (var setting in SettingsOf(profile))
        {
            if (Settings.Any(known => known.Key == setting[NameKey]!.ToString()))
            {
                values[setting[NameKey]!.ToString()] =
                    setting[BoolValueKey]?.Value<bool>() ?? false;
            }
        }

        return values;
    }

    public void Save()
    {
        // Only Toggled Unlocks Are Written, None Means No Backup
        var changed = Values.Where(pair => _loaded[pair.Key] != pair.Value).ToList();
        if (changed.Count == 0)
        {
            return;
        }

        // The Profile On Disk Now, Checked Again And Merged Into
        var model = ReadProfile();
        if (model[SettingsKey] is not JArray settings)
        {
            settings = [];
            model[SettingsKey] = settings;
        }

        foreach (var (key, value) in changed)
        {
            // Every Repeat Of A Name Changes, So The Last Agrees
            var entries = settings.Where(setting => setting[NameKey]!.ToString() == key).ToList();
            entries.ForEach(entry => entry[BoolValueKey] = value);
            if (entries.Count == 0 && value)
            {
                settings.Add(new JObject { [NameKey] = key, [BoolValueKey] = true });
            }
        }

        // A Verified Copy First, Then A Write That Cannot Tear
        if (File.Exists(_playerProfilePath))
        {
            BackupManager.CreateForFile(_playerProfilePath);
        }

        FileTransaction.Replace(
            _playerProfilePath,
            temporary =>
                File.WriteAllText(
                    temporary,
                    JsonConvert.SerializeObject(model, Formatting.Indented),
                    SaveJson.Encoding
                )
        );
    }
}
