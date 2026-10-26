using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Services.Actors;

public static class JsonModifier
{
    public static bool CompareAndModify(
        JToken? token,
        string key,
        Func<float, bool> comparator,
        float newValue
    )
    {
        if (token == null)
        {
            return false;
        }

        // The Game Leaves Out Zero, Missing Reads As Zero
        if (!comparator(token[key]?.Value<float>() ?? 0f))
        {
            return false;
        }

        token[key] = newValue;
        return true;
    }

    public static bool SetFloat(JToken? token, string key, float newValue)
    {
        return CompareAndModify(token, key, value => Math.Abs(value - newValue) >= 0.001, newValue);
    }

    public static int? UniqueIdOf(JToken entry)
    {
        return entry["UniqueId"]?.Value<int>();
    }

    public static void RestoreEntries(
        JToken? current,
        JToken? loaded,
        Func<JToken, bool> owned,
        Func<JToken, int?>? keyOf = null
    )
    {
        if (current is not JArray currentArray)
        {
            return;
        }

        // Owned Entries Go Back, Others Keep Their Edits And Tokens
        var byKey = currentArray
            .Where(entry => keyOf?.Invoke(entry) != null)
            .GroupBy(entry => keyOf!(entry)!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var keptInOrder = currentArray.Where(entry => !owned(entry)).ToList();
        var used = new HashSet<JToken>(ReferenceEqualityComparer.Instance);
        var merged = new List<JToken>();
        foreach (var entry in loaded as JArray ?? [])
        {
            var isOwned = owned(entry);
            JToken? match;
            if (keyOf?.Invoke(entry) is { } key)
            {
                match = byKey
                    .GetValueOrDefault(key)
                    ?.FirstOrDefault(candidate =>
                        !used.Contains(candidate)
                        && owned(candidate) == isOwned
                        && (!isOwned || JToken.DeepEquals(candidate, entry))
                    );
            }
            else if (isOwned)
            {
                match = currentArray.FirstOrDefault(candidate =>
                    !used.Contains(candidate)
                    && owned(candidate)
                    && JToken.DeepEquals(candidate, entry)
                );
            }
            else
            {
                match = keptInOrder.FirstOrDefault(candidate => !used.Contains(candidate));
            }

            if (match != null)
            {
                used.Add(match);
                merged.Add(match);
            }
            else if (isOwned)
            {
                merged.Add(entry.DeepClone());
            }
        }

        // New Entries Not Owned Stay At The End
        merged.AddRange(currentArray.Where(entry => !owned(entry) && !used.Contains(entry)));
        if (merged.SequenceEqual(currentArray, ReferenceEqualityComparer.Instance))
        {
            return;
        }

        currentArray.Clear();
        merged.ForEach(currentArray.Add);
    }
}
