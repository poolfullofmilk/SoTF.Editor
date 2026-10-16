using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public class ModifyConsumedItemsViewModel
{
    private const string Prefix = "consumed.";
    private readonly Savegame _savegame;

    public ModifyConsumedItemsViewModel(Savegame savegame, GameData gameData)
    {
        _savegame = savegame;
        Load(savegame, gameData.Items);
    }

    public List<ConsumedItemWrapper> ConsumedItems { get; } = [];

    private void Load(Savegame savegame, ItemList items)
    {
        if (
            savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
            is not { } saveDataWrapper
        )
        {
            return;
        }

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState) is not { } playerState
            || playerState["_entries"] is not JArray entries
        )
        {
            return;
        }

        var consumedItems = new List<ConsumedItemWrapper>();

        foreach (var entry in entries)
        {
            if (entry["Name"]?.Value<string>() is not { } name || !name.StartsWith("consumed."))
            {
                continue;
            }

            var item = GetItemId(name) is { } itemId ? items.GetItem(itemId) : null;
            consumedItems.Add(new ConsumedItemWrapper(name, item));
        }

        ConsumedItems.AddRange(consumedItems.OrderBy(wrapper => wrapper.Name));
    }

    public void RemoveAll()
    {
        if (
            _savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
            is not { } saveDataWrapper
        )
        {
            return;
        }

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState) is not { } playerState
            || playerState["_entries"] is not JArray entries
        )
        {
            return;
        }

        var toBeRemoved = ConsumedItems.Select(wrapper => wrapper.Key).ToHashSet();
        var hasChanges = RemoveEntries(entries, toBeRemoved);
        if (hasChanges)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);
        }
    }

    private static bool RemoveEntries(JArray entries, HashSet<string> toBeRemoved)
    {
        if (toBeRemoved.Count == 0)
        {
            return false;
        }

        var tokensToBeRemoved = new List<JToken>();

        foreach (var entry in entries)
        {
            if (entry["Name"]?.Value<string>() is not { } name || !toBeRemoved.Contains(name))
            {
                continue;
            }

            tokensToBeRemoved.Add(entry);
        }

        foreach (var tokenToBeRemoved in tokensToBeRemoved)
        {
            entries.Remove(tokenToBeRemoved);
        }

        return tokensToBeRemoved.Count > 0;
    }

    public void Save()
    {
        if (
            _savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.PlayerStateSaveData)
            is not { } saveDataWrapper
        )
        {
            return;
        }

        if (
            saveDataWrapper.GetJsonBasedToken(Constants.JsonKeys.PlayerState) is not { } playerState
            || playerState["_entries"] is not JArray entries
        )
        {
            return;
        }

        var toBeRemoved = ConsumedItems
            .Where(wrapper => wrapper.Remove)
            .Select(wrapper => wrapper.Key)
            .ToHashSet();
        var hasChanges = RemoveEntries(entries, toBeRemoved);
        if (hasChanges)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);
        }
    }

    private static int? GetItemId(string key)
    {
        if (int.TryParse(key.Replace(Prefix, "").TrimStart('0'), out var result))
        {
            return result;
        }

        return null;
    }

    public class ConsumedItemWrapper(string key, Item? item)
    {
        public bool Remove { get; set; }

        public string Key { get; } = key;

        public string Name => item != null ? item.Name : Key;
    }
}
