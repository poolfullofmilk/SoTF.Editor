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
        _existedAtLoad = File.Exists(playerProfilePath);
        foreach (var (key, value) in ValuesOn(ReadProfile().Profile))
        {
            Values[key] = value;
        }

        _loaded = new Dictionary<string, bool>(Values);
    }

    public Dictionary<string, bool> Values { get; } =
        Settings.ToDictionary(setting => setting.Key, _ => false);

    private readonly Dictionary<string, bool> _loaded;

    // A Profile Gone By Save Time Fails, Not Recreated
    private readonly bool _existedAtLoad;

    public bool HasChanges => Values.Any(pair => _loaded[pair.Key] != pair.Value);

    private (JObject Profile, string? Text) ReadProfile()
    {
        // A Missing Profile Reads As Every Unlock Off
        var text = File.Exists(_playerProfilePath)
            ? File.ReadAllText(_playerProfilePath, SaveJson.Encoding)
            : null;
        var profile = text is null ? [] : JObject.Parse(text);
        SettingsOf(profile);
        return (profile, text);
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
        var (model, readText) = ReadProfile();
        if (_existedAtLoad && readText is null)
        {
            throw new IOException("Found The Profile Gone Since It Was Opened");
        }

        var before = model.DeepClone();
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

        // Already As Wanted On Disk, Nothing Is Backed Up
        if (!JToken.DeepEquals(before, model))
        {
            // A Verified Copy First, Then A Write That Cannot Tear
            if (readText != null)
            {
                BackupManager.CreateForFile(_playerProfilePath);
            }

            FileTransaction.ReplaceSingle(
                _playerProfilePath,
                readText != null,
                temporary =>
                {
                    // Changed Since The Read, The Newer Profile Wins
                    if (
                        (
                            File.Exists(_playerProfilePath)
                                ? File.ReadAllText(_playerProfilePath, SaveJson.Encoding)
                                : null
                        ) != readText
                    )
                    {
                        throw new IOException("Found The Profile Changed While Saving");
                    }

                    File.WriteAllText(
                        temporary,
                        JsonConvert.SerializeObject(model, Formatting.Indented),
                        SaveJson.Encoding
                    );
                }
            );
        }

        // Saved, So The Toggles Are No Longer Pending
        foreach (var (key, value) in changed)
        {
            _loaded[key] = value;
        }
    }
}
