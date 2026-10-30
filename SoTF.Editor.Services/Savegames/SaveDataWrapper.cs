using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Services.Savegames;

public sealed class SaveDataWrapper(JToken parent)
{
    private readonly Dictionary<string, JToken> _jsonStringNodes = [];

    // Nested Documents As Loaded, Parsed Only Once Compared Or Restored
    private readonly Dictionary<string, string> _loadedTexts = [];
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
        var text = token.ToString();
        var parsed = JToken.Parse(text);
        _jsonStringNodes.Add(selector, parsed);
        _loadedTexts.Add(selector, text);
        return parsed;
    }

    private JToken? Loaded(string selector)
    {
        // Parsed On First Need, Cloning At Load Cost 100 Ms
        if (
            !_loadedNodes.TryGetValue(selector, out var loaded)
            && _loadedTexts.TryGetValue(selector, out var text)
        )
        {
            loaded = JToken.Parse(text);
            _loadedNodes.Add(selector, loaded);
        }

        return loaded;
    }

    public JToken? LoadedCopy(string selector)
    {
        return Loaded(selector)?.DeepClone();
    }

    public bool Replace(string selector, JToken document)
    {
        // A Whole Document From Elsewhere, Like A Copied Player
        if (GetJsonBasedToken(selector) is not { } current)
        {
            return false;
        }

        if (_jsonStringNodes.ContainsKey(selector))
        {
            _jsonStringNodes[selector] = document;
        }
        else
        {
            current.Replace(document);
        }

        MarkAsModified(selector);
        return true;
    }

    public void Revert(string selector)
    {
        // Only Pages That Read The Document Again Afterwards
        if (Loaded(selector) is { } loaded)
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
        return Loaded(selector) is not { } loaded
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
