using Newtonsoft.Json;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Settings;

public sealed class UserSettings
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly string s_filePath = Path.Combine(
        AppInfo.DataDirectory,
        "settings.json"
    );

    public static UserSettings Current { get; } = Load();

    public string SavegamePath { get; set; } = "";
    public string Language { get; set; } = AppText.DefaultCulture;
    public HashSet<string>? SelectedMapGroups { get; set; }

    // Savegames The Editor Duplicated, The Only Ones It Deletes
    public List<string> CopiedSavegames { get; set; } = [];

    private static UserSettings Load()
    {
        try
        {
            var settings =
                JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(s_filePath))
                ?? new UserSettings();

            // An Explicit Null Skips The Defaults Above
            settings.SavegamePath ??= "";
            settings.Language ??= AppText.DefaultCulture;
            settings.CopiedSavegames ??= [];
            return settings;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A Missing Or Damaged File Means Defaults
            return new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            FileTransaction.ReplaceSingle(
                s_filePath,
                false,
                temporary =>
                    File.WriteAllText(
                        temporary,
                        JsonConvert.SerializeObject(this, Formatting.Indented)
                    )
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Losing A Setting Must Never Stop An Edit
            s_logger.Warn(exception, "Failed To Save Settings");
        }
    }
}
