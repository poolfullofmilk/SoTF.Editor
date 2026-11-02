using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Services.Savegames;

public static class PlayerEntries
{
    public static (SaveDataWrapper Wrapper, JArray Entries)? Of(SavegameStore store)
    {
        return
            store.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData) is { } wrapper
            && (wrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState) as JObject)?["_entries"]
                is JArray entries
            ? (wrapper, entries)
            : null;
    }

    public static Dictionary<string, JToken> ByName(IEnumerable<JToken>? entries)
    {
        // The Game Can Repeat A Name, The Newest Wins
        var byName = new Dictionary<string, JToken>();
        foreach (var entry in entries ?? [])
        {
            // Entries Without An Object Shape Or Text Name Are Skipped
            if (entry is JObject && entry["Name"] is { Type: JTokenType.String } name)
            {
                byName[name.ToString()] = entry;
            }
        }

        return byName;
    }
}
