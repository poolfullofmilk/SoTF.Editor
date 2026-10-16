using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Services.Savegames;

public class SaveDataWrapper(JToken parent)
{
    private readonly Dictionary<string, JToken> _jsonStringNodes = [];
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
        var parsed = SaveJson.DeserializeRaw(token.ToString());
        _jsonStringNodes.Add(selector, parsed);
        return parsed;
    }

    public bool HasModified()
    {
        return _modified.Count > 0;
    }

    public void SerializeAllModified()
    {
        foreach (var key in _modified)
        {
            if (_jsonStringNodes.TryGetValue(key, out var token))
            {
                Parent.SelectToken(key)?.Replace(SaveJson.Serialize(token));
            }
        }
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
