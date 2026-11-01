using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Tools;

public sealed class ModifyConsumedItemsViewModel
{
    private const string Prefix = "consumed.";
    private readonly Savegame _savegame;

    public ModifyConsumedItemsViewModel(Savegame savegame, GameData gameData)
    {
        _savegame = savegame;
        ConsumedItems =
        [
            .. (PlayerEntries.Of(_savegame.SavegameStore)?.Entries ?? [])
                .Select(entry => entry["Name"]?.Value<string>())
                .OfType<string>()
                .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
                .Select(name => new ConsumedItemWrapper(
                    name,
                    int.TryParse(name[Prefix.Length..], out var itemId)
                        ? gameData.Items.GetItem(itemId)
                        : null
                ))
                .OrderBy(wrapper => wrapper.Name),
        ];
    }

    public List<ConsumedItemWrapper> ConsumedItems { get; }

    public void RemoveAll()
    {
        Remove(ConsumedItems);
    }

    public void Save()
    {
        Remove(ConsumedItems.Where(wrapper => wrapper.Remove));
    }

    private void Remove(IEnumerable<ConsumedItemWrapper> wrappers)
    {
        if (PlayerEntries.Of(_savegame.SavegameStore) is not (var saveDataWrapper, var entries))
        {
            return;
        }

        var names = wrappers.Select(wrapper => wrapper.Key).ToHashSet();
        var removedEntries = entries
            .Where(entry => entry["Name"]?.Value<string>() is { } name && names.Contains(name))
            .ToList();
        removedEntries.ForEach(entry => entry.Remove());
        if (removedEntries.Count > 0)
        {
            saveDataWrapper.MarkAsModified(Constants.JsonKeys.PlayerState);
        }
    }

    public class ConsumedItemWrapper(string key, Item? item)
    {
        public bool Remove { get; set; }

        public string Key { get; } = key;

        public string Name => item?.Name ?? Key;

        public string? ImageUrl => item?.ImageUrl;
    }
}
