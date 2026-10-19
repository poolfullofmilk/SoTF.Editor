using Newtonsoft.Json;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Services.Settings;

public class UserSettings
{
    private static readonly string s_filePath = Path.Combine(
        AppInfo.DataDirectory,
        "settings.json"
    );

    public static UserSettings Current { get; } = Load();

    public string SavegamePath { get; set; } = "";
    public string LastSavegame { get; set; } = "";
    public string Language { get; set; } = "en";
    public HashSet<string>? SelectedMapGroups { get; set; }

    private static UserSettings Load()
    {
        try
        {
            return JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(s_filePath))
                ?? new UserSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
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
            File.WriteAllText(s_filePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Losing A Setting Must Never Stop An Edit
        }
    }
}
