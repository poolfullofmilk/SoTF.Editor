using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Services.Savegames;

public class SaveDataWrapper(JToken parent)
{
    private readonly Dictionary<string, JToken> _jsonStringNodes = [];

    // Nested Documents As Loaded, An Undone Edit Is No Change
    private readonly Dictionary<string, JToken> _loadedNodes = [];
    private readonly HashSet<string> _modified = [];

    public JToken Parent { get; } = parent;

    public JToken? GetJsonBasedToken(string selector)
    {
        if (_jsonStringNodes.TryGetValue(selector, out var jsonBasedToken))
        {
            return jsonBasedToken;
        }

        var token = Parent.SelectToken(selector);
        if (token is not { Type: JTokenType.String })
        {
            return token;
        }

        // Nested Json Is Parsed Once, Edits Change That Copy
        var parsed = JToken.Parse(token.ToString());
        _jsonStringNodes.Add(selector, parsed);
        _loadedNodes.Add(selector, parsed.DeepClone());
        return parsed;
    }

    public JToken? LoadedCopy(string selector)
    {
        return _loadedNodes.GetValueOrDefault(selector)?.DeepClone();
    }

    public void Revert(string selector)
    {
        // Only Pages That Read The Document Again Afterwards
        if (_loadedNodes.TryGetValue(selector, out var loaded))
        {
            _jsonStringNodes[selector] = loaded.DeepClone();
        }
    }

    public bool HasModified()
    {
        return _modified.Any(IsChanged);
    }

    public string ChangedState()
    {
        return string.Concat(
            _modified
                .Where(IsChanged)
                .Select(key =>
                    (_jsonStringNodes.GetValueOrDefault(key) ?? Parent.SelectToken(key))?.ToString(
                        Formatting.None
                    )
                )
        );
    }

    public void SerializeAllModified()
    {
        foreach (var key in _modified.Where(IsChanged))
        {
            // A Token Edited In Place Is Already In The Parent
            if (_jsonStringNodes.TryGetValue(key, out var node))
            {
                Parent.SelectToken(key)?.Replace(SaveJson.Serialize(node));
            }
        }
    }

    private bool IsChanged(string selector)
    {
        // A Token Edited In Place Has No Copy, It Counts
        return !_loadedNodes.TryGetValue(selector, out var loaded)
            || !JToken.DeepEquals(loaded, _jsonStringNodes[selector]);
    }

    public void ClearModified()
    {
        _modified.Clear();
    }

    public void MarkAsModified(string selector)
    {
        _modified.Add(selector);
    }
}
